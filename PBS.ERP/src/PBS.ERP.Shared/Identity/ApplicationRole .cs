using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace PBS.ERP.Shared.Identity
{
    [Table(Models.Constants.RoleTable)]
    public class ApplicationRole : IdentityRole
    {
        // Optional business key
        [Required]
        [StringLength(100)]
        public string UID { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [StringLength(200)]
        public string Description { get; set; } = string.Empty;

        public bool IsSystemRole { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
        public bool IsDefault { get; set; } = false;

        public int PriorityLevel { get; set; } = 0;
        public string? ParentRoleId { get; set; }
        [ForeignKey(nameof(ParentRoleId))]
        public virtual ApplicationRole? ParentRole { get; set; }

        [StringLength(100)]
        public string? CreatedBy { get; set; }
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedTime { get; set; }

        [StringLength(100)]
        public string? DeletedBy { get; set; }
        public DateTime? DeletedTime { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;
    }
}