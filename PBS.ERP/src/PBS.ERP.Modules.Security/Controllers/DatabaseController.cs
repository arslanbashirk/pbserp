using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBS.ERP.Modules.Security.Services;

namespace PBS.ERP.Modules.Security.Controllers
{
    [Authorize(Roles = "Super, Root")]
    public class DatabaseController : Controller
    {
        private readonly DatabaseBackupService _backupService;

        public DatabaseController(DatabaseBackupService backupService)
        {
            _backupService = backupService;
        }
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var databases = await _backupService.GetAllowedDatabasesAsync();
            return View(databases);
        }

        [HttpPost]
        public async Task<IActionResult> Create(string database)
        {
            try
            {
                var fileName = await _backupService.BackupDatabaseAsync(database);

                return Json(new
                {
                    success = true,
                    message = "Backup created successfully.",
                    fileName
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpGet]
        public IActionResult Files()
        {
            try
            {
                var files = _backupService.GetBackupFiles();
                ViewBag.Error = null;
                return View(files);
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View(new List<string>());
            }
        }

        [HttpGet]
        public IActionResult Download(string file)
        {
            try
            {
                var fullPath = _backupService.GetBackupFullPath(file);

                return PhysicalFile(
                    fullPath,
                    "application/octet-stream",
                    Path.GetFileName(fullPath));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
    
}
