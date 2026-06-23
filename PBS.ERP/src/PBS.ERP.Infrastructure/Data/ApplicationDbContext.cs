using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PBS.ERP.Modules.Core.Model.Identity;
using PBS.ERP.Shared.Models;
using PBS.ERP.Shared.Identity;
using PBS.ERP.Infrastructure.Tokens;

namespace PBS.ERP.Infrastructure
{
    public class ApplicationDbContext : IdentityDbContext<
        ApplicationUser,
        ApplicationRole,
        string,
        IdentityUserClaim<string>,
        ApplicationUserRole,
        IdentityUserLogin<string>,
        IdentityRoleClaim<string>,
        IdentityUserToken<string>>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // ======================
        // DbSets
        // ======================
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<SecurityLog> SecurityLogs { get; set; }
        public DbSet<Field> Fields { get; set; }
        public DbSet<Entity> Entities { get; set; }
        public DbSet<EntityPermission> EntityPermissions { get; set; }

        public DbSet<UserRefreshToken> UserRefreshTokens => Set<UserRefreshToken>();
        
        // ======================
        // SaveChanges override
        // ======================
        public override int SaveChanges()
        {
            UpdateEntityModifiedBy();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateEntityModifiedBy();
            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ----------------------
            // Identity table renames
            // ----------------------
            builder.Entity<ApplicationUser>(entity =>
            {
                entity.ToTable(Constants.UserTable);
                entity.HasMany<ApplicationUserRole>()
                      .WithOne()
                      .HasForeignKey(ur => ur.UserId)
                      .IsRequired();
            });

            builder.Entity<ApplicationRole>(entity =>
            {
                entity.ToTable(Constants.RoleTable);
                entity.HasMany<ApplicationUserRole>()
                      .WithOne()
                      .HasForeignKey(ur => ur.RoleId)
                      .IsRequired();
            });

            builder.Entity<ApplicationUserRole>(entity =>
            {
                entity.ToTable(Constants.UserRoleTable);
                entity.Property(u => u.UID).HasDefaultValueSql("NEWID()");
                entity.Property(u => u.IsActive).HasDefaultValue(true);
                entity.Property(u => u.IsDeleted).HasDefaultValue(false);
                entity.Property(u => u.CreatedTime).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(u => u.RowVersion).IsRowVersion();
            });

            builder.Entity<IdentityUserClaim<string>>().ToTable("SYS_USER_CLAIM");
            builder.Entity<IdentityUserLogin<string>>().ToTable("SYS_USER_LOGIN");
            builder.Entity<IdentityRoleClaim<string>>().ToTable("SYS_ROLE_CLAIM");
            builder.Entity<IdentityUserToken<string>>().ToTable("SYS_USER_TOKEN");

            // ----------------------
            // Custom application tables
            // ----------------------
            builder.Entity<AuditLog>(entity =>
            {
                entity.ToTable("SYS_AUDIT_LOG");
                entity.Property(a => a.TableName).HasMaxLength(50);
                entity.Property(a => a.RecordId).HasMaxLength(50);
                entity.Property(a => a.Action).HasMaxLength(20);
            });

            builder.Entity<SecurityLog>(entity =>
            {
                entity.ToTable("SYS_SECURITY_LOG");
                entity.Property(s => s.EventType).HasMaxLength(50);
                entity.Property(s => s.Device).HasMaxLength(50);
                entity.Property(s => s.OS).HasMaxLength(50);
                entity.Property(s => s.Browser).HasMaxLength(50);
            });

            builder.Entity<Field>(entity =>
            {
                entity.ToTable(Constants.FieldTable);
                entity.HasIndex(f => new { f.Entity, f.ColumnName, f.IsDeleted }).IsUnique();
            });

            builder.Entity<Entity>(entity =>
            {
                entity.ToTable(Constants.EntityTable);
                entity.HasIndex(e => new { e.Database, e.Schema, e.Name, e.IsDeleted }).IsUnique();

            });

            builder.Entity<EntityPermission>(entity =>
            {
                entity.ToTable(Constants.PermissionTable);

                // Default values for new permissions
                entity.Property(p => p.CanAccess).HasDefaultValue(true);
                entity.Property(p => p.CanSelect).HasDefaultValue(true);
                entity.Property(p => p.CanInsert).HasDefaultValue(true);
                entity.Property(p => p.CanUpdate).HasDefaultValue(true);
                entity.Property(p => p.CanDelete).HasDefaultValue(true);

                entity.Property(p => p.CanAlter).HasDefaultValue(true);
                entity.Property(p => p.CanDrop).HasDefaultValue(true);
                entity.Property(p => p.CanRename).HasDefaultValue(true);
                entity.Property(p => p.CanTruncate).HasDefaultValue(false);

                entity.Property(p => p.CanImport).HasDefaultValue(true);
                entity.Property(p => p.CanExport).HasDefaultValue(true);
                entity.Property(p => p.CanViewFields).HasDefaultValue(true);
                entity.Property(p => p.CanViewList).HasDefaultValue(true);
                entity.Property(p => p.CanViewDetail).HasDefaultValue(true);

                entity.Property(p => p.CanCustomizeColumns).HasDefaultValue(false);
                entity.Property(p => p.CanSaveViewLayout).HasDefaultValue(false);
                entity.Property(p => p.CanFilter).HasDefaultValue(true);
                entity.Property(p => p.CanUseGlobalSearch).HasDefaultValue(true);
                entity.Property(p => p.CanSort).HasDefaultValue(true);
            });

            builder.Entity<EntityPermission>()
            .HasOne(ep => ep.Entity)
            .WithMany()
            .HasForeignKey(ep => ep.EntityUID)
            .HasPrincipalKey(e => e.UID);


            
            builder.Entity<UserRefreshToken>(entity =>
            {
                entity.ToTable("SYS_USER_REFRESH_TOKEN");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.UserId)
                    .HasMaxLength(450)
                    .IsRequired();

                entity.Property(x => x.TokenHash)
                    .HasMaxLength(128)
                    .IsRequired();

                entity.Property(x => x.JwtId)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.CreatedByIp)
                    .HasMaxLength(100);

                entity.Property(x => x.RevokedByIp)
                    .HasMaxLength(100);

                entity.Property(x => x.ReplacedByTokenHash)
                    .HasMaxLength(128);

                entity.Property(x => x.ReasonRevoked)
                    .HasMaxLength(250);

                entity.HasIndex(x => x.UserId);
                entity.HasIndex(x => x.TokenHash).IsUnique();
                entity.HasIndex(x => x.ExpiresAtUtc);
                entity.HasIndex(x => x.RevokedAtUtc);

                entity.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            
        }

        // ======================
        // Automatic ModifiedBy update
        // ======================
        private void UpdateEntityModifiedBy()
        {
            try
            {
                var modifiedFields = ChangeTracker.Entries<Field>()
                    .Where(f => f.State == EntityState.Modified && f.Entity.Entity != null)
                    .ToList();

                if (!modifiedFields.Any())
                    return;

                var entityGroups = modifiedFields
                    .GroupBy(f => f.Entity.Entity)
                    .ToDictionary(g => g.Key, g => g.Last().Entity.ModifiedBy);

                var trackedEntities = ChangeTracker.Entries<Entity>()
                    .Where(e => entityGroups.ContainsKey(e.Entity.UID))
                    .ToDictionary(e => e.Entity.UID, e => e);

                foreach (var kvp in trackedEntities)
                {
                    try
                    {
                        kvp.Value.Entity.ModifiedBy = entityGroups[kvp.Key];
                        kvp.Value.State = EntityState.Modified;
                    }
                    catch { }

                    entityGroups.Remove(kvp.Key);
                }

                if (entityGroups.Any())
                {
                    var untrackedEntities = Entities
                        .Where(e => entityGroups.Keys.Contains(e.UID))
                        .ToList();

                    foreach (var entity in untrackedEntities)
                    {
                        try
                        {
                            entity.ModifiedBy = entityGroups[entity.UID];
                            Entry(entity).State = EntityState.Modified;
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        // ======================
        // Optional: Create default permissions for a new entity
        // ======================
        public async Task CreateDefaultPermissionsAsync(string entityUid)
        {
            if (string.IsNullOrEmpty(entityUid)) return;

            // Example: Admin role gets full permission
            var adminRole = await Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
            if (adminRole != null)
            {
                var permission = new EntityPermission
                {
                    EntityUID = entityUid,
                    RoleId = adminRole.Id,
                    CanAccess = true,
                    CanSelect = true,
                    CanInsert = true,
                    CanUpdate = true,
                    CanDelete = true,
                    CanAlter = true,
                    CanDrop = true,
                    CanRename = true
                };
                EntityPermissions.Add(permission);
            }

            // Example: Viewer role gets read-only permission
            var viewerRole = await Roles.FirstOrDefaultAsync(r => r.Name == "Viewer");
            if (viewerRole != null)
            {
                var permission = new EntityPermission
                {
                    EntityUID = entityUid,
                    RoleId = viewerRole.Id,
                    CanAccess = true,
                    CanSelect = true,
                    CanInsert = false,
                    CanUpdate = false,
                    CanDelete = false
                };
                EntityPermissions.Add(permission);
            }

            await SaveChangesAsync();
        }
    }
}