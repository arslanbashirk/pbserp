using PBS.ERP.Shared.Identity;

namespace PBS.ERP.Modules.Core.Model.Identity
{
    public class AuditLog
    {
        public int Id { get; set; }

        // Link to the user who performed the action
        public string UserId { get; set; }
        public virtual ApplicationUser User { get; set; }

        // Table and record info
        public string TableName { get; set; } = string.Empty; // e.g., "AspNetUsers"
        public string RecordId { get; set; } = string.Empty;  // Primary key of affected record
        public string Action { get; set; } = string.Empty;    // Insert / Update / Delete

        // Old and new values (store as JSON for structured data)
        public string OldValue { get; set; }
        public string NewValue { get; set; }

        // Optional: track source of change
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
