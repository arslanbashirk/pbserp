using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace PBS.ERP.Shared.Models
{
    public abstract class AuditEntity
    {

        [Required]
        [StringLength(100)]
        [BindNever]
        public string UID { get; set; } = Guid.NewGuid().ToString();

        [StringLength(100)]
        public string? CreatedBy { get; set; }
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedTime { get; set; }
        [StringLength(100)]
        public string? DeletedBy { get; set; }
        public DateTime? DeletedTime { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
        [Timestamp]
        [BindNever]
        public byte[] RowVersion { get; set; } = null!;
    }

}
