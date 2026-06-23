using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PBS.ERP.Shared.Identity;

namespace PBS.ERP.Modules.Security.Controllers
{
    [Authorize(Roles = "Admin,Super,Root")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalUsers = (await _userManager.Users.ToListAsync()).Count;
            ViewBag.TotalRoles = (await _roleManager.Roles.ToListAsync()).Count;
            return View();
        }
    }
}