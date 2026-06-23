using PBS.ERP.Shared.Identity;

namespace PBS.ERP.Shared.Identity
{
    public class SecurityLog
    {
        public int Id { get; set; }

        // Optional: link to the user (null if login failed for unknown username)
        public string? UserId { get; set; }
        public virtual ApplicationUser? User { get; set; }

        // Event type: LoginSuccess, LoginFailed, Logout, PasswordReset, RoleChange
        public string EventType { get; set; } = string.Empty;

        // Client / request information
        public string IpAddress { get; set; } = string.Empty;
        public string Device { get; set; } = string.Empty;   // Desktop / Mobile / Tablet
        public string OS { get; set; } = string.Empty;       // Windows / macOS / Android / iOS
        public string Browser { get; set; } = string.Empty;  // Chrome, Edge, etc.
        public string Origin { get; set; } = string.Empty;   // Optional: referrer or calling app

        // Username attempted (never store passwords)
        public string UsernameAttempted { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
