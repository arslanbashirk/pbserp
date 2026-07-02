using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PBS.ERP.Modules.GitHub.Models;

namespace PBS.ERP.Modules.GitHub.Services;

public sealed class GitHubApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GitHubAuthService _authService;
    private readonly GitHubOptions _options;

    public GitHubApiClient(
        IHttpClientFactory httpClientFactory,
        GitHubAuthService authService,
        IOptions<GitHubOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _authService = authService;
        _options = options.Value;
    }

    public async Task<JsonDocument> GetAsync(string relativeOrAbsoluteUrl, CancellationToken ct = default)
    {
        var token = await _authService.GetInstallationTokenAsync(ct);

        var url = relativeOrAbsoluteUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? relativeOrAbsoluteUrl
            : $"{_options.ApiBaseUrl}{relativeOrAbsoluteUrl}";

        using var client = _httpClientFactory.CreateClient("GitHubRaw");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", _options.ApiVersion);
        request.Headers.UserAgent.ParseAdd("PBS-ERP-GitHub-Dashboard");

        using var response = await client.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            return JsonDocument.Parse("""
            {
                "status": "accepted",
                "message": "GitHub accepted the request but is preparing the result. Retry later."
            }
            """);
        }

        if (!response.IsSuccessStatusCode)
        {
            var remaining = response.Headers.TryGetValues("x-ratelimit-remaining", out var r)
                ? r.FirstOrDefault()
                : null;

            var reset = response.Headers.TryGetValues("x-ratelimit-reset", out var x)
                ? x.FirstOrDefault()
                : null;

            throw new InvalidOperationException(
                $"GitHub API error: {(int)response.StatusCode}. Remaining={remaining}, Reset={reset}, Body={json}");
        }

        return JsonDocument.Parse(json);
    }

    public async Task<List<JsonElement>> GetPagedArrayAsync(
        string relativeUrl,
        string? arrayPropertyName = null,
        int maxPages = 5,
        CancellationToken ct = default)
    {
        var result = new List<JsonElement>();

        for (var page = 1; page <= maxPages; page++)
        {
            var separator = relativeUrl.Contains('?') ? "&" : "?";
            var url = $"{relativeUrl}{separator}per_page=100&page={page}";

            using var doc = await GetAsync(url, ct);

            JsonElement array;

            if (!string.IsNullOrWhiteSpace(arrayPropertyName))
            {
                if (!doc.RootElement.TryGetProperty(arrayPropertyName, out array))
                    break;
            }
            else
            {
                array = doc.RootElement;
            }

            if (array.ValueKind != JsonValueKind.Array)
                break;

            var items = array.EnumerateArray().ToList();

            if (items.Count == 0)
                break;

            foreach (var item in items)
                result.Add(item.Clone());

            if (items.Count < 100)
                break;
        }

        return result;
    }
}