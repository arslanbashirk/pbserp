namespace PBS.ERP.Modules.GitHub.Models;

public sealed class GitHubOptions
{
    public string Mode { get; set; } = "SingleRepository";

    public string Owner { get; set; } = "";
    public string Repository { get; set; } = "";

    public string Organization { get; set; } = "";

    public string AppId { get; set; } = "";
    public long InstallationId { get; set; }

    public string PrivateKeyPath { get; set; } = "";
    public string ApiBaseUrl { get; set; } = "https://api.github.com";
    public string ApiVersion { get; set; } = "2026-03-10";
}