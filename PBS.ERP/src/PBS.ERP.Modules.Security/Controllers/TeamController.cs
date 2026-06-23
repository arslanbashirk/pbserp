using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PBS.ERP.Infrastructure;
using PBS.ERP.Shared.Identity; // For EF queries

namespace PBS.ERP.Modules.Security.Controllers
{
    [Authorize(Roles = "Root,Super,Admin")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("Team")]
    public class TeamController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public TeamController(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
        {
            _userManager = userManager;
            _context = db;
        }
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var connString = _context.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(connString))
                return StatusCode(500, "Database connection could not be established.");
            ViewBag.CurrentUser = User.Identity?.Name;
            using (var conn = new SqlConnection(connString))
            {
                string sql = @"
                SELECT u.UserName, s.Name, 
                (select Description from ERPHR.dbo.Designation where code=s.DesignationId) as Designation
                ,CASE WHEN u.[UID] is null then 0 else 1 end as HasAccount
                ,(SELECT COUNT(*) 
                     FROM ERPCORE.dbo.SYS_SECURITY_LOG l
                     WHERE l.UserId = u.Id AND l.EventType = 'LoginSuccess') as TimesLogin
                ,(SELECT Max(CreatedAt) 
                FROM ERPCORE.dbo.SYS_SECURITY_LOG l
                WHERE l.UserId = u.Id AND l.EventType = 'LoginSuccess') as LastLogin
                ,(SELECT Count(distinct(e.UID))
                FROM ERPCORE.dbo.ENTITY e
                WHERE e.CreatedBy = u.UserName AND e.IsDeleted=0) as CreatedEntities
                ,(SELECT Count(distinct(f.UID))
                FROM ERPCORE.dbo.FIELD f
                WHERE f.CreatedBy = u.UserName AND f.IsDeleted=0) as CreatedFields
                ,(
                    SELECT MAX(dt) FROM (
                        SELECT MAX(f.CreatedTime) AS dt
                        FROM ERPCORE.dbo.Field f
                        WHERE f.CreatedBy = u.UserName AND f.IsDeleted = 0
                        UNION ALL
                        SELECT MAX(f.ModifiedTime) AS dt
                        FROM ERPCORE.dbo.Field f
                        WHERE f.CreatedBy = u.UserName AND f.IsDeleted = 0
                    ) AS sub
                ) AS  LastActivity
                from ERPHR.dbo.Staff s	LEFT OUTER JOIN ERPCORE.dbo.SYS_USER u
                ON(u.UID=s.[User])
                WHERE s.IsTeamMember=1
                ORDER BY s.Scale Desc,AppointDate, Name; ";

                var activities = (await conn.QueryAsync<TeamActivity>(sql)).ToList();
                return View(activities);
            }
        }

        [HttpGet("Module")]
        public async Task<IActionResult> Module(string module, string desc)
        {
            var ehr = await _context.Entities
           .Where(e => e.Type == module && !e.IsDeleted)
           .ToListAsync();
            ViewBag.Description = desc;
            ViewBag.Module = module;
            return View("~/Views/Team/Member.cshtml", ehr);
        }

        [HttpPost("Member")]
        public async Task<IActionResult> Member(string username, string name, string designation)
        {
            var ehr = await _context.Entities
           .Where(e => e.CreatedBy == username && !e.IsDeleted)
           .ToListAsync();

            var fieldActivity = await GetFieldActivityByUserAsync(username);

            ViewBag.Description = name + " (" + designation + ")";
            ViewBag.Module = "Team";
            ViewBag.FieldActivity = fieldActivity;

            return View("~/Views/Team/Member.cshtml",ehr);
        }

        public async Task<List<FieldUserActivityDto>> GetFieldActivityByUserAsync(
            string userName,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            const string sql = @"
                WITH CreatedData AS
                (
                    SELECT 
                        CAST(CreatedTime AS date) AS ActivityDate,
                        COUNT(*) AS CreatedCount,
                        0 AS ModifiedCount
                    FROM Field
                    WHERE CreatedBy = @UserName AND isDeleted=0 AND SectionNumber BETWEEN 1 AND 9
                      AND (@FromDate IS NULL OR CreatedTime >= @FromDate)
                      AND (@ToDate IS NULL OR CreatedTime < DATEADD(day, 1, @ToDate))
                    GROUP BY CAST(CreatedTime AS date)
                ),
                ModifiedData AS
                (
                    SELECT 
                        CAST(ModifiedTime AS date) AS ActivityDate,
                        0 AS CreatedCount,
                        COUNT(*) AS ModifiedCount
                    FROM Field
                    WHERE ModifiedBy = @UserName AND isDeleted=0 
                      AND ModifiedTime IS NOT NULL
                      AND (@FromDate IS NULL OR ModifiedTime >= @FromDate)
                      AND (@ToDate IS NULL OR ModifiedTime < DATEADD(day, 1, @ToDate))
                    GROUP BY CAST(ModifiedTime AS date)
                )
                SELECT 
                    ActivityDate,
                    SUM(CreatedCount) AS CreatedCount,
                    SUM(ModifiedCount) AS ModifiedCount
                FROM
                (
                    SELECT * FROM CreatedData
                    UNION ALL
                    SELECT * FROM ModifiedData
                ) x
                GROUP BY ActivityDate
                ORDER BY ActivityDate;
            ";

            await using var con = new SqlConnection(_context.Database.GetConnectionString());

            await con.OpenAsync();

            var result = await con.QueryAsync<FieldUserActivityDto>(sql, new
            {
                UserName = userName,
                FromDate = fromDate,
                ToDate = toDate
            });

            return result.ToList();
        }


        [HttpGet("Access")]
        public async Task<IActionResult> Access()
        {
            var connString = _context.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(connString))
                return StatusCode(500, "Database connection could not be established.");

            await using var conn = new SqlConnection(connString);

            string sql = @"
DECLARE @FromDate DATETIME = DATEADD(DAY, -30, GETDATE());

-- 1. KPI Summary
SELECT
    (SELECT COUNT(*) FROM ERPCORE.dbo.SYS_USER WHERE IsDeleted = 0) AS TotalUsers,
    (SELECT COUNT(*) FROM ERPCORE.dbo.SYS_USER WHERE IsActive = 1 AND IsDeleted = 0) AS ActiveUsers,
    (SELECT COUNT(*) FROM ERPCORE.dbo.SYS_USER WHERE IsActive = 0 AND IsDeleted = 0) AS InactiveUsers,
    (SELECT COUNT(*) FROM ERPCORE.dbo.SYS_USER WHERE IsDeleted = 1) AS DeletedUsers,

    (SELECT COUNT(*) FROM ERPCORE.dbo.SYS_ROLE WHERE IsDeleted = 0) AS TotalRoles,
    (SELECT COUNT(*) FROM ERPCORE.dbo.SYS_ROLE WHERE IsSystemRole = 1 AND IsDeleted = 0) AS SystemRoles,
    (SELECT COUNT(*) FROM ERPCORE.dbo.SYS_ROLE WHERE IsDefault = 1 AND IsDeleted = 0) AS DefaultRoles,

    (SELECT COUNT(*) FROM ERPCORE.dbo.SYS_USER_ROLE WHERE IsActive = 1 AND IsDeleted = 0) AS ActiveRoleMappings,

    (SELECT COUNT(*) FROM ERPCORE.dbo.SYS_SECURITY_LOG WHERE EventType = 'LoginSuccess') AS TotalSuccessfulLogins,
    (SELECT COUNT(*) FROM ERPCORE.dbo.SYS_SECURITY_LOG WHERE EventType = 'LoginFailed') AS TotalFailedLogins,
    (SELECT COUNT(*) FROM ERPCORE.dbo.SYS_SECURITY_LOG) AS TotalSecurityEvents,

    (SELECT COUNT(DISTINCT UserId) FROM ERPCORE.dbo.SYS_SECURITY_LOG WHERE EventType = 'LoginSuccess') AS UsersEverLoggedIn,
    (SELECT COUNT(*) FROM ERPCORE.dbo.SYS_USER u 
        WHERE u.IsDeleted = 0 
        AND NOT EXISTS (
            SELECT 1 FROM ERPCORE.dbo.SYS_SECURITY_LOG l 
            WHERE l.UserId = u.Id AND l.EventType = 'LoginSuccess'
        )
    ) AS NeverLoggedInUsers,

    (SELECT MAX(CreatedAt) FROM ERPCORE.dbo.SYS_SECURITY_LOG WHERE EventType = 'LoginSuccess') AS LastLoginTime;

-- 2. Account creation trend
SELECT 
    CAST(CreatedTime AS DATE) AS [Date],
    COUNT(*) AS Total
FROM ERPCORE.dbo.SYS_USER
WHERE CreatedTime >= @FromDate
GROUP BY CAST(CreatedTime AS DATE)
ORDER BY [Date];

-- 3. Login trend
SELECT 
    CAST(CreatedAt AS DATE) AS [Date],
    SUM(CASE WHEN EventType = 'LoginSuccess' THEN 1 ELSE 0 END) AS SuccessCount,
    SUM(CASE WHEN EventType = 'LoginFailed' THEN 1 ELSE 0 END) AS FailedCount,
    COUNT(*) AS TotalEvents
FROM ERPCORE.dbo.SYS_SECURITY_LOG
WHERE CreatedAt >= @FromDate
GROUP BY CAST(CreatedAt AS DATE)
ORDER BY [Date];

-- 4. Role distribution
SELECT 
    r.Name AS RoleName,
    r.Description,
    r.PriorityLevel,
    COUNT(ur.UserId) AS UserCount
FROM ERPCORE.dbo.SYS_ROLE r
LEFT JOIN ERPCORE.dbo.SYS_USER_ROLE ur 
    ON ur.RoleId = r.Id 
    AND ur.IsActive = 1 
    AND ur.IsDeleted = 0
WHERE r.IsDeleted = 0
GROUP BY r.Name, r.Description, r.PriorityLevel
ORDER BY r.PriorityLevel DESC, UserCount DESC;

-- 5. Device analytics
SELECT 
    ISNULL(NULLIF(Device, ''), 'Unknown') AS Name,
    COUNT(*) AS Total
FROM ERPCORE.dbo.SYS_SECURITY_LOG
GROUP BY ISNULL(NULLIF(Device, ''), 'Unknown')
ORDER BY Total DESC;

-- 6. OS analytics
SELECT 
    ISNULL(NULLIF(OS, ''), 'Unknown') AS Name,
    COUNT(*) AS Total
FROM ERPCORE.dbo.SYS_SECURITY_LOG
GROUP BY ISNULL(NULLIF(OS, ''), 'Unknown')
ORDER BY Total DESC;

-- 7. Browser analytics
SELECT 
    ISNULL(NULLIF(Browser, ''), 'Unknown') AS Name,
    COUNT(*) AS Total
FROM ERPCORE.dbo.SYS_SECURITY_LOG
GROUP BY ISNULL(NULLIF(Browser, ''), 'Unknown')
ORDER BY Total DESC;

-- 8. Top failed usernames
SELECT TOP 10
    ISNULL(NULLIF(UsernameAttempted, ''), 'Unknown') AS UsernameAttempted,
    COUNT(*) AS FailedCount,
    MAX(CreatedAt) AS LastAttempt
FROM ERPCORE.dbo.SYS_SECURITY_LOG
WHERE EventType = 'LoginFailed'
GROUP BY ISNULL(NULLIF(UsernameAttempted, ''), 'Unknown')
ORDER BY FailedCount DESC;

-- 9. Top failed IPs
SELECT TOP 10
    ISNULL(NULLIF(IpAddress, ''), 'Unknown') AS IpAddress,
    COUNT(*) AS FailedCount,
    MAX(CreatedAt) AS LastAttempt
FROM ERPCORE.dbo.SYS_SECURITY_LOG
WHERE EventType = 'LoginFailed'
GROUP BY ISNULL(NULLIF(IpAddress, ''), 'Unknown')
ORDER BY FailedCount DESC;

-- 10. Top active users
SELECT TOP 10
    u.UserName,
    u.Name,
    COUNT(*) AS LoginCount,
    MAX(l.CreatedAt) AS LastLogin
FROM ERPCORE.dbo.SYS_SECURITY_LOG l
INNER JOIN ERPCORE.dbo.SYS_USER u ON u.Id = l.UserId
WHERE l.EventType = 'LoginSuccess'
GROUP BY u.UserName, u.Name
ORDER BY LoginCount DESC;

-- 11. Recent security events
SELECT TOP 100
    l.CreatedAt,
    ISNULL(u.UserName, l.UsernameAttempted) AS UserName,
    ISNULL(u.Name, '') AS Name,
    l.EventType,
    l.IpAddress,
    l.Device,
    l.OS,
    l.Browser,
    l.Origin
FROM ERPCORE.dbo.SYS_SECURITY_LOG l
LEFT JOIN ERPCORE.dbo.SYS_USER u 
    ON u.Id = l.UserId
ORDER BY l.CreatedAt DESC;

-- 12. Detailed users
;WITH RoleAgg AS
(
    SELECT
        ur.UserId,
        COUNT(DISTINCT ur.RoleId) AS RoleCount,
        ISNULL(
            STRING_AGG(CAST(ur.RoleName AS NVARCHAR(MAX)), ', '),
            'No Role'
        ) AS Roles
    FROM
    (
        SELECT DISTINCT
            ur.UserId,
            ur.RoleId,
            r.Name AS RoleName
        FROM ERPCORE.dbo.SYS_USER_ROLE ur
        INNER JOIN ERPCORE.dbo.SYS_ROLE r
            ON r.Id = ur.RoleId
            AND r.IsDeleted = 0
        WHERE ur.IsActive = 1
          AND ur.IsDeleted = 0
    ) ur
    GROUP BY ur.UserId
),
LoginAgg AS
(
    SELECT
        l.UserId,
        SUM(CASE WHEN l.EventType = 'LoginSuccess' THEN 1 ELSE 0 END) AS TotalLogins,
        SUM(CASE WHEN l.EventType = 'LoginFailed' THEN 1 ELSE 0 END) AS FailedLogins,
        MAX(CASE WHEN l.EventType = 'LoginSuccess' THEN l.CreatedAt END) AS LastLogin
    FROM ERPCORE.dbo.SYS_SECURITY_LOG l
    WHERE l.UserId IS NOT NULL
    GROUP BY l.UserId
)
SELECT  
    u.Id,
    u.UID,
    u.UserName,
    u.Name,
    u.Gender,
    u.CNIC,
    u.PNO,
    u.Mobile,
    u.Email,
    u.IsActive,
    u.IsDeleted,
    u.CreatedBy,
    u.CreatedTime,

    ISNULL(ra.RoleCount, 0) AS RoleCount,
    ISNULL(ra.Roles, 'No Role') AS Roles,

    ISNULL(la.TotalLogins, 0) AS TotalLogins,
    ISNULL(la.FailedLogins, 0) AS FailedLogins,
    la.LastLogin

FROM ERPCORE.dbo.SYS_USER u
LEFT JOIN RoleAgg ra ON ra.UserId = u.Id
LEFT JOIN LoginAgg la ON la.UserId = u.Id
WHERE u.IsDeleted = 0
ORDER BY u.CreatedTime DESC;
";

            using var multi = await conn.QueryMultipleAsync(sql);

            var model = new UserStatisticsVm
            {
                Summary = await multi.ReadFirstAsync<UserStatisticsSummaryVm>(),
                AccountCreationTrend = (await multi.ReadAsync<DateCountVm>()).ToList(),
                LoginTrend = (await multi.ReadAsync<LoginTrendVm>()).ToList(),
                RoleDistribution = (await multi.ReadAsync<RoleDistributionVm>()).ToList(),
                DeviceStats = (await multi.ReadAsync<NameCountVm>()).ToList(),
                OsStats = (await multi.ReadAsync<NameCountVm>()).ToList(),
                BrowserStats = (await multi.ReadAsync<NameCountVm>()).ToList(),
                TopFailedUsernames = (await multi.ReadAsync<TopFailedUsernameVm>()).ToList(),
                TopFailedIps = (await multi.ReadAsync<TopFailedIpVm>()).ToList(),
                TopActiveUsers = (await multi.ReadAsync<TopActiveUserVm>()).ToList(),
                RecentSecurityEvents = (await multi.ReadAsync<SecurityEventRowVm>()).ToList(),
                Users = (await multi.ReadAsync<UserStatisticsRowVm>()).ToList()
            };

            return View(model);
        }
    }





    public class TeamActivity
    {
        public string UserName { get; set; }
        public string Name { get; set; }
        public string Designation { get; set; }
        public bool HasAccount { get; set; }
        public int? TimesLogin { get; set; }
        public DateTime? LastLogin { get; set; }
        public int? CreatedEntities { get; set; }
        public int? CreatedFields { get; set; }
        public DateTime? LastActivity { get; set; }
    }
    public class UserStatisticsVm
    {
        public UserStatisticsSummaryVm Summary { get; set; } = new();
        public List<DateCountVm> AccountCreationTrend { get; set; } = new();
        public List<LoginTrendVm> LoginTrend { get; set; } = new();
        public List<RoleDistributionVm> RoleDistribution { get; set; } = new();
        public List<NameCountVm> DeviceStats { get; set; } = new();
        public List<NameCountVm> OsStats { get; set; } = new();
        public List<NameCountVm> BrowserStats { get; set; } = new();
        public List<TopFailedUsernameVm> TopFailedUsernames { get; set; } = new();
        public List<TopFailedIpVm> TopFailedIps { get; set; } = new();
        public List<TopActiveUserVm> TopActiveUsers { get; set; } = new();
        public List<SecurityEventRowVm> RecentSecurityEvents { get; set; } = new();
        public List<UserStatisticsRowVm> Users { get; set; } = new();
    }

    public class UserStatisticsSummaryVm
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }
        public int DeletedUsers { get; set; }
        public int TotalRoles { get; set; }
        public int SystemRoles { get; set; }
        public int DefaultRoles { get; set; }
        public int ActiveRoleMappings { get; set; }
        public int TotalSuccessfulLogins { get; set; }
        public int TotalFailedLogins { get; set; }
        public int TotalSecurityEvents { get; set; }
        public int UsersEverLoggedIn { get; set; }
        public int NeverLoggedInUsers { get; set; }
        public DateTime? LastLoginTime { get; set; }

        public decimal LoginSuccessRate =>
            TotalSecurityEvents == 0 ? 0 :
            Math.Round((decimal)TotalSuccessfulLogins / TotalSecurityEvents * 100, 1);
    }

    public class DateCountVm
    {
        public DateTime Date { get; set; }
        public int Total { get; set; }
    }

    public class LoginTrendVm
    {
        public DateTime Date { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public int TotalEvents { get; set; }
    }

    public class RoleDistributionVm
    {
        public string RoleName { get; set; } = "";
        public string Description { get; set; } = "";
        public int PriorityLevel { get; set; }
        public int UserCount { get; set; }
    }

    public class NameCountVm
    {
        public string Name { get; set; } = "";
        public int Total { get; set; }
    }

    public class TopFailedUsernameVm
    {
        public string UsernameAttempted { get; set; } = "";
        public int FailedCount { get; set; }
        public DateTime? LastAttempt { get; set; }
    }

    public class TopFailedIpVm
    {
        public string IpAddress { get; set; } = "";
        public int FailedCount { get; set; }
        public DateTime? LastAttempt { get; set; }
    }

    public class TopActiveUserVm
    {
        public string UserName { get; set; } = "";
        public string Name { get; set; } = "";
        public int LoginCount { get; set; }
        public DateTime? LastLogin { get; set; }
    }

    public class SecurityEventRowVm
    {
        public DateTime CreatedAt { get; set; }
        public string UserName { get; set; } = "";
        public string Name { get; set; } = "";
        public string EventType { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public string Device { get; set; } = "";
        public string OS { get; set; } = "";
        public string Browser { get; set; } = "";
        public string Origin { get; set; } = "";
    }

    public class UserStatisticsRowVm
    {
        public string Id { get; set; } = "";
        public string UID { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Name { get; set; } = "";
        public string Gender { get; set; } = "";
        public string CNIC { get; set; } = "";
        public string PNO { get; set; } = "";
        public string Mobile { get; set; } = "";
        public string Email { get; set; } = "";
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedTime { get; set; }
        public int RoleCount { get; set; }
        public string Roles { get; set; } = "";
        public int TotalLogins { get; set; }
        public int FailedLogins { get; set; }
        public DateTime? LastLogin { get; set; }
    }

    public class FieldUserActivityDto
    {
        public DateTime ActivityDate { get; set; }
        public int CreatedCount { get; set; }
        public int ModifiedCount { get; set; }
    }

}