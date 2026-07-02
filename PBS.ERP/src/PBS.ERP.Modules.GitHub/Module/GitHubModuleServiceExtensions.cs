using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PBS.ERP.Modules.GitHub.Models;
using PBS.ERP.Modules.GitHub.Services;

namespace PBS.ERP.Modules.GitHub.Module;

public static class GitHubModuleServiceExtensions
{
    public static IServiceCollection AddGitHubModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GitHubOptions>(
            configuration.GetSection("GitHub"));

        services.AddHttpClient("GitHubRaw");

        services.AddScoped<GitHubAuthService>();
        services.AddScoped<GitHubApiClient>();
        services.AddScoped<GitHubStatsSyncService>();

        return services;
    }
}