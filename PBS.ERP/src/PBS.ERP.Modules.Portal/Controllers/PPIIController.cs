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

namespace Election.Controllers
{
    [Route("[controller]")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize(
        AuthenticationSchemes = Constants.Identity_Application_Scheme,
        Roles = "Root,Super,Admin,Staff")]
    public class PPIIController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public PPIIController(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
        {
            _userManager = userManager;
            _context = db;
        }
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("Home")]
        public async Task<IActionResult> Home(string id)
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
                            FieldStartDate,
                            FieldClosingDate,
                            Env
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
                        new { id, cnic=user.CNIC, uid=user.UID }
                    )).ToList();

                    model.roles = roles;
                    model.user = user;

                    var fieldQuery = @"SELECT  f.PSIC4, f.PSIC, f.Province, f.District, f.DistrictCode, 
                    f.AreaCode, f.Name, f.Address, f.Contact, f.Employee, b.UID Form1, i.UID as Form2, 0 as ReferBack, r.UID as Respondent, 
                    b.CreatedTime, b.ModifiedTime, b.CreatedBy, b.ModifiedBy FROM [PPII2026].[dbo].[BasicInfo] b
                    right outer join (select  PSIC4, PSIC, Province, District, DistrictCode, 
                    AreaCode, Name, Address, Contact, Employee FROM [PPII2026].[dbo].[Sample]
                    where Env!='FIELD' and isDeleted=0) f on (f.AreaCode=b.AreaCode AND b.CreatedBy=@email)
                    left outer join (SELECT AreaCode, Max(UID) as UID, Max(CreatedBy) as CreatedBy FROM  [PPII2026].[dbo].[Items] WHERE CreatedBy=@email AND IsDeleted=0  Group By AreaCode) i on (f.AreaCode=i.AreaCode AND i.CreatedBy=@email) 
                    left outer join [PPII2026].[dbo].[Respondent] r on (f.AreaCode=r.AreaCode AND r.CreatedBy=@email) 
                    order by f.DistrictCode,f.AreaCode";
                    
                    
                    if (model.phase!=null && model.phase.Env != null && model.phase.Env.Equals("FIELD"))
                    {
                            //field query 
                            fieldQuery = @"SELECT  
                                f.PSIC4, f.PSIC, f.Province, f.District, f.DistrictCode, 
                                f.AreaCode, f.Name, f.Address, f.Contact, f.Employee,
                                b.UID AS Form1, 
                                i.UID AS Form2, 
                                i.ReferBack,
                                r.UID AS Respondent, 
                                b.CreatedTime, b.ModifiedTime, b.CreatedBy, b.ModifiedBy
                            FROM [PPII2026].[dbo].[Sample] f
                            LEFT JOIN [PPII2026].[dbo].[BasicInfo] b 
                                ON f.AreaCode = b.AreaCode

                            LEFT JOIN (
                                SELECT AreaCode, MAX(UID) AS UID, SUM(Case when VerificationStatus=2 then 1 else 0 end) as ReferBack
                                FROM [PPII2026].[dbo].[Items]
                                WHERE IsDeleted = 0
                                GROUP BY AreaCode
                            ) i
                                ON f.AreaCode = i.AreaCode

                            LEFT JOIN [PPII2026].[dbo].[Respondent] r 
                                ON f.AreaCode = r.AreaCode

                            WHERE f.IsDeleted = 0
                            AND f.Office IN (
                                SELECT uid 
                                FROM [ERPHR].[dbo].[Office] 
                                WHERE isdeleted = 0
                                AND code IN (
                                    SELECT officeID 
                                    FROM [ERPHR].[dbo].[Staff] 
                                    WHERE isDeleted = 0 
                                    AND (CNIC = @cnic OR [User] = @uid)
                                )
                            )
                            ORDER BY f.DistrictCode, f.AreaCode;";

                            model.sample = (await conn.QueryAsync<FieldPPIISample>(
                                fieldQuery,
                                new { cnic=user.CNIC, uid=user.UID }
                            )).ToList();
                    }
                    else
                    {
                        model.sample = (await conn.QueryAsync<FieldPPIISample>(
                            fieldQuery,
                            new { user.Email }
                        )).ToList();
                    }
                }

                return View(model);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("Form1")]
        public virtual async Task<IActionResult> Form1(string code)
        {

            string survey = "2f0227ed-af86-44f4-be5d-2407ab793052";
            string table = "e2607096-e8a7-4e6a-9c80-5e6b72f817a8";
            if (string.IsNullOrEmpty(table))
                return BadRequest("Table UID is required.");

            // Get the entity where UID equals the table parameter
            var entity = await _context.Entities
           .Where(e => e.UID == table && !e.IsDeleted)
           .FirstOrDefaultAsync();

            if (entity == null)
                return NotFound();

            var where = await GetWhere();
            ViewBag.Where = where;

            ViewBag.Table = entity.Name;
            ViewBag.UID = entity.UID;
            ViewBag.Code = code;
            ViewBag.Path = "crud";

            return View(entity);
        }

        [HttpGet("Form2")]
        public virtual async Task<IActionResult> Form2(string code, string psic)
        {
            string table = "740b6820-f0b7-4e83-b40a-6957415722ac";
            if (string.IsNullOrEmpty(table))
                return BadRequest("Table UID is required.");

            // Get the entity where UID equals the table parameter
            var entity = await _context.Entities
           .Where(e => e.UID == table && !e.IsDeleted)
           .FirstOrDefaultAsync();

            if (entity == null)
                return NotFound();


            var where = await GetWhere();
            ViewBag.Where = where;

            ViewBag.Table = entity.Name;
            ViewBag.UID = entity.UID;
            ViewBag.Code = code;
            ViewBag.PSIC = psic;
            ViewBag.Path = "crud";

            return View(entity);
        }

        [HttpGet("Form3")]
        public virtual async Task<IActionResult> Form3(string code)
        {
            string survey = "2f0227ed-af86-44f4-be5d-2407ab793052";
            string table = "2d0009f7-1fc8-4240-bb84-11fb696230f4";
            if (string.IsNullOrEmpty(table))
                return BadRequest("Table UID is required.");

            // Get the entity where UID equals the table parameter
            var entity = await _context.Entities
           .Where(e => e.UID == table && !e.IsDeleted)
           .FirstOrDefaultAsync();

            if (entity == null)
                return NotFound();


            var where = await GetWhere();
            ViewBag.Where = where;
            ViewBag.Table = entity.Name;
            ViewBag.UID = entity.UID;
            ViewBag.Code = code;
            ViewBag.Path = "crud";

            return View(entity);
        }



        [HttpGet("Entity")]
        public async Task<IActionResult> Entity(string id)
        {
            string survey = "2f0227ed-af86-44f4-be5d-2407ab793052";
            FieldPPIISample entity = new FieldPPIISample();

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            try
            {
                if (!await IsConnectionOk())
                    return StatusCode(500, "Database connection failed.");

                using (var conn = new SqlConnection(_context.Database.GetConnectionString()))
                {
                    await conn.OpenAsync();

                    // Get phase
                    var phaseQuery = @"
                    SELECT UID, Phase, Title, Survey,
                           ReferencePeriod, TestingClosingDate,
                           TrainingclosingDate, FieldStartDate,
                           FieldClosingDate, Env
                    FROM [ERPCORE].[dbo].[SurveyPhase]
                    WHERE Survey = @survey AND IsDeleted = 0 AND IsActive = 1
                    ORDER BY Phase";

                    var phase = (await conn.QueryAsync<FieldPhase>(
                        phaseQuery,
                        new { survey }
                    )).FirstOrDefault();

                    // Default query
                    var fieldQuery = @"
                        SELECT f.PSIC4, f.PSIC, f.Province, f.District, f.DistrictCode,
                               f.AreaCode, f.Name, f.Address, f.Contact, f.Employee,
                               b.UID AS Form1, r.UID AS Respondent,
                               b.CreatedTime, b.ModifiedTime, b.CreatedBy, b.ModifiedBy
                        FROM [PPII2026].[dbo].[BasicInfo] b
                        RIGHT OUTER JOIN (
                            SELECT PSIC4, PSIC, Province, District, DistrictCode,
                                   AreaCode, Name, Address, Contact, Employee
                            FROM [PPII2026].[dbo].[Sample]
                            WHERE Env != 'FIELD' AND IsDeleted = 0
                        ) f ON (f.AreaCode = b.AreaCode AND b.CreatedBy = @email)
                        LEFT JOIN [PPII2026].[dbo].[Respondent] r 
                            ON (f.AreaCode = r.AreaCode AND r.CreatedBy = @email)
                        WHERE f.AreaCode = @id
                        ORDER BY f.DistrictCode, f.AreaCode";

                    // FIELD environment override
                    if (phase?.Env?.Equals("FIELD", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        fieldQuery = @"
                            SELECT f.PSIC4, f.PSIC, f.Province, f.District, f.DistrictCode,
                                    f.AreaCode, f.Name, f.Address, f.Contact, f.Employee,
                                    b.UID AS Form1, r.UID AS Respondent,
                                    b.CreatedTime, b.ModifiedTime, b.CreatedBy, b.ModifiedBy
                            FROM [PPII2026].[dbo].[BasicInfo] b
                            RIGHT OUTER JOIN (
                                SELECT PSIC4, PSIC, Province, District, DistrictCode,
                                        AreaCode, Name, Address, Contact, Employee
                                FROM [PPII2026].[dbo].[Sample]
                                WHERE office IN (
                                    SELECT uid 
                                    FROM [ERPHR].[dbo].[Office]
                                    WHERE IsDeleted = 0 
                                    AND code IN (
                                        SELECT officeID 
                                        FROM [ERPHR].[dbo].[Staff]
                                        WHERE IsDeleted = 0 
                                        AND (CNIC = @cnic OR [User] = @uid)
                                    )
                                )
                                AND IsDeleted = 0
                            ) f ON (f.AreaCode = b.AreaCode)
                            LEFT JOIN [PPII2026].[dbo].[Respondent] r 
                                ON (f.AreaCode = r.AreaCode)
                            WHERE f.AreaCode = @id
                            ORDER BY f.DistrictCode, f.AreaCode";

                        entity = (await conn.QueryAsync<FieldPPIISample>(
                            fieldQuery,
                            new { id, cnic = user.CNIC, uid = user.UID }
                        )).FirstOrDefault();
                    }
                    else
                    {
                        entity = (await conn.QueryAsync<FieldPPIISample>(
                            fieldQuery,
                            new { id, email = user.Email }
                        )).FirstOrDefault();
                    }
                }

                // Return ONLY entity as JSON
                return Ok(entity);
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



        [HttpGet("Where")]
        private async Task<string> GetWhere()
        {
            string alias = "{alias}";
            string surveyId = "2f0227ed-af86-44f4-be5d-2407ab793052";
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return null;

            string where = $"{alias}.CreatedBy='{user.Email}' AND";

            if (!await IsConnectionOk())
                throw new Exception("Database connection failed.");

            using (var conn = new SqlConnection(_context.Database.GetConnectionString()))
            {
                await conn.OpenAsync();

                var phaseQuery = @"SELECT Env FROM [ERPCORE].[dbo].[SurveyPhase]
                           WHERE Survey = @id AND IsDeleted = 0 AND IsActive = 1";

                var phase = (await conn.QueryAsync<FieldPhase>(phaseQuery, new { id = surveyId }))
                            .FirstOrDefault();

                var roleQuery = @"SELECT Name FROM [ERPCORE].[dbo].[SYS_ROLE]
                          WHERE UID IN (
                              SELECT Role FROM [ERPHR].[dbo].[SurveyStaff]
                              WHERE Survey = @id AND Staff IN (
                                  SELECT UID FROM [ERPHR].[dbo].[Staff]
                                  WHERE CNIC = @cnic OR [User] = @uid
                              )
                          )";

                var roles = (await conn.QueryAsync<FieldRole>(
                    roleQuery,
                    new { id = surveyId, cnic = user.CNIC, uid = user.UID }
                )).ToList();

                bool isFieldRole = roles.Any(r =>
                    r.Name.Equals("Enumerator", StringComparison.OrdinalIgnoreCase) ||
                    r.Name.Equals("Supervisor", StringComparison.OrdinalIgnoreCase));

                if (phase?.Env?.Equals("FIELD", StringComparison.OrdinalIgnoreCase) == true && isFieldRole)
                {
                    where = "";
                }
            }

            return where;
        }
    }
}
