using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PBS.ERP.Modules.GitHub.Models;

namespace PBS.ERP.Modules.GitHub.Services;

public sealed class GitHubStatsSyncService
{
    private readonly GitHubApiClient _client;
    private readonly GitHubOptions _options;
    private readonly string _connectionString;

    public GitHubStatsSyncService(
        GitHubApiClient client,
        IOptions<GitHubOptions> options,
        IConfiguration configuration)
    {
        _client = client;
        _options = options.Value;
        _connectionString = configuration.GetConnectionString("GitHubDb")
        ?? throw new InvalidOperationException("GitHubDb not found.");
    }

    public async Task SyncPhase1Async(int days = 30, CancellationToken ct = default)
    {
        if (days <= 0 || days > 90)
            days = 30;

        var from = DateTime.UtcNow.Date.AddDays(-days);
        var to = DateTime.UtcNow.Date.AddDays(1);

        var fullName = $"{_options.Owner}/{_options.Repository}";

        await LogAsync("Phase1", fullName, "Started", "GitHub Phase 1 sync started.", null);

        try
        {
            var repo = await SyncRepositoryAsync(ct);

            var repoStats = new Dictionary<DateTime, RepoDayStats>();
            var userStats = new Dictionary<(DateTime Date, string Login), UserDayStats>();

            await CountCommitsAsync(repo, from, to, repoStats, userStats, ct);
            await CountPullRequestsAsync(repo, from, to, repoStats, userStats, ct);
            await CountWorkflowRunsAsync(repo, from, to, repoStats, userStats, ct);
            await TryCountTrafficAsync(repo, repoStats, ct);

            await SaveRepoStatsAsync(repo.GitHubId, repoStats);
            await SaveUserStatsAsync(repo.GitHubId, userStats);

            await LogAsync("Phase1", fullName, "Success", "GitHub Phase 1 sync completed.", repoStats.Count);
        }
        catch (Exception ex)
        {
            await LogAsync("Phase1", fullName, "Failed", ex.ToString(), null);
            throw;
        }
    }

    private async Task<GitHubRepoDto> SyncRepositoryAsync(CancellationToken ct)
    {
        using var doc = await _client.GetAsync($"/repos/{_options.Owner}/{_options.Repository}", ct);
        var r = doc.RootElement;

        var repo = new GitHubRepoDto
        {
            GitHubId = r.GetProperty("id").GetInt64(),
            OwnerName = _options.Owner,
            RepoName = r.GetProperty("name").GetString() ?? _options.Repository,
            FullName = r.GetProperty("full_name").GetString() ?? $"{_options.Owner}/{_options.Repository}",
            HtmlUrl = TryGetNullableString(r, "html_url"),
            DefaultBranch = TryGetNullableString(r, "default_branch"),
            Language = TryGetNullableString(r, "language"),
            IsPrivate = r.GetProperty("private").GetBoolean(),
            IsArchived = TryGetBool(r, "archived"),
            LastPushedAt = TryGetNullableDateTime(r, "pushed_at")
        };

        await using var con = new SqlConnection(_connectionString);

        await con.ExecuteAsync("""
        MERGE dbo.GitHubRepository AS T
        USING (
            SELECT
                @GitHubId AS GitHubId,
                @OwnerName AS OwnerName,
                @RepoName AS RepoName,
                @FullName AS FullName,
                @HtmlUrl AS HtmlUrl,
                @DefaultBranch AS DefaultBranch,
                @Language AS Language,
                @IsPrivate AS IsPrivate,
                @IsArchived AS IsArchived,
                @LastPushedAt AS LastPushedAt
        ) AS S
        ON T.GitHubId = S.GitHubId
        WHEN MATCHED THEN UPDATE SET
            OwnerName = S.OwnerName,
            RepoName = S.RepoName,
            FullName = S.FullName,
            HtmlUrl = S.HtmlUrl,
            DefaultBranch = S.DefaultBranch,
            Language = S.Language,
            IsPrivate = S.IsPrivate,
            IsArchived = S.IsArchived,
            LastPushedAt = S.LastPushedAt,
            LastSyncedAt = SYSUTCDATETIME()
        WHEN NOT MATCHED THEN INSERT
            (GitHubId, OwnerName, RepoName, FullName, HtmlUrl, DefaultBranch, Language,
             IsPrivate, IsArchived, LastPushedAt, LastSyncedAt)
        VALUES
            (S.GitHubId, S.OwnerName, S.RepoName, S.FullName, S.HtmlUrl, S.DefaultBranch, S.Language,
             S.IsPrivate, S.IsArchived, S.LastPushedAt, SYSUTCDATETIME());
        """, repo);

        return repo;
    }

    private async Task CountCommitsAsync(
        GitHubRepoDto repo,
        DateTime from,
        DateTime to,
        Dictionary<DateTime, RepoDayStats> repoStats,
        Dictionary<(DateTime Date, string Login), UserDayStats> userStats,
        CancellationToken ct)
    {
        var commits = await _client.GetPagedArrayAsync(
            $"/repos/{repo.OwnerName}/{repo.RepoName}/commits?since={from:O}&until={to:O}",
            maxPages: 10,
            ct: ct);

        foreach (var commit in commits)
        {
            var commitDate = GetCommitDate(commit);
            if (commitDate == null)
                continue;

            var date = commitDate.Value.Date;
            var login = GetCommitAuthorLogin(commit);

            GetRepoDay(repoStats, date).Commits++;

            if (!string.IsNullOrWhiteSpace(login))
                GetUserDay(userStats, date, login).Commits++;
        }
    }

    private async Task CountPullRequestsAsync(
        GitHubRepoDto repo,
        DateTime from,
        DateTime to,
        Dictionary<DateTime, RepoDayStats> repoStats,
        Dictionary<(DateTime Date, string Login), UserDayStats> userStats,
        CancellationToken ct)
    {
        var pulls = await _client.GetPagedArrayAsync(
            $"/repos/{repo.OwnerName}/{repo.RepoName}/pulls?state=all&sort=updated&direction=desc",
            maxPages: 10,
            ct: ct);

        foreach (var pr in pulls)
        {
            var author = GetNestedLogin(pr, "user");

            var createdAt = TryGetNullableDateTime(pr, "created_at");
            if (createdAt != null && createdAt.Value >= from && createdAt.Value < to)
            {
                var date = createdAt.Value.Date;
                GetRepoDay(repoStats, date).PullRequestsOpened++;

                if (!string.IsNullOrWhiteSpace(author))
                    GetUserDay(userStats, date, author).PullRequestsOpened++;
            }

            var closedAt = TryGetNullableDateTime(pr, "closed_at");
            if (closedAt != null && closedAt.Value >= from && closedAt.Value < to)
            {
                var date = closedAt.Value.Date;
                GetRepoDay(repoStats, date).PullRequestsClosed++;
            }

            var mergedAt = TryGetNullableDateTime(pr, "merged_at");
            if (mergedAt != null && mergedAt.Value >= from && mergedAt.Value < to)
            {
                var date = mergedAt.Value.Date;
                GetRepoDay(repoStats, date).PullRequestsMerged++;

                if (!string.IsNullOrWhiteSpace(author))
                    GetUserDay(userStats, date, author).PullRequestsMerged++;
            }
        }
    }

    private async Task CountWorkflowRunsAsync(
        GitHubRepoDto repo,
        DateTime from,
        DateTime to,
        Dictionary<DateTime, RepoDayStats> repoStats,
        Dictionary<(DateTime Date, string Login), UserDayStats> userStats,
        CancellationToken ct)
    {
        var runs = await _client.GetPagedArrayAsync(
            $"/repos/{repo.OwnerName}/{repo.RepoName}/actions/runs?created={from:yyyy-MM-dd}..{to:yyyy-MM-dd}",
            arrayPropertyName: "workflow_runs",
            maxPages: 10,
            ct: ct);

        foreach (var run in runs)
        {
            var createdAt = TryGetNullableDateTime(run, "created_at");
            if (createdAt == null || createdAt.Value < from || createdAt.Value >= to)
                continue;

            var date = createdAt.Value.Date;
            var actor = GetNestedLogin(run, "actor");
            var conclusion = TryGetNullableString(run, "conclusion");

            var repoDay = GetRepoDay(repoStats, date);
            repoDay.WorkflowRuns++;

            var isFailure = string.Equals(conclusion, "failure", StringComparison.OrdinalIgnoreCase);

            if (isFailure)
                repoDay.WorkflowFailures++;

            if (!string.IsNullOrWhiteSpace(actor))
            {
                var userDay = GetUserDay(userStats, date, actor);
                userDay.WorkflowRuns++;

                if (isFailure)
                    userDay.WorkflowFailures++;
            }
        }
    }

    private async Task TryCountTrafficAsync(
        GitHubRepoDto repo,
        Dictionary<DateTime, RepoDayStats> repoStats,
        CancellationToken ct)
    {
        try
        {
            using var viewsDoc = await _client.GetAsync(
                $"/repos/{repo.OwnerName}/{repo.RepoName}/traffic/views?per=day",
                ct);

            if (viewsDoc.RootElement.TryGetProperty("views", out var views) &&
                views.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in views.EnumerateArray())
                {
                    var date = TryGetNullableDateTime(item, "timestamp")?.Date;
                    if (date == null) continue;

                    var day = GetRepoDay(repoStats, date.Value);
                    day.Views = TryGetInt(item, "count");
                    day.UniqueViews = TryGetInt(item, "uniques");
                }
            }

            using var clonesDoc = await _client.GetAsync(
                $"/repos/{repo.OwnerName}/{repo.RepoName}/traffic/clones?per=day",
                ct);

            if (clonesDoc.RootElement.TryGetProperty("clones", out var clones) &&
                clones.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in clones.EnumerateArray())
                {
                    var date = TryGetNullableDateTime(item, "timestamp")?.Date;
                    if (date == null) continue;

                    var day = GetRepoDay(repoStats, date.Value);
                    day.Clones = TryGetInt(item, "count");
                    day.UniqueClones = TryGetInt(item, "uniques");
                }
            }
        }
        catch
        {
            // Traffic needs sufficient repository permission.
            // Keep Phase 1 sync successful even if traffic is unavailable.
        }
    }

    private async Task SaveRepoStatsAsync(
        long repoGitHubId,
        Dictionary<DateTime, RepoDayStats> stats)
    {
        await using var con = new SqlConnection(_connectionString);

        const string sql = """
        MERGE dbo.GitHubDailyRepoStats AS T
        USING (
            SELECT @StatDate AS StatDate, @RepoGitHubId AS RepoGitHubId
        ) AS S
        ON T.StatDate = S.StatDate AND T.RepoGitHubId = S.RepoGitHubId
        WHEN MATCHED THEN UPDATE SET
            Commits = @Commits,
            PullRequestsOpened = @PullRequestsOpened,
            PullRequestsMerged = @PullRequestsMerged,
            PullRequestsClosed = @PullRequestsClosed,
            WorkflowRuns = @WorkflowRuns,
            WorkflowFailures = @WorkflowFailures,
            Views = @Views,
            UniqueViews = @UniqueViews,
            Clones = @Clones,
            UniqueClones = @UniqueClones,
            CalculatedAt = SYSUTCDATETIME()
        WHEN NOT MATCHED THEN INSERT
            (StatDate, RepoGitHubId, Commits, PullRequestsOpened, PullRequestsMerged,
             PullRequestsClosed, WorkflowRuns, WorkflowFailures, Views, UniqueViews,
             Clones, UniqueClones, CalculatedAt)
        VALUES
            (@StatDate, @RepoGitHubId, @Commits, @PullRequestsOpened, @PullRequestsMerged,
             @PullRequestsClosed, @WorkflowRuns, @WorkflowFailures, @Views, @UniqueViews,
             @Clones, @UniqueClones, SYSUTCDATETIME());
        """;

        foreach (var row in stats)
        {
            await con.ExecuteAsync(sql, new
            {
                StatDate = row.Key.Date,
                RepoGitHubId = repoGitHubId,
                row.Value.Commits,
                row.Value.PullRequestsOpened,
                row.Value.PullRequestsMerged,
                row.Value.PullRequestsClosed,
                row.Value.WorkflowRuns,
                row.Value.WorkflowFailures,
                row.Value.Views,
                row.Value.UniqueViews,
                row.Value.Clones,
                row.Value.UniqueClones
            });
        }
    }

    private async Task SaveUserStatsAsync(
        long repoGitHubId,
        Dictionary<(DateTime Date, string Login), UserDayStats> stats)
    {
        await using var con = new SqlConnection(_connectionString);

        const string sql = """
        MERGE dbo.GitHubDailyUserStats AS T
        USING (
            SELECT
                @StatDate AS StatDate,
                @RepoGitHubId AS RepoGitHubId,
                @GitHubLogin AS GitHubLogin
        ) AS S
        ON T.StatDate = S.StatDate
           AND T.RepoGitHubId = S.RepoGitHubId
           AND T.GitHubLogin = S.GitHubLogin
        WHEN MATCHED THEN UPDATE SET
            Commits = @Commits,
            PullRequestsOpened = @PullRequestsOpened,
            PullRequestsMerged = @PullRequestsMerged,
            WorkflowRuns = @WorkflowRuns,
            WorkflowFailures = @WorkflowFailures,
            CalculatedAt = SYSUTCDATETIME()
        WHEN NOT MATCHED THEN INSERT
            (StatDate, RepoGitHubId, GitHubLogin, Commits, PullRequestsOpened,
             PullRequestsMerged, WorkflowRuns, WorkflowFailures, CalculatedAt)
        VALUES
            (@StatDate, @RepoGitHubId, @GitHubLogin, @Commits, @PullRequestsOpened,
             @PullRequestsMerged, @WorkflowRuns, @WorkflowFailures, SYSUTCDATETIME());
        """;

        foreach (var row in stats)
        {
            await con.ExecuteAsync(sql, new
            {
                StatDate = row.Key.Date.Date,
                RepoGitHubId = repoGitHubId,
                GitHubLogin = row.Key.Login,
                row.Value.Commits,
                row.Value.PullRequestsOpened,
                row.Value.PullRequestsMerged,
                row.Value.WorkflowRuns,
                row.Value.WorkflowFailures
            });
        }
    }

    private async Task LogAsync(
        string syncType,
        string? repoFullName,
        string status,
        string? message,
        int? recordsAffected)
    {
        await using var con = new SqlConnection(_connectionString);

        await con.ExecuteAsync("""
        INSERT INTO dbo.GitHubSyncLog
            (SyncType, RepoFullName, FinishedAt, Status, Message, RecordsAffected)
        VALUES
            (@SyncType, @RepoFullName, SYSUTCDATETIME(), @Status, @Message, @RecordsAffected);
        """, new
        {
            SyncType = syncType,
            RepoFullName = repoFullName,
            Status = status,
            Message = message,
            RecordsAffected = recordsAffected
        });
    }

    private static RepoDayStats GetRepoDay(Dictionary<DateTime, RepoDayStats> dict, DateTime date)
    {
        date = date.Date;

        if (!dict.TryGetValue(date, out var stats))
        {
            stats = new RepoDayStats();
            dict[date] = stats;
        }

        return stats;
    }

    private static UserDayStats GetUserDay(
        Dictionary<(DateTime Date, string Login), UserDayStats> dict,
        DateTime date,
        string login)
    {
        var key = (date.Date, login);

        if (!dict.TryGetValue(key, out var stats))
        {
            stats = new UserDayStats();
            dict[key] = stats;
        }

        return stats;
    }

    private static DateTime? GetCommitDate(JsonElement commit)
    {
        if (!commit.TryGetProperty("commit", out var commitObj))
            return null;

        if (!commitObj.TryGetProperty("author", out var authorObj))
            return null;

        if (!authorObj.TryGetProperty("date", out var date))
            return null;

        return date.ValueKind == JsonValueKind.String && date.TryGetDateTime(out var dt)
            ? dt
            : null;
    }

    private static string? GetCommitAuthorLogin(JsonElement commit)
    {
        if (commit.TryGetProperty("author", out var author) &&
            author.ValueKind == JsonValueKind.Object &&
            author.TryGetProperty("login", out var login) &&
            login.ValueKind == JsonValueKind.String)
        {
            return login.GetString();
        }

        if (commit.TryGetProperty("commit", out var commitObj) &&
            commitObj.TryGetProperty("author", out var authorObj) &&
            authorObj.TryGetProperty("name", out var name) &&
            name.ValueKind == JsonValueKind.String)
        {
            return name.GetString();
        }

        return null;
    }

    private static string? GetNestedLogin(JsonElement item, string propertyName)
    {
        if (item.TryGetProperty(propertyName, out var nested) &&
            nested.ValueKind == JsonValueKind.Object &&
            nested.TryGetProperty("login", out var login) &&
            login.ValueKind == JsonValueKind.String)
        {
            return login.GetString();
        }

        return null;
    }

    private static string? TryGetNullableString(JsonElement item, string property)
    {
        return item.TryGetProperty(property, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool TryGetBool(JsonElement item, string property)
    {
        return item.TryGetProperty(property, out var value) &&
               value.ValueKind == JsonValueKind.True;
    }

    private static int TryGetInt(JsonElement item, string property)
    {
        return item.TryGetProperty(property, out var value) &&
               value.ValueKind == JsonValueKind.Number &&
               value.TryGetInt32(out var i)
            ? i
            : 0;
    }

    private static DateTime? TryGetNullableDateTime(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value))
            return null;

        if (value.ValueKind != JsonValueKind.String)
            return null;

        return value.TryGetDateTime(out var dt) ? dt : null;
    }

    private sealed class GitHubRepoDto
    {
        public long GitHubId { get; set; }
        public string OwnerName { get; set; } = "";
        public string RepoName { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? HtmlUrl { get; set; }
        public string? DefaultBranch { get; set; }
        public string? Language { get; set; }
        public bool IsPrivate { get; set; }
        public bool IsArchived { get; set; }
        public DateTime? LastPushedAt { get; set; }
    }

    private sealed class RepoDayStats
    {
        public int Commits { get; set; }
        public int PullRequestsOpened { get; set; }
        public int PullRequestsMerged { get; set; }
        public int PullRequestsClosed { get; set; }
        public int WorkflowRuns { get; set; }
        public int WorkflowFailures { get; set; }
        public int Views { get; set; }
        public int UniqueViews { get; set; }
        public int Clones { get; set; }
        public int UniqueClones { get; set; }
    }

    private sealed class UserDayStats
    {
        public int Commits { get; set; }
        public int PullRequestsOpened { get; set; }
        public int PullRequestsMerged { get; set; }
        public int WorkflowRuns { get; set; }
        public int WorkflowFailures { get; set; }
    }
}