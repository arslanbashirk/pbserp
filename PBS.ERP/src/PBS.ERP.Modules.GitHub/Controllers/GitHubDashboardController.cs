using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PBS.ERP.Modules.GitHub.Services;

namespace PBS.ERP.Modules.GitHub.Controllers;

[ApiController]
[Authorize]
[Route("api/github/dashboard")]
public sealed class GitHubDashboardController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly GitHubStatsSyncService _syncService;

    public GitHubDashboardController(
        IConfiguration configuration,
        GitHubStatsSyncService syncService)
    {
        _configuration = configuration;
        _syncService = syncService;
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromQuery] int days = 30)
    {
        await _syncService.SyncPhase1Async(days);

        return Ok(new
        {
            Message = "GitHub statistics synchronized successfully.",
            Success = true,
            Data = new { Days = days },
            Errors = Array.Empty<string>()
        });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var dateFrom = (from ?? DateTime.UtcNow.Date.AddDays(-30)).Date;
        var dateTo = (to ?? DateTime.UtcNow.Date).Date;

        await using var con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));

        var data = await con.QueryFirstAsync("""
        SELECT
            (SELECT COUNT(*) FROM dbo.GitHubRepository WHERE IsArchived = 0) AS ActiveRepositories,
            ISNULL(SUM(Commits), 0) AS Commits,
            ISNULL(SUM(PullRequestsOpened), 0) AS PullRequestsOpened,
            ISNULL(SUM(PullRequestsMerged), 0) AS PullRequestsMerged,
            ISNULL(SUM(PullRequestsClosed), 0) AS PullRequestsClosed,
            ISNULL(SUM(WorkflowRuns), 0) AS WorkflowRuns,
            ISNULL(SUM(WorkflowFailures), 0) AS WorkflowFailures,
            ISNULL(SUM(Views), 0) AS Views,
            ISNULL(SUM(Clones), 0) AS Clones
        FROM dbo.GitHubDailyRepoStats
        WHERE StatDate >= @From
          AND StatDate <= @To;
        """, new { From = dateFrom, To = dateTo });

        return Ok(new
        {
            Message = "GitHub dashboard summary loaded.",
            Success = true,
            Data = data,
            Errors = Array.Empty<string>()
        });
    }

    [HttpGet("daily-trend")]
    public async Task<IActionResult> DailyTrend([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var dateFrom = (from ?? DateTime.UtcNow.Date.AddDays(-30)).Date;
        var dateTo = (to ?? DateTime.UtcNow.Date).Date;

        await using var con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));

        var rows = await con.QueryAsync("""
        SELECT
            StatDate,
            SUM(Commits) AS Commits,
            SUM(PullRequestsOpened) AS PullRequestsOpened,
            SUM(PullRequestsMerged) AS PullRequestsMerged,
            SUM(WorkflowFailures) AS WorkflowFailures
        FROM dbo.GitHubDailyRepoStats
        WHERE StatDate >= @From
          AND StatDate <= @To
        GROUP BY StatDate
        ORDER BY StatDate;
        """, new { From = dateFrom, To = dateTo });

        return Ok(new
        {
            Message = "GitHub daily trend loaded.",
            Success = true,
            Data = rows,
            Errors = Array.Empty<string>()
        });
    }

    [HttpGet("top-developers")]
    public async Task<IActionResult> TopDevelopers([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var dateFrom = (from ?? DateTime.UtcNow.Date.AddDays(-30)).Date;
        var dateTo = (to ?? DateTime.UtcNow.Date).Date;

        await using var con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));

        var rows = await con.QueryAsync("""
        SELECT TOP 20
            U.GitHubLogin,
            COALESCE(M.DisplayName, U.GitHubLogin) AS DisplayName,
            M.PNO,
            M.TeamName,
            SUM(U.Commits) AS Commits,
            SUM(U.PullRequestsOpened) AS PullRequestsOpened,
            SUM(U.PullRequestsMerged) AS PullRequestsMerged,
            SUM(U.WorkflowRuns) AS WorkflowRuns,
            SUM(U.WorkflowFailures) AS WorkflowFailures
        FROM dbo.GitHubDailyUserStats U
        LEFT JOIN dbo.GitHubUserMapping M
            ON M.GitHubLogin = U.GitHubLogin
        WHERE U.StatDate >= @From
          AND U.StatDate <= @To
        GROUP BY U.GitHubLogin, M.DisplayName, M.PNO, M.TeamName
        ORDER BY
            SUM(U.PullRequestsMerged) DESC,
            SUM(U.Commits) DESC,
            SUM(U.WorkflowRuns) DESC;
        """, new { From = dateFrom, To = dateTo });

        return Ok(new
        {
            Message = "Top developers loaded.",
            Success = true,
            Data = rows,
            Errors = Array.Empty<string>()
        });
    }

    [HttpGet("repositories")]
    public async Task<IActionResult> Repositories([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var dateFrom = (from ?? DateTime.UtcNow.Date.AddDays(-30)).Date;
        var dateTo = (to ?? DateTime.UtcNow.Date).Date;

        await using var con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));

        var rows = await con.QueryAsync("""
        SELECT
            R.FullName,
            R.HtmlUrl,
            R.DefaultBranch,
            R.Language,
            R.LastPushedAt,
            ISNULL(SUM(S.Commits), 0) AS Commits,
            ISNULL(SUM(S.PullRequestsOpened), 0) AS PullRequestsOpened,
            ISNULL(SUM(S.PullRequestsMerged), 0) AS PullRequestsMerged,
            ISNULL(SUM(S.WorkflowRuns), 0) AS WorkflowRuns,
            ISNULL(SUM(S.WorkflowFailures), 0) AS WorkflowFailures,
            ISNULL(SUM(S.Views), 0) AS Views,
            ISNULL(SUM(S.Clones), 0) AS Clones
        FROM dbo.GitHubRepository R
        LEFT JOIN dbo.GitHubDailyRepoStats S
            ON S.RepoGitHubId = R.GitHubId
           AND S.StatDate >= @From
           AND S.StatDate <= @To
        GROUP BY
            R.FullName,
            R.HtmlUrl,
            R.DefaultBranch,
            R.Language,
            R.LastPushedAt
        ORDER BY
            ISNULL(SUM(S.Commits), 0) DESC,
            R.LastPushedAt DESC;
        """, new { From = dateFrom, To = dateTo });

        return Ok(new
        {
            Message = "Repositories loaded.",
            Success = true,
            Data = rows,
            Errors = Array.Empty<string>()
        });
    }
}