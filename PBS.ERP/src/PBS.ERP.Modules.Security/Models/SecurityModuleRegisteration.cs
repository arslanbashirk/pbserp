using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PBS.ERP.Modules.Security.Services;

namespace PBS.ERP.Modules.Security.Models;

public static class SecurityModuleRegistration
{
    public static IServiceCollection AddSecurityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<DatabaseBackupService>();

        return services;
    }
}