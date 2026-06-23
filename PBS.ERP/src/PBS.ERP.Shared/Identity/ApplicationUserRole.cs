using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace PBS.ERP.Shared.Identity
{
    public class ApplicationUserRole : IdentityUserRole<string>
    {
        [Required]
        [StringLength(100)]
        public string UID { get; set; } = Guid.NewGuid().ToString();

        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;

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

        public virtual ApplicationUser? User { get; set; }
        public virtual ApplicationRole? Role { get; set; }
    }
}