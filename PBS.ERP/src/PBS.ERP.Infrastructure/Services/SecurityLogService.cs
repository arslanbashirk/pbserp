using Microsoft.AspNetCore.Http;
using PBS.ERP.Shared.Identity;

namespace PBS.ERP.Infrastructure.Services
{
    public class SecurityLogService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SecurityLogService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        // Generic log
        public async Task LogAsync(string eventType, string? userId = null, string usernameAttempted = "")
        {
            var http = _httpContextAccessor.HttpContext;

            var log = new SecurityLog
            {
                UserId = userId, // <-- just pass null
                EventType = eventType,
                UsernameAttempted = usernameAttempted,
                IpAddress = http?.Connection?.RemoteIpAddress?.ToString() ?? string.Empty,
                Device = GetDevice(http),
                OS = GetOS(http),
                Browser = GetBrowser(http),
                Origin = http?.Request?.Headers["Referer"].ToString() ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            _context.SecurityLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        // Convenience methods
        public Task LogLoginAttempt(string username, bool success, string? userId = null)
            => LogAsync(success ? "LoginSuccess" : "LoginFailed", userId, username);

        public Task LogLogout(string? userId)
            => LogAsync("Logout", userId);

        public Task LogPasswordReset(string? userId)
            => LogAsync("PasswordReset", userId);

        public Task LogRoleChange(string? userId)
            => LogAsync("RoleChange", userId);

        // Device detection
        private string GetDevice(HttpContext? http)
        {
            var ua = http?.Request?.Headers["User-Agent"].ToString() ?? "";
            if (ua.Contains("Mobile")) return "Mobile";
            if (ua.Contains("Tablet")) return "Tablet";
            return "Desktop";
        }

        // OS detection
        private string GetOS(HttpContext? http)
        {
            var ua = http?.Request?.Headers["User-Agent"].ToString() ?? "";
            if (ua.Contains("Windows")) return "Windows";
            if (ua.Contains("Macintosh")) return "macOS";
            if (ua.Contains("Android")) return "Android";
            if (ua.Contains("iPhone") || ua.Contains("iPad")) return "iOS";
            return "Unknown";
        }

        // Browser detection
        private string GetBrowser(HttpContext? http)
        {
            var ua = http?.Request?.Headers["User-Agent"].ToString() ?? "";
            if (ua.Contains("Chrome")) return "Chrome";
            if (ua.Contains("Firefox")) return "Firefox";
            if (ua.Contains("Edge")) return "Edge";
            if (ua.Contains("Safari") && !ua.Contains("Chrome")) return "Safari";
            return "Unknown";
        }
    }
}
