using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PBS.ERP.Modules.GitHub.Models;

namespace PBS.ERP.Modules.GitHub.Services;

public sealed class GitHubAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GitHubOptions _options;

    private string? _cachedInstallationToken;
    private DateTimeOffset _cachedTokenExpiresAt;

    public GitHubAuthService(
        IHttpClientFactory httpClientFactory,
        IOptions<GitHubOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<string> GetInstallationTokenAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_cachedInstallationToken) &&
            _cachedTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return _cachedInstallationToken;
        }

        var jwt = GenerateAppJwt();

        using var client = _httpClientFactory.CreateClient("GitHubRaw");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.ApiBaseUrl}/app/installations/{_options.InstallationId}/access_tokens");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", _options.ApiVersion);
        request.Headers.UserAgent.ParseAdd("PBS-ERP-GitHub-Dashboard");

        using var response = await client.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"GitHub installation token error: {(int)response.StatusCode} {json}");
        }

        using var doc = JsonDocument.Parse(json);

        _cachedInstallationToken = doc.RootElement.GetProperty("token").GetString();
        _cachedTokenExpiresAt = doc.RootElement.GetProperty("expires_at").GetDateTimeOffset();

        return _cachedInstallationToken!;
    }

    private string GenerateAppJwt()
    {
        if (string.IsNullOrWhiteSpace(_options.AppId))
            throw new InvalidOperationException("GitHub AppId is missing in appsettings.json.");

        if (string.IsNullOrWhiteSpace(_options.PrivateKeyPath))
            throw new InvalidOperationException("GitHub PrivateKeyPath is missing in appsettings.json.");

        if (!File.Exists(_options.PrivateKeyPath))
            throw new FileNotFoundException("GitHub private key file not found.", _options.PrivateKeyPath);

        var privateKeyPem = File.ReadAllText(_options.PrivateKeyPath);

        RSA? rsa = null;

        try
        {
            rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem);

            var now = DateTimeOffset.UtcNow;

            var securityKey = new RsaSecurityKey(rsa)
            {
                KeyId = _options.AppId
            };

            var credentials = new SigningCredentials(
                securityKey,
                SecurityAlgorithms.RsaSha256);

            var claims = new List<System.Security.Claims.Claim>
        {
            new(
                JwtRegisteredClaimNames.Iat,
                now.AddSeconds(-60).ToUnixTimeSeconds().ToString(),
                System.Security.Claims.ClaimValueTypes.Integer64),

            new(
                JwtRegisteredClaimNames.Iss,
                _options.AppId)
        };

            var token = new JwtSecurityToken(
                issuer: _options.AppId,
                audience: null,
                claims: claims,
                notBefore: null,
                expires: now.AddMinutes(9).UtcDateTime,
                signingCredentials: credentials);

            var handler = new JwtSecurityTokenHandler();

            // IMPORTANT:
            // WriteToken must execute before RSA is disposed.
            var jwt = handler.WriteToken(token);

            return jwt;
        }
        finally
        {
            rsa?.Dispose();
        }
    }
}