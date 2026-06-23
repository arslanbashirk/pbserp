using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using PBS.ERP.Infrastructure;
using PBS.ERP.Infrastructure.Services;
using PBS.ERP.Shared.Identity;
using PBS.ERP.Modules.Core.Services;
using Microsoft.Extensions.FileProviders;
using PBS.ERP.Infrastructure.Interfaces;
using PBS.ERP.Modules.Survey.Services;

namespace PBS.ERP
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // =====================================================
            // DATABASE
            // =====================================================

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            // =====================================================
            // MVC + MODULES + JSON SETTINGS
            // =====================================================
            // Preserved all your modules/application parts.
            // Consolidated duplicate AddControllersWithViews/AddControllers calls safely.


            // =====================================================
            // MVC + MODULES + JSON SETTINGS
            // =====================================================

            var mvc = builder.Services
                .AddControllersWithViews()
                .AddApplicationPart(typeof(Modules.Api.ModuleMarker).Assembly)
                .AddApplicationPart(typeof(Modules.Security.ModuleMarker).Assembly)
                .AddApplicationPart(typeof(Modules.Core.ModuleMarker).Assembly)
                .AddApplicationPart(typeof(Modules.Portal.ModuleMarker).Assembly)
                .AddApplicationPart(typeof(Modules.HR.ModuleMarker).Assembly)
                .AddApplicationPart(typeof(Modules.Inventory.ModuleMarker).Assembly)
                .AddApplicationPart(typeof(Modules.Survey.ModuleMarker).Assembly)
                .AddApplicationPart(typeof(Modules.Subject.ModuleMarker).Assembly)
                .AddApplicationPart(typeof(Modules.Staff.ModuleMarker).Assembly)
                .AddApplicationPart(typeof(Modules.Training.ModuleMarker).Assembly)
                .AddApplicationPart(typeof(Modules.Monitoring.ModuleMarker).Assembly)
                .AddApplicationPart(typeof(Modules.Frame.ModuleMarker).Assembly)
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = null;
                    options.JsonSerializerOptions.DictionaryKeyPolicy = null;
                });

            if (builder.Environment.IsDevelopment())
            {
                mvc.AddRazorRuntimeCompilation(options =>
                {
                    AddModuleViewProvider(options, builder.Environment.ContentRootPath, "PBS.ERP.Modules.Api");
                    AddModuleViewProvider(options, builder.Environment.ContentRootPath, "PBS.ERP.Modules.Security");
                    AddModuleViewProvider(options, builder.Environment.ContentRootPath, "PBS.ERP.Modules.Core");
                    AddModuleViewProvider(options, builder.Environment.ContentRootPath, "PBS.ERP.Modules.Portal");
                    AddModuleViewProvider(options, builder.Environment.ContentRootPath, "PBS.ERP.Modules.HR");
                    AddModuleViewProvider(options, builder.Environment.ContentRootPath, "PBS.ERP.Modules.Inventory");
                    AddModuleViewProvider(options, builder.Environment.ContentRootPath, "PBS.ERP.Modules.Survey");
                    AddModuleViewProvider(options, builder.Environment.ContentRootPath, "PBS.ERP.Modules.Subject");
                    AddModuleViewProvider(options, builder.Environment.ContentRootPath, "PBS.ERP.Modules.Staff");
                    AddModuleViewProvider(options, builder.Environment.ContentRootPath, "PBS.ERP.Modules.Training");
                    AddModuleViewProvider(options, builder.Environment.ContentRootPath, "PBS.ERP.Modules.Monitoring");
                    AddModuleViewProvider(options, builder.Environment.ContentRootPath, "PBS.ERP.Modules.Frame");
                });
            }

            builder.Services.AddRazorPages();

            // =====================================================
            // IDENTITY - COOKIE BASED MVC LOGIN
            // =====================================================

            builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;

                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 8;

                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;

                // This matches your registration logic where email is checked manually.
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            // =====================================================
            // COOKIE SETTINGS FOR MVC
            // =====================================================

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";

                options.Cookie.Name = "PBS.ERP.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;

                // In production, cookies should only go through HTTPS.
                // In development, SameAsRequest avoids breaking local HTTP testing.
                options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;

                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);

                // API should receive 401/403 instead of redirecting to HTML login page.
                options.Events.OnRedirectToLogin = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
            });

            // =====================================================
            // JWT SETTINGS FOR API LOGIN / REGISTER
            // =====================================================
            // This does NOT remove cookie authentication.
            // MVC remains cookie based.
            // API can use JWT Bearer tokens.

            var jwtSection = builder.Configuration.GetSection("Jwt");

            var jwtKey = jwtSection["Key"];
            var jwtIssuer = jwtSection["Issuer"];
            var jwtAudience = jwtSection["Audience"];

            if (string.IsNullOrWhiteSpace(jwtKey))
            {
                throw new InvalidOperationException("Jwt:Key is missing in appsettings.json.");
            }

            if (jwtKey.Length < 32)
            {
                throw new InvalidOperationException("Jwt:Key must be at least 32 characters long.");
            }

            if (string.IsNullOrWhiteSpace(jwtIssuer))
            {
                throw new InvalidOperationException("Jwt:Issuer is missing in appsettings.json.");
            }

            if (string.IsNullOrWhiteSpace(jwtAudience))
            {
                throw new InvalidOperationException("Jwt:Audience is missing in appsettings.json.");
            }

            builder.Services
                .AddAuthentication()
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        var jwt = builder.Configuration.GetSection("Jwt");

        var key = jwt["Key"]
            ?? throw new InvalidOperationException("Jwt:Key is missing.");

        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],

            ValidateAudience = true,
            ValidAudience = jwt["Audience"],

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(key)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),

            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
    });
            // =====================================================
            // AUTHORIZATION POLICIES
            // =====================================================

            builder.Services.AddAuthorization(options =>
            {
                // Use this on protected API controllers/actions:
                // [Authorize(Policy = "ApiJwt")]
                options.AddPolicy("ApiJwt", policy =>
                {
                    policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
                    policy.RequireAuthenticatedUser();
                });

                // Optional helpful policies. These do not break existing [Authorize].
                options.AddPolicy("AdminOrSuperOrRoot", policy =>
                {
                    policy.RequireRole("Admin", "Super", "Root");
                });

                options.AddPolicy("StaffOnly", policy =>
                {
                    policy.RequireRole("Staff");
                });

                options.AddPolicy("ManagerOnly", policy =>
                {
                    policy.RequireRole("Manager");
                });
            });

            // =====================================================
            // SWAGGER
            // =====================================================
            // Your Swagger was registered multiple times.
            // This single block keeps:
            // 1. SwaggerDoc title/version
            // 2. XML comments
            // 3. JWT Bearer Authorize button

            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "ERP / Survey Solution",
                    Version = "1.0"
                });

                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath);
                }

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Description = "Enter JWT token only. Do not write Bearer manually.",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // =====================================================
            // EXISTING SERVICES
            // =====================================================

            builder.Services.AddScoped<SecurityLogService>();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<IMetadata, MetadataService>();
            builder.Services.AddScoped<ISuperInterface, TableService>();
            builder.Services.AddScoped<IDbInterface, DbService>();
            builder.Services.AddScoped<IConnectionService, ConnectionService>();
            builder.Services.AddScoped<IDatabaseService, DatabaseService>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IDbConnection>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                return new SqlConnection(config.GetConnectionString("DefaultConnection"));
            });

            // Keep these commented services as you had them.
            // builder.Services.AddSingleton<TemplateHelper>();
            // builder.Services.AddScoped<IMetadata, MetadataService>();

            // builder.Services.Configure<BackupSettings>(
            //     builder.Configuration.GetSection("BackupSettings"));
            // builder.Services.AddScoped<DatabaseBackupService>();

            var app = builder.Build();

            // =====================================================
            // SEED DATA
            // =====================================================

            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var configuration = services.GetRequiredService<IConfiguration>();

                await SeedData.InitializeAsync(services, configuration);
            }

            // =====================================================
            // MIDDLEWARE
            // =====================================================

            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStatusCodePagesWithReExecute("/Error/{0}");

            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            // =====================================================
            // SWAGGER
            // =====================================================
            // Swagger JSON stays public.
            // Swagger UI stays protected by Admin/Super/Root.

            app.UseSwagger();

            app.UseWhen(context => context.Request.Path.StartsWithSegments("/swagger"), appBuilder =>
            {
                appBuilder.Use(async (context, next) =>
                {
                    // Allow Swagger JSON endpoint publicly.
                    if (context.Request.Path.StartsWithSegments("/swagger/v1/swagger.json"))
                    {
                        await next();
                        return;
                    }

                    // Protect Swagger UI.
                    if (context.User?.Identity?.IsAuthenticated != true ||
                        !(context.User.IsInRole("Admin") ||
                          context.User.IsInRole("Super") ||
                          context.User.IsInRole("Root")))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return;
                    }

                    await next();
                });
            });

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "ERP End Points API");
                c.RoutePrefix = "swagger";
                c.DocumentTitle = "ERP / Survey Solution API";
            });

            // =====================================================
            // ROUTES
            // =====================================================
            // MapControllers is important for attribute routes like:
            // [Route("api/authenticate")]
            // [Route("api/test")]
            // [Route("api/auth/login")]

            app.MapControllers();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.MapRazorPages();

            await app.RunAsync();
        }

        private static void AddModuleViewProvider(
        Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation.MvcRazorRuntimeCompilationOptions options,
        string webRootPath,
        string moduleProjectName)
        {
            var modulePath = Path.GetFullPath(
                Path.Combine(webRootPath, "..", moduleProjectName));

            if (Directory.Exists(modulePath))
            {
                options.FileProviders.Add(new PhysicalFileProvider(modulePath));
            }
        }
    }
}