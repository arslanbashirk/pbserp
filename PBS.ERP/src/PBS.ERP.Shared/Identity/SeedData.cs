using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PBS.ERP.Shared.Identity
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            // Get the RoleManager & UserManager from DI
            var roleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Define roles
            var roles = new List<(string Name, int Priority, bool IsSystem)>
            {
                (RoleNames.Root, 99, true),
                (RoleNames.Super, 95, true),
                (RoleNames.Admin, 90, true),
                (RoleNames.Manager, 80, false),
                (RoleNames.User, 0, false)
            };

            foreach (var (Name, Priority, IsSystem) in roles)
            {
                if (!await roleManager.RoleExistsAsync(Name))
                {
                    var role = new ApplicationRole
                    {
                        Name = Name,
                        Description = $"{Name} role",
                        CreatedTime = DateTime.UtcNow,
                        PriorityLevel = Priority,
                        IsSystemRole = IsSystem,
                        IsActive = true,
                        IsDeleted = false
                    };

                    await roleManager.CreateAsync(role);
                }
            }

            // --------------------------------–
            // 2. Create Admin User
            // --------------------------------–
            string adminEmail = configuration["SeedUsers:AdminEmail"] ?? "admin@pbs.gov.pk";
            string adminPassword = configuration["SeedUsers:AdminPassword"] ?? "GreaterERP2026";

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    Name = "Administrator",
                    UID = Guid.NewGuid().ToString(),
                    CreatedTime = DateTime.UtcNow,
                    IsActive = true,
                    IsDeleted = false
                };

                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (!result.Succeeded)
                {
                    throw new Exception(
                        $"Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }

                // Assign roles to admin
                await userManager.AddToRolesAsync(adminUser, new[] { "Root", "Admin" });
            }

            // --------------------------------–
            // 3. Create Test User
            // --------------------------------–
            string testEmail = configuration["SeedUsers:TestEmail"] ?? "user@pbs.gov.pk";
            string testPassword = configuration["SeedUsers:TestPassword"] ?? "User@12345";

            var testUser = await userManager.FindByEmailAsync(testEmail);
            if (testUser == null)
            {
                testUser = new ApplicationUser
                {
                    UserName = testEmail,
                    Email = testEmail,
                    EmailConfirmed = true,
                    Name = "Test User",
                    UID = Guid.NewGuid().ToString(),
                    CreatedTime = DateTime.UtcNow,
                    IsActive = true,
                    IsDeleted = false
                };

                await userManager.CreateAsync(testUser, testPassword);
                await userManager.AddToRoleAsync(testUser, "User");
            }

            Console.WriteLine("SeedData: Roles and Users created successfully.");
        }
    }
}