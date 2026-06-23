using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PBS.ERP.Infrastructure.Services;
using PBS.ERP.Shared.Auth;
using PBS.ERP.Shared.Identity;

namespace PBS.ERP.Modules.Core.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly SecurityLogService _securityLogService;

    public AccountController(
        IAuthService authService,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        SecurityLogService securityLogService)
    {
        _authService = authService;
        _userManager = userManager;
        _signInManager = signInManager;
        _securityLogService = securityLogService;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        if (!ModelState.IsValid)
            return View();

        var result = await _authService.LoginAsync(request,signInCookie: true);

        if (!result.Succeeded)
        {
            ModelState.AddModelError("", result.Error ?? "Invalid login attempt.");
            return View();
        }

        return RedirectByRole(result.User?.Roles ?? new List<string>());
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return View();

        var result = await _authService.RegisterAsync(request,signInCookie: true);

        if (!result.Succeeded)
        {
            ModelState.AddModelError("", result.Error ?? "Registration failed.");

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error);

            return View();
        }

        if (result.User?.Roles.Contains("Staff") == true)
            return RedirectToAction("Index", "Staff");

        return RedirectToAction("Anonymous", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Forgot()
    {
        return View();
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var user = await _userManager.GetUserAsync(User);

        await _signInManager.SignOutAsync();

        await _securityLogService.LogLogout(user?.Id);

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    private IActionResult RedirectByRole(IEnumerable<string> roles)
    {
        var roleList = roles.ToList();

        if (roleList.Contains("Root") ||
            roleList.Contains("Super") ||
            roleList.Contains("Admin"))
        {
            return RedirectToAction("Index", "Super");
        }

        if (roleList.Contains("Manager"))
            return RedirectToAction("Index", "Manager");

        if (roleList.Contains("Staff"))
            return RedirectToAction("Index", "Staff");

        return RedirectToAction("Anonymous", "Home");
    }

    // Keep your existing UpdateProfile action here if it is used by Razor/MVC pages.
}