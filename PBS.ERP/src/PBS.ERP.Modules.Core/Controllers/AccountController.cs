using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PBS.ERP.Infrastructure.Interfaces;
using PBS.ERP.Infrastructure.Services;
using PBS.ERP.Shared.Auth;
using PBS.ERP.Shared.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PBS.ERP.Modules.Core.Controllers 
{

    public class AccountController : Controller
    {
        private readonly IAuthService _authService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly SecurityLogService _securityLogService;
        private readonly IConfiguration _configuration;


        public AccountController(
            IAuthService authService,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            SecurityLogService securityLogService, IConfiguration configuration)
        {
            _configuration = configuration;
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

            var result = await _authService.LoginAsync(request, signInCookie: true);

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

            var result = await _authService.RegisterAsync(request, signInCookie: true);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("", result.Error ?? "Registration failed.");

                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error);

                return View();
            }

            return RedirectToAction("Login", "Account");
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

        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SignInFromApiToken([FromBody] SignInFromApiTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Token is required."
                });
            }

            ClaimsPrincipal principal;

            try
            {
                principal = ValidateJwtToken(request.Token);
            }
            catch
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid or expired token."
                });
            }

            var claims = principal.Claims.ToList();

            var identity = new ClaimsIdentity(
                claims,
                IdentityConstants.ApplicationScheme,
                ClaimTypes.Name,
                ClaimTypes.Role);

            var localPrincipal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = request.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                IdentityConstants.ApplicationScheme,
                localPrincipal,
                authProperties);

            return Ok(new
            {
                success = true,
                message = "Local MVC cookie created."
            });
        }

        private ClaimsPrincipal ValidateJwtToken(string token)
        {
            var jwtKey = _configuration["Jwt:Key"];
            var issuer = _configuration["Jwt:Issuer"];
            var audience = _configuration["Jwt:Audience"];

            if (string.IsNullOrWhiteSpace(jwtKey))
                throw new InvalidOperationException("JWT key is missing.");

            var tokenHandler = new JwtSecurityTokenHandler();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey)),

                ValidateIssuer = true,
                ValidIssuer = issuer,

                ValidateAudience = true,
                ValidAudience = audience,

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),

                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role
            };

            var principal = tokenHandler.ValidateToken(
                token,
                validationParameters,
                out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(
                    SecurityAlgorithms.HmacSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new SecurityTokenException("Invalid token algorithm.");
            }

            return principal;
        }
        // Keep your existing UpdateProfile action here if it is used by Razor/MVC pages.
    }
    public sealed class SignInFromApiTokenRequest
    {
        public string Token { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
    }
}