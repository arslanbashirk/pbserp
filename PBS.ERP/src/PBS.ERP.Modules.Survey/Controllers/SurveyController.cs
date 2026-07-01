using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PBS.ERP.Infrastructure;
using PBS.ERP.Infrastructure.Interfaces;
using PBS.ERP.Shared.Models;
using static PBS.ERP.Shared.Models.Constants;
using static PBS.ERP.Shared.Models.SurveyModel;

namespace PBS.ERP.Modules.Survey.Controllers
{
    [Route("[controller]")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize(
        AuthenticationSchemes = Constants.Identity_Application_Scheme,
        Roles = "Root,Super,Admin")]
    public class SurveyController : Controller
    {

        private readonly ISuperInterface _tableService;
        protected readonly ApplicationDbContext _context;
        public SurveyController(
        ISuperInterface tableService,
        ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tableService = tableService;
        }

        [HttpGet("All")]
        public async Task<IActionResult> All()
        {
            var surveys = new List<SurveyViewModel>();

            try
            {
                var connString = _context.Database.GetConnectionString();
                if (string.IsNullOrWhiteSpace(connString))
                    return StatusCode(500, "Database connection could not be established.");

                using (var conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();

                    const string surveyQuery = "SELECT * FROM [Survey] WHERE IsDeleted = 0 and IsActive=1";

                    using (var cmd = new SqlCommand(surveyQuery, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (!reader.HasRows)
                            return NotFound("No surveys found.");

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
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching surveys: {ex.Message}");
            }

            return View(surveys);
        }

        [HttpGet("Index")]
        public async Task<IActionResult> Index(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Survey ID is required.");
            ViewBag.Survey = id;
            var survey = new SurveyViewModel();

            try
            {
                var connString = _context.Database.GetConnectionString();
                if (string.IsNullOrWhiteSpace(connString))
                    return StatusCode(500, "Database connection could not be established.");

                using (var conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();

                    // 1️⃣ Get Survey
                    const string surveyQuery = "SELECT * FROM [Survey] WHERE UID = @Id";
                    using (var cmd = new SqlCommand(surveyQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (!reader.HasRows)
                                return NotFound("Survey not found.");

                            await reader.ReadAsync();

                            survey = new SurveyViewModel
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
                            };
                        }
                    }

                    // 2️⃣ Get Questionnaires
                    const string qQuery = "SELECT * FROM [Questionnaire] WHERE Survey = @SurveyName AND IsDeleted = 0";
                    using (var qCmd = new SqlCommand(qQuery, conn))
                    {
                        qCmd.Parameters.AddWithValue("@SurveyName", survey.UID);

                        using (var qReader = await qCmd.ExecuteReaderAsync())
                        {
                            while (await qReader.ReadAsync())
                            {
                                var questionnaire = new QuestionnaireViewModel
                                {
                                    ID = Convert.ToInt32(qReader["ID"]),
                                    UID = qReader["UID"].ToString(),
                                    Survey = qReader["Survey"].ToString(),
                                    Type = qReader["Type"].ToString(),
                                    Version = Convert.ToInt32(qReader["Version"]),
                                    IsActive = Convert.ToBoolean(qReader["IsActive"]),
                                    IsDeleted = Convert.ToBoolean(qReader["IsDeleted"]),
                                    CreatedBy = qReader["CreatedBy"].ToString(),
                                    ModifiedBy = qReader["ModifiedBy"]?.ToString()
                                };

                                survey.Questionnaires.Add(questionnaire);
                            }
                        }
                    }

                    // 3️⃣ Get Sections + Forms (IMPORTANT: new connection per nested read)
                    foreach (var questionnaire in survey.Questionnaires)
                    {
                        const string sQuery = "SELECT * FROM [Section] WHERE Questionnaire = @QType AND IsDeleted = 0";

                        using (var sCmd = new SqlCommand(sQuery, conn))
                        {
                            sCmd.Parameters.AddWithValue("@QType", questionnaire.UID);

                            using (var sReader = await sCmd.ExecuteReaderAsync())
                            {
                                while (await sReader.ReadAsync())
                                {
                                    var section = new SectionViewModel
                                    {
                                        ID = Convert.ToInt32(sReader["ID"]),
                                        UID = sReader["UID"].ToString(),
                                        Questionnaire = sReader["Questionnaire"].ToString(),
                                        SortOrder = Convert.ToInt32(sReader["SortOrder"]),
                                        Title = sReader["Title"].ToString(),
                                        Subtitle = sReader["Subtitle"].ToString(),
                                        IsVisible = Convert.ToBoolean(sReader["IsVisible"]),
                                        IsDeleted = Convert.ToBoolean(sReader["IsDeleted"]),
                                        CreatedBy = sReader["CreatedBy"].ToString(),
                                        ModifiedBy = sReader["ModifiedBy"]?.ToString()
                                    };

                                    questionnaire.Sections.Add(section);
                                }
                            }
                        }

                        // 4️⃣ Forms for each section
                        foreach (var section in questionnaire.Sections)
                        {
                            const string fQuery = "SELECT * FROM [Form] WHERE Section = @SectionTitle AND IsDeleted = 0";

                            using (var fCmd = new SqlCommand(fQuery, conn))
                            {
                                fCmd.Parameters.AddWithValue("@SectionTitle", section.UID);

                                using (var fReader = await fCmd.ExecuteReaderAsync())
                                {
                                    while (await fReader.ReadAsync())
                                    {
                                        var form = new FormViewModel
                                        {
                                            ID = Convert.ToInt32(fReader["ID"]),
                                            UID = fReader["UID"].ToString(),
                                            Section = fReader["Section"].ToString(),
                                            SortOrder = Convert.ToInt32(fReader["SortOrder"]),
                                            Heading = fReader["Heading"].ToString(),
                                            Type = fReader["Type"].ToString(),
                                            TableName = fReader["TableName"].ToString(),
                                            IsActive = Convert.ToBoolean(fReader["IsActive"]),
                                            IsDeleted = Convert.ToBoolean(fReader["IsDeleted"]),
                                            CreatedBy = fReader["CreatedBy"].ToString(),
                                            ModifiedBy = fReader["ModifiedBy"]?.ToString()
                                        };

                                        section.Forms.Add(form);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching survey hierarchy: {ex.Message}");
            }

            return View(survey);
        }


        [HttpGet("Questionnaire")]
        public async Task<IActionResult> Questionnaire(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Survey ID is required.");
            ViewBag.Survey = id;
            var survey = new SurveyViewModel();

            try
            {
                var connString = _context.Database.GetConnectionString();
                if (string.IsNullOrWhiteSpace(connString))
                    return StatusCode(500, "Database connection could not be established.");

                using (var conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();

                    const string surveyQuery = "SELECT * FROM [Survey] WHERE UID = @Id";
                    using (var cmd = new SqlCommand(surveyQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (!reader.HasRows)
                                return NotFound("Survey not found.");

                            await reader.ReadAsync();

                            survey = new SurveyViewModel
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
                            };
                        }
                    }

                    // 2️⃣ Get Questionnaires
                    const string qQuery = "SELECT * FROM [Questionnaire] WHERE Survey = @SurveyName AND IsDeleted = 0";
                    using (var qCmd = new SqlCommand(qQuery, conn))
                    {
                        qCmd.Parameters.AddWithValue("@SurveyName", survey.UID);

                        using (var qReader = await qCmd.ExecuteReaderAsync())
                        {
                            while (await qReader.ReadAsync())
                            {
                                var questionnaire = new QuestionnaireViewModel
                                {
                                    ID = Convert.ToInt32(qReader["ID"]),
                                    UID = qReader["UID"].ToString(),
                                    Survey = qReader["Survey"].ToString(),
                                    Type = qReader["Type"].ToString(),
                                    Version = Convert.ToDouble(qReader["Version"]),
                                    IsActive = Convert.ToBoolean(qReader["IsActive"]),
                                    IsDeleted = Convert.ToBoolean(qReader["IsDeleted"]),
                                    CreatedBy = qReader["CreatedBy"].ToString(),
                                    ModifiedBy = qReader["ModifiedBy"]?.ToString()
                                };

                                survey.Questionnaires.Add(questionnaire);
                            }
                        }
                    }

                    // 3️⃣ Get Sections + Forms (IMPORTANT: new connection per nested read)
                    foreach (var questionnaire in survey.Questionnaires)
                    {
                        const string sQuery = "SELECT * FROM [Section] WHERE Questionnaire = @QType AND IsDeleted = 0";

                        using (var sCmd = new SqlCommand(sQuery, conn))
                        {
                            sCmd.Parameters.AddWithValue("@QType", questionnaire.UID);

                            using (var sReader = await sCmd.ExecuteReaderAsync())
                            {
                                while (await sReader.ReadAsync())
                                {
                                    var section = new SectionViewModel
                                    {
                                        ID = Convert.ToInt32(sReader["ID"]),
                                        UID = sReader["UID"].ToString(),
                                        Questionnaire = sReader["Questionnaire"].ToString(),
                                        SortOrder = Convert.ToInt32(sReader["SortOrder"]),
                                        Title = sReader["Title"].ToString(),
                                        Subtitle = sReader["Subtitle"].ToString(),
                                        IsVisible = Convert.ToBoolean(sReader["IsVisible"]),
                                        IsDeleted = Convert.ToBoolean(sReader["IsDeleted"]),
                                        CreatedBy = sReader["CreatedBy"].ToString(),
                                        ModifiedBy = sReader["ModifiedBy"]?.ToString()
                                    };

                                    questionnaire.Sections.Add(section);
                                }
                            }
                        }

                        // 4️⃣ Forms for each section
                        foreach (var section in questionnaire.Sections)
                        {
                            const string fQuery = "SELECT * FROM [Form] WHERE Section = @SectionTitle AND IsDeleted = 0";

                            using (var fCmd = new SqlCommand(fQuery, conn))
                            {
                                fCmd.Parameters.AddWithValue("@SectionTitle", section.UID);

                                using (var fReader = await fCmd.ExecuteReaderAsync())
                                {
                                    while (await fReader.ReadAsync())
                                    {
                                        var form = new FormViewModel
                                        {
                                            ID = Convert.ToInt32(fReader["ID"]),
                                            UID = fReader["UID"].ToString(),
                                            Section = fReader["Section"].ToString(),
                                            SortOrder = Convert.ToInt32(fReader["SortOrder"]),
                                            Heading = fReader["Heading"].ToString(),
                                            Type = fReader["Type"].ToString(),
                                            TableName = fReader["TableName"].ToString(),
                                            IsActive = Convert.ToBoolean(fReader["IsActive"]),
                                            IsDeleted = Convert.ToBoolean(fReader["IsDeleted"]),
                                            CreatedBy = fReader["CreatedBy"].ToString(),
                                            ModifiedBy = fReader["ModifiedBy"]?.ToString()
                                        };

                                        section.Forms.Add(form);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching survey hierarchy: {ex.Message}");
            }

            return View(survey);
        }


        [HttpGet("Form")]
        public async Task<IActionResult> Form(string table)
        {
            return View();
        }

        

        [HttpGet("Entity")]
        public async Task<IActionResult> Entity(string id, string? msg)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Survey ID is required.");
            string database;
            string surveyName;
            string executionYear;

            try
            {
                using (var conn = new SqlConnection(_context.Database.GetConnectionString()))
                {
                    await conn.OpenAsync();

                    const string query = @"
                    SELECT
                        Name,
                        ExecutionYear,
                        DatabaseName
                    FROM Survey
                    WHERE UID = @Id";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync())
                                return NotFound("Survey not found.");

                            surveyName = reader["Name"]?.ToString();
                            executionYear = reader["ExecutionYear"]?.ToString();
                            database = reader["DatabaseName"]?.ToString();
                        }
                    }
                }
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

        [HttpPost("UploadIcon")]
        public IActionResult UploadIcon(string uid, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file");

            var dbName = GetDatabaseName(uid);
            if (string.IsNullOrEmpty(dbName))
                return NotFound("Survey not found");

            var ext = Path.GetExtension(file.FileName).ToLower();

            if (ext != ".jpg" && ext != ".png" && ext != ".jpeg")
                return BadRequest("Only JPG/PNG allowed");

            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/survey/icons");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var fileName = $"{dbName}_icon{ext}";
            var fullPath = Path.Combine(folderPath, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                file.CopyTo(stream);
            }

            return Ok("Icon uploaded successfully");
        }

        [HttpPost("UploadBanner")]
        public IActionResult UploadBanner(string uid, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file");

            var dbName = GetDatabaseName(uid);
            if (string.IsNullOrEmpty(dbName))
                return NotFound("Survey not found");

            var ext = Path.GetExtension(file.FileName).ToLower();

            if (ext != ".jpg" && ext != ".png" && ext != ".jpeg")
                return BadRequest("Only JPG/PNG allowed");

            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/survey/banners");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var fileName = $"{dbName}_banner{ext}";
            var fullPath = Path.Combine(folderPath, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                file.CopyTo(stream);
            }

            return Ok("Banner uploaded successfully");
        }

        private string GetDatabaseName(string uid)
        {
            using var conn = new SqlConnection(_context.Database.GetConnectionString());
            conn.Open();

            var query = "SELECT DatabaseName FROM [ERPCORE].[dbo].[Survey] WHERE UID = @UID AND IsDeleted = 0";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@UID", uid);

            return cmd.ExecuteScalar()?.ToString();
        }


        [HttpGet("Home")]
        public async Task<IActionResult> Home(string id)
        {
            return View();
        }

        [HttpGet("Designer")]
        public async Task<IActionResult> Designer(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Survey ID is required.");
            ViewBag.Survey = id;

            var requiredEntityUIDs = await _context.Entities
        .Where(e => !e.IsDeleted
            && e.Database == "ERPCORE"
            && e.Schema == "dbo"
            && (
                e.Name == "Questionnaire" ||
                e.Name == "Section" ||
                e.Name == "Form"
            ))
        .Select(e => new
        {
            e.Name,
            e.UID
        })
        .ToListAsync();

            ViewBag.QuestionnaireUID = requiredEntityUIDs
                .FirstOrDefault(e => e.Name == "Questionnaire")?.UID;

            ViewBag.SectionUID = requiredEntityUIDs
                .FirstOrDefault(e => e.Name == "Section")?.UID;

            ViewBag.FormUID = requiredEntityUIDs
                .FirstOrDefault(e => e.Name == "Form")?.UID;

            var survey = new SurveyViewModel();

            try
            {
                var connString = _context.Database.GetConnectionString();
                if (string.IsNullOrWhiteSpace(connString))
                    return StatusCode(500, "Database connection could not be established.");

                using (var conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();

                    // 1️⃣ Get Survey
                    const string surveyQuery = "SELECT * FROM [Survey] WHERE UID = @Id";
                    using (var cmd = new SqlCommand(surveyQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (!reader.HasRows)
                                return NotFound("Survey not found.");

                            await reader.ReadAsync();

                            survey = new SurveyViewModel
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
                            };
                        }
                    }

                    // 2️⃣ Get Questionnaires
                    const string qQuery = "SELECT * FROM [Questionnaire] WHERE Survey = @SurveyName AND IsDeleted = 0";
                    using (var qCmd = new SqlCommand(qQuery, conn))
                    {
                        qCmd.Parameters.AddWithValue("@SurveyName", survey.UID);

                        using (var qReader = await qCmd.ExecuteReaderAsync())
                        {
                            while (await qReader.ReadAsync())
                            {
                                var questionnaire = new QuestionnaireViewModel
                                {
                                    ID = Convert.ToInt32(qReader["ID"]),
                                    UID = qReader["UID"].ToString(),
                                    Survey = qReader["Survey"].ToString(),
                                    Type = qReader["Type"].ToString(),
                                    Version = Convert.ToInt32(qReader["Version"]),
                                    IsActive = Convert.ToBoolean(qReader["IsActive"]),
                                    IsDeleted = Convert.ToBoolean(qReader["IsDeleted"]),
                                    CreatedBy = qReader["CreatedBy"].ToString(),
                                    ModifiedBy = qReader["ModifiedBy"]?.ToString()
                                };

                                survey.Questionnaires.Add(questionnaire);
                            }
                        }
                    }

                    // 3️⃣ Get Sections + Forms (IMPORTANT: new connection per nested read)
                    foreach (var questionnaire in survey.Questionnaires)
                    {
                        const string sQuery = "SELECT * FROM [Section] WHERE Questionnaire = @QType AND IsDeleted = 0";

                        using (var sCmd = new SqlCommand(sQuery, conn))
                        {
                            sCmd.Parameters.AddWithValue("@QType", questionnaire.UID);

                            using (var sReader = await sCmd.ExecuteReaderAsync())
                            {
                                while (await sReader.ReadAsync())
                                {
                                    var section = new SectionViewModel
                                    {
                                        ID = Convert.ToInt32(sReader["ID"]),
                                        UID = sReader["UID"].ToString(),
                                        Questionnaire = sReader["Questionnaire"].ToString(),
                                        SortOrder = Convert.ToInt32(sReader["SortOrder"]),
                                        Title = sReader["Title"].ToString(),
                                        Subtitle = sReader["Subtitle"].ToString(),
                                        IsVisible = Convert.ToBoolean(sReader["IsVisible"]),
                                        IsDeleted = Convert.ToBoolean(sReader["IsDeleted"]),
                                        CreatedBy = sReader["CreatedBy"].ToString(),
                                        ModifiedBy = sReader["ModifiedBy"]?.ToString()
                                    };

                                    questionnaire.Sections.Add(section);
                                }
                            }
                        }

                        // 4️⃣ Forms for each section
                        foreach (var section in questionnaire.Sections)
                        {
                            const string fQuery = "SELECT * FROM [Form] WHERE Section = @SectionTitle AND IsDeleted = 0";

                            using (var fCmd = new SqlCommand(fQuery, conn))
                            {
                                fCmd.Parameters.AddWithValue("@SectionTitle", section.UID);

                                using (var fReader = await fCmd.ExecuteReaderAsync())
                                {
                                    while (await fReader.ReadAsync())
                                    {
                                        var form = new FormViewModel
                                        {
                                            ID = Convert.ToInt32(fReader["ID"]),
                                            UID = fReader["UID"].ToString(),
                                            Section = fReader["Section"].ToString(),
                                            SortOrder = Convert.ToInt32(fReader["SortOrder"]),
                                            Heading = fReader["Heading"].ToString(),
                                            Type = fReader["Type"].ToString(),
                                            TableName = fReader["TableName"].ToString(),
                                            IsActive = Convert.ToBoolean(fReader["IsActive"]),
                                            IsDeleted = Convert.ToBoolean(fReader["IsDeleted"]),
                                            CreatedBy = fReader["CreatedBy"].ToString(),
                                            ModifiedBy = fReader["ModifiedBy"]?.ToString()
                                        };

                                        section.Forms.Add(form);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching survey hierarchy: {ex.Message}");
            }

            return View(survey);
        }



        [HttpGet("Mode")]
        public async Task<IActionResult> Mode(string id)
        {
            var surveys = new MainModel();

            try
            {
                if (!await IsConnectionOk())
                {
                    return StatusCode(500, ServerErrorMessages.ErrorDB);
                }

                using (var conn = new SqlConnection(_context.Database.GetConnectionString()))
                {
                    await conn.OpenAsync();

                    var surveyQuery = @"SELECT * FROM [Survey] WHERE IsDeleted = 0 AND UID = @id";

                    var survey = await conn.QueryFirstOrDefaultAsync<SurveyViewModel>(
                        surveyQuery,
                        new { id }
                    );

                    if (survey == null)
                    {
                        return NotFound("Survey not found.");
                    }

                    surveys.Survey = survey;
                }

                return View(surveys);
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

        [HttpGet("Frame")]
        public async Task<IActionResult> Frame(string id)
        {
            string? table;
            string? uid;

            var frame = new List<PhaseFrameView>();

            try
            {
                if (!await IsConnectionOk())
                {
                    return StatusCode(500, ServerErrorMessages.ErrorDB);
                }

                using (var conn = new SqlConnection(_context.Database.GetConnectionString()))
                {
                    await conn.OpenAsync();

                    var q = @"SELECT UID, TableReference FROM [ERPCORE].[dbo].[FileUpload] 
                            WHERE IsDeleted = 0 AND Title LIKE '%Survey Frame Upload Utility%'";
                    var result = (await conn.QueryAsync<(string UID, string TableReference)>(q)).FirstOrDefault();
                    ViewBag.UID = result.UID;
                    ViewBag.Table = result.TableReference;


                    var query = @"SELECT p.UID as PhaseUID, p.Phase, p.Title, f.UID, f.AreaCode, f.Status, f.ReplacedAreaCode 
                          FROM [ERPCORE].[dbo].[SurveyPhase] p 
                          LEFT OUTER JOIN [ERPFRM].[dbo].[SurveyFrame] f 
                          ON (f.SurveyPhase = p.UID) 
                          WHERE p.IsDeleted = 0 
                          AND p.Survey = @id";

                    frame = (await conn.QueryAsync<PhaseFrameView>(
                        query,
                        new { id }
                    )).ToList();

                    if (frame == null || !frame.Any())
                    {
                        return NotFound("Survey Phase not found.");
                    }
                }

                ViewBag.Survey = id;
                return View(frame);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }


        [ValidateAntiForgeryToken]
        [HttpPost("Survey/Create")]
        public async Task<IActionResult> Create(SurveyTableCreate request)
        {
            if (string.IsNullOrWhiteSpace(request.Survey) || string.IsNullOrWhiteSpace(request.TableName))
                return BadRequest("Survey Table is required.");
            string database;
            try
            {
                using (var conn = new SqlConnection(_context.Database.GetConnectionString()))
                {
                    if (conn == null)
                        return StatusCode(500, "Database connection could not be established.");

                    await conn.OpenAsync();

                    const string query = "SELECT [DatabaseName] FROM [Survey] WHERE UID = @Id";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", request.Survey);
                        var result = await cmd.ExecuteScalarAsync();

                        if (result == null || result == DBNull.Value)
                            return NotFound("Survey not found or database not set.");

                        database = result.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching survey database: {ex.Message}");
            }
            var user = User?.Identity?.Name ?? "system";

            var create = new TableCreateRequest
            {
                Table = request.TableName,
                Database = database,
                TableType = "SLT",
                TableDescription = request.TableDescription
            };
            var res = await _tableService.CreateTableAsync(create, user);
            string message;
            if (!res.Success)
            {
                message = $"Table creation failed, " + res.Message;
            }
            else
            {
                message = "Table created successfully";
            }

            return RedirectToAction("Entity", new { id = request.Survey, msg = message });
        }

    }




}