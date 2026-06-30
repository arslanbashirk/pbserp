
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PBS.ERP.Infrastructure;
using PBS.ERP.Shared.Identity;
using PBS.ERP.Shared.Models;
using System.Data;
using static PBS.ERP.Shared.Models.SurveyModel;

namespace PBS.ERP.Modules.Subject.Controllers
{
    [Authorize]
    [Route("Subject")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize(
        AuthenticationSchemes = Constants.Identity_Application_Scheme,
        Roles = "Root,Super,Admin,Staff")]
    public class SubjectController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public SubjectController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpGet("Survey")]
        public async Task<IActionResult> All()
        {
            var surveys = new List<SurveyViewModel>();

            try
            {
                var currentUser = await _userManager.GetUserAsync(User);

                if (currentUser == null)
                    return Unauthorized("User is not logged in.");

                // This should be ERPCORE.dbo.SYS_USER.UID
                // If your Identity user uses Id instead of UID, replace currentUser.UID with currentUser.Id
                var currentUserUid = currentUser.UID;

                if (string.IsNullOrWhiteSpace(currentUserUid))
                    return Unauthorized("Logged-in user UID was not found.");

                var isAdminUser = false;

                var userRoleNames = await _userManager.GetRolesAsync(currentUser);

                foreach (var roleName in userRoleNames)
                {
                    var role = await _roleManager.FindByNameAsync(roleName);

                    if (role != null && role.PriorityLevel > 95)
                    {
                        isAdminUser = true;
                        break;
                    }
                }

                var connString = _context.Database.GetConnectionString();

                if (string.IsNullOrWhiteSpace(connString))
                    return StatusCode(500, "Database connection could not be established.");

                using (var conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();

                    const string surveyQuery = @"
            SELECT DISTINCT 
                s.*
            FROM ERPCORE.dbo.[SURVEY] s
            WHERE 
                s.IsDeleted = 0
                AND s.IsActive = 1
                AND
                (
                    @IsAdminUser = 1

                    OR EXISTS
                    (
                        SELECT 1
                        FROM ERPHR.dbo.[Staff] st
                        INNER JOIN ERPHR.dbo.[SurveyStaff] ss 
                            ON ss.[Staff] = st.[UID]
                        INNER JOIN ERPCORE.dbo.[SYS_ROLE] sr 
                            ON sr.[UID] = ss.[Role]
                        WHERE 
                            st.[User] = @CurrentUserUid
                            AND ss.[Survey] = s.[UID]
                            AND sr.[PriorityLevel] BETWEEN 51 AND 59
                            AND st.IsDeleted = 0 
                            AND ss.IsDeleted = 0 
                            AND sr.IsDeleted = 0
                            AND st.IsActive = 1 
                            AND ss.IsActive = 1 
                            AND sr.IsActive = 1
                    )
                )
            ORDER BY s.CreatedTime DESC;";

                    using (var cmd = new SqlCommand(surveyQuery, conn))
                    {
                        cmd.Parameters.Add("@IsAdminUser", SqlDbType.Bit).Value = isAdminUser;
                        cmd.Parameters.Add("@CurrentUserUid", SqlDbType.NVarChar, 100).Value = currentUserUid;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            // Do not return NotFound here.
                            // If there are no rows, the while loop simply will not run,
                            // and the empty surveys list will be passed to the view.

                            while (await reader.ReadAsync())
                            {
                                surveys.Add(new SurveyViewModel
                                {
                                    ID = Convert.ToInt32(reader["ID"]),
                                    UID = reader["UID"]?.ToString(),
                                    Name = reader["Name"]?.ToString(),
                                    ShortVersion = reader["ShortVersion"]?.ToString(),
                                    ExecutionYear = reader["ExecutionYear"]?.ToString(),
                                    ReferencePeriod = reader["ReferencePeriod"]?.ToString(),
                                    SurveyType = reader["SurveyType"]?.ToString(),
                                    DatabaseName = reader["DatabaseName"]?.ToString(),

                                    CreatedTime = reader["CreatedTime"] == DBNull.Value
                                        ? DateTime.MinValue
                                        : Convert.ToDateTime(reader["CreatedTime"]),

                                    ModifiedTime = reader["ModifiedTime"] == DBNull.Value
                                        ? null
                                        : Convert.ToDateTime(reader["ModifiedTime"]),

                                    IsActive = reader["IsActive"] != DBNull.Value
                                        && Convert.ToBoolean(reader["IsActive"]),

                                    IsDeleted = reader["IsDeleted"] != DBNull.Value
                                        && Convert.ToBoolean(reader["IsDeleted"]),

                                    CreatedBy = reader["CreatedBy"]?.ToString(),

                                    ModifiedBy = reader["ModifiedBy"] == DBNull.Value
                                        ? null
                                        : reader["ModifiedBy"]?.ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching surveys: {ex.Message}");
            }

            return View(surveys);
        }

        [HttpGet("Entity")]
        public async Task<IActionResult> Entity(string id, string? msg)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Survey ID is required.");

            string? database;
            string? surveyName;
            string? executionYear;

            try
            {
                var currentUser = await _userManager.GetUserAsync(User);

                if (currentUser == null)
                    return Unauthorized("User is not logged in.");

                // This should be ERPCORE.dbo.SYS_USER.UID
                // If your Identity user uses Id instead of UID, replace currentUser.UID with currentUser.Id
                var currentUserUid = currentUser.UID;

                if (string.IsNullOrWhiteSpace(currentUserUid))
                    return Unauthorized("Logged-in user UID was not found.");

                var isAdminUser = false;

                var userRoleNames = await _userManager.GetRolesAsync(currentUser);

                foreach (var roleName in userRoleNames)
                {
                    var role = await _roleManager.FindByNameAsync(roleName);

                    if (role != null && role.PriorityLevel > 95)
                    {
                        isAdminUser = true;
                        break;
                    }
                }

                var connString = _context.Database.GetConnectionString();

                if (string.IsNullOrWhiteSpace(connString))
                    return StatusCode(500, "Database connection could not be established.");

                using (var conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();

                    const string query = @"
                    SELECT TOP 1
                        s.[Name],
                        s.[ExecutionYear],
                        s.[DatabaseName]
                    FROM ERPCORE.dbo.[SURVEY] s
                    WHERE 
                        s.[UID] = @Id
                        AND s.[IsDeleted] = 0
                        AND s.[IsActive] = 1
                        AND
                        (
                            @IsAdminUser = 1

                            OR EXISTS
                            (
                                SELECT 1
                                FROM ERPHR.dbo.[Staff] st
                                INNER JOIN ERPHR.dbo.[SurveyStaff] ss
                                    ON ss.[Staff] = st.[UID]
                                INNER JOIN ERPCORE.dbo.[SYS_ROLE] sr
                                    ON sr.[UID] = ss.[Role]
                                WHERE 
                                    st.[User] = @CurrentUserUid
                                    AND ss.[Survey] = s.[UID]
                                    AND sr.[PriorityLevel] BETWEEN 51 AND 59
                            )
                        );";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@Id", SqlDbType.NVarChar, 100).Value = id;
                        cmd.Parameters.Add("@IsAdminUser", SqlDbType.Bit).Value = isAdminUser;
                        cmd.Parameters.Add("@CurrentUserUid", SqlDbType.NVarChar, 100).Value = currentUserUid;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync())
                                return NotFound("Survey not found or access denied.");

                            surveyName = reader["Name"] == DBNull.Value
                                ? null
                                : reader["Name"]?.ToString();

                            executionYear = reader["ExecutionYear"] == DBNull.Value
                                ? null
                                : reader["ExecutionYear"]?.ToString();

                            database = reader["DatabaseName"] == DBNull.Value
                                ? null
                                : reader["DatabaseName"]?.ToString();
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(database))
                    return NotFound("Survey database was not found.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching survey details: {ex.Message}");
            }

            List<Entity> entities;

            try
            {
                entities = await _context.Entities
                    .Where(m => m.Database == database && !m.IsDeleted)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching entities: {ex.Message}");
            }

            ViewBag.Survey = id;
            ViewBag.SurveyName = surveyName;
            ViewBag.ExecutionYear = executionYear;
            ViewBag.DatabaseName = database;
            ViewBag.Message = msg;

            return View(entities);
        }

        [HttpGet("Table")]
        public virtual async Task<IActionResult> Table(string table)
        {
            if (string.IsNullOrEmpty(table))
                return BadRequest("Table UID is required.");

            // Get the entity where UID equals the table parameter
            var entity = await _context.Entities
           .Where(e => e.UID == table && !e.IsDeleted)
           .FirstOrDefaultAsync();

            if (entity == null)
                return NotFound();

            ViewBag.Table = entity.Name;
            ViewBag.UID = entity.UID;
            ViewBag.Path = "crud";

           
            return View(entity);
        }



    }
}