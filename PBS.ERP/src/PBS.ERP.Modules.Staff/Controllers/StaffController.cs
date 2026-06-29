using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PBS.ERP.Infrastructure;
using PBS.ERP.Shared.Identity;
using PBS.ERP.Shared.Models;
using static PBS.ERP.Shared.Models.SurveyModel;
namespace PBS.ERP.Modules.Staff.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize(
        AuthenticationSchemes = Constants.Identity_Application_Scheme,
        Roles = "Root,Super,Admin,Staff")]
    public class StaffController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public StaffController(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
        {
            _userManager = userManager;
            _context = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Account");

            return View(user);
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Account");

            return View(user);
        }
        
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var name = await _context.Entities
           .Where(e => e.Name == "Dashboard" && e.Database == "ERPCORE" && !e.IsDeleted)
           .Select(e => e.UID)
           .FirstOrDefaultAsync();
            ViewBag.Table = name;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Task()
        {
            var name = await _context.Entities
           .Where(e => e.Name == Constants.FileUploadFacility && e.Database == "ERPCORE" && !e.IsDeleted)
           .Select(e => e.UID)
           .FirstOrDefaultAsync();
            ViewBag.Table = name;
            return View();
        }

        [HttpGet]
        public IActionResult About()
        {
            return View();
        }


        [HttpGet("Survey")]
        public async Task<IActionResult> Survey()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var surveys = new List<SurveyViewModel>();
            try
            {
                var connString = _context.Database.GetConnectionString();
                if (string.IsNullOrWhiteSpace(connString))
                    return StatusCode(500, "Database connection could not be established.");

                using (var conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();

                    const string surveyQuery = @"
                    SELECT * FROM [ERPCORE].[dbo].[Survey] 
                    WHERE IsDeleted = 0 AND IsActive = 1";

                    using (var cmd = new SqlCommand(surveyQuery, conn))
                    {

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (!reader.HasRows)
                                return View(null);

                            while (await reader.ReadAsync())
                            {
                                surveys.Add(new SurveyViewModel
                                {
                                    ID = Convert.ToInt32(reader["ID"]),
                                    UID = reader["UID"].ToString(),
                                    Name = reader["Name"].ToString(),
                                    ShortVersion = reader["ShortVersion"].ToString(),
                                    ExecutionYear = reader["ExecutionYear"].ToString(),
                                    ReferencePeriod = reader["ReferencePeriod"].ToString(),
                                    SurveyType = reader["SurveyType"].ToString(),
                                    DatabaseName = reader["DatabaseName"].ToString(),
                                    CreatedTime = Convert.ToDateTime(reader["CreatedTime"]),
                                    ModifiedTime = reader["ModifiedTime"] as DateTime?,
                                    IsActive = Convert.ToBoolean(reader["IsActive"]),
                                    IsDeleted = Convert.ToBoolean(reader["IsDeleted"]),
                                    CreatedBy = reader["CreatedBy"].ToString(),
                                    ModifiedBy = reader["ModifiedBy"]?.ToString()
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

        [HttpGet("Field")]
        public async Task<IActionResult> Field(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();
            try
            {
                if (!await IsConnectionOk())
                {
                    return StatusCode(500, "Database connection failed.");
                }

                var model = new FieldView();

                using (var conn = new SqlConnection(_context.Database.GetConnectionString()))
                {
                    await conn.OpenAsync();

                    var surveyQuery = @"
                        SELECT UID, Name, ShortVersion, ExecutionYear,ReferencePeriod
                        FROM [ERPCORE].[dbo].[Survey]  WHERE UID = @id AND IsDeleted=0 AND IsActive = 1";

                    var survey = await conn.QueryFirstOrDefaultAsync<FieldSurvey>(
                        surveyQuery,
                        new { id }
                    );

                    if (survey == null)
                        return NotFound("Survey not found");

                    model.survey = survey;

                    var phaseQuery = @"
                        SELECT 
                            UID, Phase, Title, Survey,
                            ReferencePeriod,
                            TestingClosingDate,
                            TrainingclosingDate,
                            FieldClosingDate
                        FROM [ERPCORE].[dbo].[SurveyPhase] 
                        WHERE Survey = @id AND IsDeleted=0 AND IsActive = 1
                        ORDER BY Phase
                    ";
                    
                    var phase = (await conn.QueryAsync<FieldPhase>(
                        phaseQuery,
                        new { id }
                    )).FirstOrDefault();


                    model.phase = phase;

                    
                    var roleQuery = @"SELECT UID, Name, Description, PriorityLevel FROM [ERPCORE].[dbo].[SYS_ROLE] 
                    WHERE IsDeleted = 0 AND IsActive = 1 
                    AND UID IN 
                    (
                          SELECT Role 
                          FROM [ERPHR].[dbo].[SurveyStaff] 
                          WHERE IsDeleted = 0 AND Survey=@id AND Staff IN 
                          (
                                SELECT UID 
                                FROM [ERPHR].[dbo].[Staff] 
                                WHERE IsDeleted = 0 AND (CNIC = @cnic OR [User] = @uid)
                          )   
                    )";

                    var roles = (await conn.QueryAsync<FieldRole>(
                        roleQuery,
                        new { id, user.CNIC, user.UID }
                    )).ToList();

                    model.roles = roles;
                    model.user= user;
                }

                return View(model);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        private async Task<bool> IsConnectionOk()
        {
            return !string.IsNullOrWhiteSpace(_context.Database.GetConnectionString());
        }



        [HttpGet("Export")]
        public async Task<IActionResult> Export()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Unauthorized();

            var userRoles = await _userManager.GetRolesAsync(user);

            var list = new List<ExportView>();

            try
            {
                var connString = _context.Database.GetConnectionString();

                if (string.IsNullOrWhiteSpace(connString))
                    return StatusCode(500, "Database connection could not be established.");

                await using var conn = new SqlConnection(connString);

                await conn.OpenAsync();

                const string query = @"
                SELECT 
                    Heading,
                    TableReference,
                    Filter,
                    VisibleRole,
                    CreatedBy,
                    ModifiedBy
                FROM [ERPCORE].[dbo].[ExportUtility]
                WHERE IsDeleted = 0
                AND IsActive = 1
                ";

                await using var cmd = new SqlCommand(query, conn);

                await using var reader = await cmd.ExecuteReaderAsync();

                if (!reader.HasRows)
                    return View(null);

                while (await reader.ReadAsync())
                {
                    var visibleRole = reader["VisibleRole"]?.ToString();
                    var createdBy = reader["CreatedBy"]?.ToString();
                    var modifiedBy = reader["ModifiedBy"]?.ToString();

                    bool isOwner =
                        !string.IsNullOrWhiteSpace(visibleRole) &&
                        userRoles.Any(r =>
                            r.Equals(visibleRole, StringComparison.OrdinalIgnoreCase));

                    if (isOwner)
                    {
                        list.Add(new ExportView
                        {
                            Heading = reader["Heading"]?.ToString(),
                            TableReference = reader["TableReference"]?.ToString(),
                            Filter = reader["Filter"]?.ToString(),
                            VisibleRole = visibleRole
                        });
                    }
                    else
                    {
                        list.Add(new ExportView
                        {
                            Heading = reader["Heading"]?.ToString(),
                            TableReference = reader["TableReference"]?.ToString(),
                            Filter = reader["Filter"]?.ToString()+ " AND (LOWER(CreatedBy)='" + user.Email?.ToLower()+ "' or LOWER(ModifiedBy)='" + user.Email?.ToLower()+"')",
                            VisibleRole = visibleRole
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching exports: {ex.Message}");
            }

            return View(list);
        }

    }
}