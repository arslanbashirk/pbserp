using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PBS.ERP.Modules.Security.Models;
using PBS.ERP.Shared.Identity;

namespace PBS.ERP.Modules.Security.Controllers
{
    [Route("User")]
    [Authorize(Roles = "Super,Root")]
    [ApiExplorerSettings(IgnoreApi = true)]

    public class UserController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UserController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync(); // Get the list of users

            var usersWithRoles = new List<UserWithRolesViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user); // Fetch roles for each user
                usersWithRoles.Add(new UserWithRolesViewModel
                {
                    User = user,
                    Roles = roles
                });
            }

            return View(usersWithRoles); // Pass the users with their roles to the view
        }

        [HttpPost("Reset")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reset(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest("User ID is required.");

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return NotFound();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var newPassword = "Temp@12345";

            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (result.Succeeded)
            {
                TempData["Success"] = "Password reset successfully.";
            }
            else
            {
                TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction("Index"); // back to user list
        }
    }

    
}