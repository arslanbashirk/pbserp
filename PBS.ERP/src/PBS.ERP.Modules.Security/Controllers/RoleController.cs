using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PBS.ERP.Modules.Security.Controllers;
using PBS.ERP.Shared.Identity;

namespace   PBS.ERP.Modules.Security.Controllers
{
    [Route("Role")]
    [Authorize(Roles = "Super,Root")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class RoleController : ERPController
    {
        private readonly RoleManager<ApplicationRole> _roleManager;

        public RoleController(RoleManager<ApplicationRole> roleManager)
        {
            _roleManager = roleManager;
        }

        [HttpGet]
        public IActionResult Index()
        {
            // MVC view, automatically redirects to login if not authenticated
            return View();
        }

        [HttpGet("List")]
        public async Task<IActionResult> List()
        {
            // Only roles that are not deleted
            var roles = await _roleManager.Roles
                .Where(r => !r.IsDeleted)
                .OrderBy(r => r.PriorityLevel)
                .ToListAsync();

            return Json(roles.Select(r => new
            {
                r.Id,
                r.UID,
                r.Name,
                r.Description,
                r.PriorityLevel,
                r.IsActive,
                r.IsDefault
            })); // Return safe fields only
        }

        [HttpGet("Get")]
        public async Task<IActionResult> Get(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return BadRequest(new { error = "UID required" });

            var role = await _roleManager.Roles
                .FirstOrDefaultAsync(r => r.UID == uid && !r.IsDeleted);

            if (role == null)
                return Json(new { error = "Role not found" });

            return Json(new
            {
                role.UID,
                role.Id,
                role.Name,
                role.Description,
                role.PriorityLevel,
                role.IsActive,
                role.IsDefault,
                role.ParentRoleId
            });
        }

        [HttpPost("Insert")]
        public async Task<IActionResult> Insert([FromBody] ApplicationRole model)
        {
            ModelState.Remove("RowVersion");
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (model == null)
                return Json(new { error = "No data received" });

            // Prevent overposting: only allow specific fields
            model.Id = Guid.NewGuid().ToString();
            model.UID = Guid.NewGuid().ToString();
            model.CreatedTime = DateTime.UtcNow;
            model.CreatedBy = User?.Identity?.Name ?? "system";
            model.NormalizedName = model.Name?.Trim().ToUpperInvariant();

            model.Name = model.Name?.Trim();
            model.Description = model.Description?.Trim();

            var result = await _roleManager.CreateAsync(model);
            if (!result.Succeeded)
                return Json(new { error = result.Errors.Select(e => e.Description) });

            return Json(new { success = true });
        }

        [HttpPost("Update")]
        public async Task<IActionResult> Update(string uid, [FromBody] ApplicationRole model)
        {
            if (model == null)
                return Json(new { error = "No data received" });

            if (string.IsNullOrWhiteSpace(uid))
                return BadRequest(new { error = "UID required" });

            var role = await _roleManager.Roles
                .FirstOrDefaultAsync(r => r.UID == uid && !r.IsDeleted);

            if (role == null)
                return Json(new { error = "Role not found" });

            // Sanitize input
            role.Name = model.Name?.Trim();
            role.Description = model.Description?.Trim();
            role.PriorityLevel = model.PriorityLevel;
            role.IsActive = model.IsActive;
            role.IsDefault = model.IsDefault;
            role.ParentRoleId = string.IsNullOrEmpty(model.ParentRoleId) ? null : model.ParentRoleId;
            role.NormalizedName = role.Name?.ToUpperInvariant();

            role.ModifiedBy = User?.Identity?.Name ?? "system";
            role.ModifiedTime = DateTime.UtcNow;

            var result = await _roleManager.UpdateAsync(role);
            if (!result.Succeeded)
                return Json(new { error = result.Errors.Select(e => e.Description) });

            return Json(new { success = true });
        }

        [HttpPost("Delete")]
        public async Task<IActionResult> Delete(string uid)
        {
            if (string.IsNullOrEmpty(uid))
                return Json(new { error = "UID not provided" });

            var role = await _roleManager.Roles
                .FirstOrDefaultAsync(r => r.UID == uid && !r.IsDeleted);

            if (role == null)
                return Json(new { error = "Role not found" });

            if (role.IsSystemRole)
                return Json(new { error = "System roles cannot be deleted" });

            // Soft delete for safety
            role.IsDeleted = true;
            role.DeletedBy = User?.Identity?.Name ?? "system";
            role.DeletedTime = DateTime.UtcNow;

            var result = await _roleManager.UpdateAsync(role);
            if (!result.Succeeded)
                return Json(new { error = result.Errors.Select(e => e.Description) });

            return Json(new { success = true });
        }

        [HttpGet("ParentRoles")]
        public async Task<IActionResult> ParentRoles()
        {
            // Only return necessary fields
            var roles = await _roleManager.Roles
                .Where(r => !r.IsDeleted)
                .Select(r => new
                {
                    r.Id,
                    r.Name
                })
                .ToListAsync();

            return Json(roles);
        }
    }
}