using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PBS.ERP.Infrastructure;
using PBS.ERP.Shared.Identity;

namespace PBS.ERP.Modules.Security.Controllers
{
    [Route("UserRole")]
    [Authorize(Roles = "Super,Root")] // Controller-level security
    [ApiExplorerSettings(IgnoreApi = true)]
    public class UserRoleController : ERPController // inherit ERPController for login/role handling
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public UserRoleController(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        
        [HttpGet("Users")]
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users
                .Where(u => !u.IsDeleted)
                .Select(u => new
                {
                    u.Id,
                    u.UID,
                    u.UserName,
                    u.Email
                })
                .ToListAsync();

            return Json(users);
        }

       
        [HttpGet("Roles")]
        public async Task<IActionResult> Roles()
        {
            var roles = await _roleManager.Roles
                .Where(r => !r.IsDeleted)
                .Select(r => new
                {
                    r.Id,
                    r.UID,
                    r.Name
                })
                .ToListAsync();

            return Json(roles);
        }

        
        [HttpGet("Assignments")]
        public async Task<IActionResult> Assignments()
        {
            var assignments = await _context.UserRoles.ToListAsync();

            var users = await _userManager.Users
                .Where(u => !u.IsDeleted)
                .Select(u => new { u.Id, u.UID, u.UserName })
                .ToListAsync();

            var roles = await _roleManager.Roles
                .Where(r => !r.IsDeleted)
                .Select(r => new { r.Id, r.UID, r.Name })
                .ToListAsync();

            var result = assignments
            .Where(ur => !ur.IsDeleted) 
            .Select(ur => new
            {
                ur.UID,
                ur.UserId,
                UserName = users.FirstOrDefault(u => u.Id == ur.UserId)?.UserName ?? "",
                ur.RoleId,
                RoleName = roles.FirstOrDefault(r => r.Id == ur.RoleId)?.Name ?? "",
                ur.IsActive,
                ur.CreatedTime,
                ur.CreatedBy
            })
            .ToList();

            return Json(result);
        }


        [HttpGet("RolesForUser")]
        public async Task<IActionResult> RolesForUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return Json(new { error = "Invalid user" });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Json(new { error = "User not found" });

            var allRoles = await _roleManager.Roles
                .Where(r => !r.IsDeleted)
                .ToListAsync();

            var userRoleAssignments = await _context.UserRoles
                .Where(ur => ur.UserId == userId && !ur.IsDeleted)
                .ToListAsync();

            var roles = allRoles.Select(r => new RoleSelection
            {
                RoleId = r.Id,
                RoleName = r.Name,
                IsSelected = userRoleAssignments.Any(ura => ura.RoleId == r.Id)
            }).ToList();

            return Json(roles);
        }


        [HttpPost("Assign")]
        public async Task<IActionResult> Assign([FromBody] UserRoleAssignmentModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(model.UserId))
                return BadRequest(new { error = "Invalid user" });

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
                return BadRequest(new { error = "User not found" });

            var selectedRoles = (model.SelectedRoles ?? new List<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct()
                .ToList();

            // Validate roles exist
            var validRoles = await _roleManager.Roles
                .Select(r => r.Name)
                .ToListAsync();

            selectedRoles = selectedRoles.Intersect(validRoles).ToList();

            var currentRoles = await _userManager.GetRolesAsync(user);

            // Add roles
            var rolesToAdd = selectedRoles.Except(currentRoles).ToList();
            if (rolesToAdd.Any())
            {
                var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
                if (!addResult.Succeeded)
                    return BadRequest(new
                    {
                        error = "Failed to add roles",
                        details = addResult.Errors.Select(e => e.Description).ToList()
                    });
            }

            // Remove roles
            var rolesToRemove = currentRoles.Except(selectedRoles).ToList();
            if (rolesToRemove.Any())
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                if (!removeResult.Succeeded)
                    return BadRequest(new
                    {
                        error = "Failed to remove roles",
                        details = removeResult.Errors.Select(e => e.Description).ToList()
                    });
            }

            return Ok(new { success = true });
        }

        [HttpPost("DeleteAssignment")]
        public async Task<IActionResult> DeleteAssignment(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return Json(new { error = "UID required" });

            var assignment = await _context.UserRoles.FirstOrDefaultAsync(ur => ur.UID == uid);
            if (assignment == null)
                return Json(new { error = "Assignment not found" });

            // Option 1: Soft delete
            assignment.IsActive = false;
            assignment.IsDeleted = true;
            assignment.DeletedBy = User?.Identity?.Name ?? "system";
            assignment.DeletedTime = DateTime.UtcNow;
            _context.UserRoles.Update(assignment);

            // Option 2: Hard delete (if soft delete not desired)
            // _context.UserRoles.Remove(assignment);

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }

    
    public class UserRoleAssignmentModel
    {
        public string UserId { get; set; } = string.Empty;
        public List<string> SelectedRoles { get; set; } = new();
    }

    public class RoleSelection
    {
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = false;
    }
}