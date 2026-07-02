using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PBS.ERP.Modules.GitHub.Models;
using PBS.ERP.Modules.GitHub.Services;

namespace PBS.ERP.Modules.GitHub.Controllers;

[ApiController]
[Authorize]
[Route("api/github/test")]
public sealed class GitHubTestController : ControllerBase
{
    private readonly GitHubOptions _options;
    private readonly GitHubAuthService _authService;
    private readonly GitHubApiClient _client;

    public GitHubTestController(
        IOptions<GitHubOptions> options,
        GitHubAuthService authService,
        GitHubApiClient client)
    {
        _options = options.Value;
        _authService = authService;
        _client = client;
    }

    [HttpGet("config")]
    public IActionResult Config()
    {
        return Ok(new
        {
            Message = "GitHub configuration loaded.",
            Success = true,
            Data = new
            {
                _options.Mode,
                _options.Owner,
                _options.Repository,
                _options.Organization,
                _options.AppId,
                _options.InstallationId,
                HasPrivateKeyPath = !string.IsNullOrWhiteSpace(_options.PrivateKeyPath),
                PrivateKeyPath = _options.PrivateKeyPath,
                PrivateKeyFileExists = System.IO.File.Exists(_options.PrivateKeyPath),
                _options.ApiBaseUrl,
                _options.ApiVersion
            },
            Errors = Array.Empty<string>()
        });
    }

    [HttpGet("token")]
    public async Task<IActionResult> Token()
    {
        var token = await _authService.GetInstallationTokenAsync();

        return Ok(new
        {
            Message = "GitHub installation token generated successfully.",
            Success = true,
            Data = new
            {
                TokenPreview = token.Length > 15
                    ? token.Substring(0, 10) + "..."
                    : "***",
                TokenLength = token.Length
            },
            Errors = Array.Empty<string>()
        });
    }

    [HttpGet("repo")]
    public async Task<IActionResult> Repo()
    {
        if (string.IsNullOrWhiteSpace(_options.Owner) ||
            string.IsNullOrWhiteSpace(_options.Repository))
        {
            return BadRequest(new
            {
                Message = "GitHub Owner or Repository is missing in appsettings.json.",
                Success = false,
                Data = (object?)null,
                Errors = new[] { "Set GitHub:Owner and GitHub:Repository." }
            });
        }

        using var doc = await _client.GetAsync(
            $"/repos/{_options.Owner}/{_options.Repository}");

        var r = doc.RootElement;

        var data = new
        {
            GitHubId = r.GetProperty("id").GetInt64(),
            Name = r.GetProperty("name").GetString(),
            FullName = r.GetProperty("full_name").GetString(),
            Private = r.GetProperty("private").GetBoolean(),
            HtmlUrl = r.GetProperty("html_url").GetString(),
            DefaultBranch = r.TryGetProperty("default_branch", out var db)
                ? db.GetString()
                : null,
            Language = r.TryGetProperty("language", out var lang) &&
                       lang.ValueKind != System.Text.Json.JsonValueKind.Null
                ? lang.GetString()
                : null,
            PushedAt = r.TryGetProperty("pushed_at", out var pushed) &&
                       pushed.ValueKind != System.Text.Json.JsonValueKind.Null
                ? pushed.GetDateTime()
                : (DateTime?)null
        };

        return Ok(new
        {
            Message = "GitHub repository loaded successfully.",
            Success = true,
            Data = data,
            Errors = Array.Empty<string>()
        });
    }

    [HttpGet("repos")]
    public async Task<IActionResult> Repos()
    {
        if (string.Equals(_options.Mode, "SingleRepository", StringComparison.OrdinalIgnoreCase))
        {
            using var doc = await _client.GetAsync(
                $"/repos/{_options.Owner}/{_options.Repository}");

            var r = doc.RootElement;

            var singleRepo = new[]
            {
            new
            {
                GitHubId = r.GetProperty("id").GetInt64(),
                Name = r.GetProperty("name").GetString(),
                FullName = r.GetProperty("full_name").GetString(),
                Private = r.GetProperty("private").GetBoolean(),
                HtmlUrl = r.GetProperty("html_url").GetString(),
                DefaultBranch = r.TryGetProperty("default_branch", out var db)
                    ? db.GetString()
                    : null,
                Language = r.TryGetProperty("language", out var lang) &&
                           lang.ValueKind != System.Text.Json.JsonValueKind.Null
                    ? lang.GetString()
                    : null,
                PushedAt = r.TryGetProperty("pushed_at", out var pushed) &&
                           pushed.ValueKind != System.Text.Json.JsonValueKind.Null
                    ? pushed.GetDateTime()
                    : (DateTime?)null
            }
        };

            return Ok(new
            {
                Message = "Single GitHub repository loaded successfully.",
                Success = true,
                Data = singleRepo,
                Errors = Array.Empty<string>()
            });
        }

        var repos = await _client.GetPagedArrayAsync(
            $"/orgs/{_options.Organization}/repos?type=all&sort=pushed&direction=desc",
            maxPages: 2);

        var data = repos.Select(r => new
        {
            GitHubId = r.GetProperty("id").GetInt64(),
            Name = r.GetProperty("name").GetString(),
            FullName = r.GetProperty("full_name").GetString(),
            Private = r.GetProperty("private").GetBoolean(),
            HtmlUrl = r.GetProperty("html_url").GetString(),
            DefaultBranch = r.TryGetProperty("default_branch", out var db)
                ? db.GetString()
                : null,
            Language = r.TryGetProperty("language", out var lang) &&
                       lang.ValueKind != System.Text.Json.JsonValueKind.Null
                ? lang.GetString()
                : null,
            PushedAt = r.TryGetProperty("pushed_at", out var pushed) &&
                       pushed.ValueKind != System.Text.Json.JsonValueKind.Null
                ? pushed.GetDateTime()
                : (DateTime?)null
        }).ToList();

        return Ok(new
        {
            Message = "GitHub organization repositories loaded successfully.",
            Success = true,
            Data = data,
            Errors = Array.Empty<string>()
        });
    }
}