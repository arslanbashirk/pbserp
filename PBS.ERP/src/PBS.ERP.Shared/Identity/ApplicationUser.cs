using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace PBS.ERP.Shared.Identity
{
    public class ApplicationUser : IdentityUser
    {

        [Required]
        [MaxLength(100)]
        public string UID { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Gender { get; set; }

        [MaxLength(15)]
        public string? CNIC { get; set; }

        [MaxLength(15)]
        public string? PNO { get; set; }

        [MaxLength(15)]
        public string? Mobile { get; set; }


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
    }
}
