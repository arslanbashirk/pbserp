using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PBS.ERP.Infrastructure.Interfaces;
using PBS.ERP.Shared.Auth;
using PBS.ERP.Shared.Models;
using System.Security.Claims;

namespace PBS.ERP.Modules.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthApiController : ControllerBase
{
    private const string RefreshCookiePath = "/api/auth";

    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;

    public AuthApiController(
        IAuthService authService,
        IConfiguration configuration)
    {
        _authService = authService;
        _configuration = configuration;
    }

    // For ERP web login page.
    // Creates:
    // 1. MVC Identity cookie
    // 2. JWT access token
    // 3. Secure refresh-token cookie
    //
    // POST /api/auth/login
    // POST /api/authenticate
    [HttpPost("login")]
    [HttpPost("authenticate")]
    [HttpPost("~/api/authenticate")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _authService.LoginAsync(
            request,
            signInCookie: true,
            issueRefreshToken: true,
            ipAddress: GetIpAddress());

        if (!result.Succeeded)
            return ToFailedLoginResponse(result);

        AppendRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAtUtc);

        return Ok(new
        {
            success = true,
            token = result.Token,
            accessTokenExpiresAtUtc = result.AccessTokenExpiresAtUtc,
            user = result.User,
            redirectUrl = result.RedirectUrl
        });
    }

    // For mobile/external API clients.
    // Does not create MVC cookie.
    //
    // POST /api/auth/token
    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<IActionResult> TokenOnly([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _authService.LoginAsync(
            request,
            signInCookie: false,
            issueRefreshToken: true,
            ipAddress: GetIpAddress());

        if (!result.Succeeded)
            return ToFailedLoginResponse(result);

        return Ok(new
        {
            success = true,
            token = result.Token,
            accessTokenExpiresAtUtc = result.AccessTokenExpiresAtUtc,
            refreshToken = result.RefreshToken,
            refreshTokenExpiresAtUtc = result.RefreshTokenExpiresAtUtc,
            user = result.User
        });
    }

    // POST /api/auth/refresh
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest? request)
    {
        var refreshToken =
            request?.RefreshToken ??
            Request.Cookies[GetRefreshCookieName()];

        var result = await _authService.RefreshAsync(
            refreshToken ?? string.Empty,
            GetIpAddress());

        if (!result.Succeeded)
        {
            DeleteRefreshTokenCookie();

            return Unauthorized(new
            {
                success = false,
                message = result.Error ?? "Session expired. Please login again."
            });
        }

        AppendRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAtUtc);

        return Ok(new
        {
            success = true,
            token = result.Token,
            accessTokenExpiresAtUtc = result.AccessTokenExpiresAtUtc,
            user = result.User,
            redirectUrl = result.RedirectUrl
        });
    }

    // POST /api/auth/register
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _authService.RegisterAsync(
            request,
            signInCookie: true,
            issueRefreshToken: true,
            ipAddress: GetIpAddress());

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                success = false,
                message = result.Error ?? "Registration failed.",
                errors = result.Errors
            });
        }

        AppendRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAtUtc);

        return Ok(new
        {
            success = true,
            token = result.Token,
            accessTokenExpiresAtUtc = result.AccessTokenExpiresAtUtc,
            user = result.User,
            assignedRole = result.AssignedRole,
            redirectUrl = result.RedirectUrl
        });
    }

    // POST /api/auth/logout
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies[GetRefreshCookieName()];

        await _authService.LogoutAsync(
            refreshToken,
            User,
            signOutCookie: true,
            ipAddress: GetIpAddress());

        DeleteRefreshTokenCookie();

        return Ok(new
        {
            success = true,
            message = "Logged out successfully."
        });
    }

    // GET /api/auth/sessions
    [HttpGet("sessions")]
    [Authorize(AuthenticationSchemes = Constants.Identity_Application_Scheme)]
    public async Task<IActionResult> Sessions()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var sessions = await _authService.GetActiveSessionsAsync(userId);

        return Ok(new
        {
            success = true,
            sessions
        });
    }

    // POST /api/auth/revoke-all
    [HttpPost("revoke-all")]
    [Authorize(AuthenticationSchemes = Constants.Identity_Application_Scheme)]
    public async Task<IActionResult> RevokeAll()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        await _authService.RevokeAllUserSessionsAsync(
            userId,
            "User revoked all active sessions.",
            GetIpAddress());

        DeleteRefreshTokenCookie();

        return Ok(new
        {
            success = true,
            message = "All active sessions revoked."
        });
    }

    // GET /api/auth/ping
    [HttpGet("ping")]
    [AllowAnonymous]
    public IActionResult Ping()
    {
        return Ok(new
        {
            success = true,
            message = "PBS.ERP.Modules.Api is reachable.",
            time = DateTime.Now
        });
    }

    private IActionResult ToFailedLoginResponse(AuthResult result)
    {
        if (result.IsLockedOut)
        {
            return StatusCode(StatusCodes.Status423Locked, new
            {
                success = false,
                message = result.Error,
                lockoutMinutes = result.LockoutMinutes
            });
        }

        return Unauthorized(new
        {
            success = false,
            message = result.Error ?? "Invalid login attempt."
        });
    }

    [HttpPut("profile")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest model,
        CancellationToken cancellationToken)
    {
        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("uid");

        var response = await _authService.UpdateProfileAsync(
            userId,
            model,
            cancellationToken);

        if (response.Success)
        {
            return Ok(response);
        }

        if (response.Message == "Unauthorized")
        {
            return Unauthorized(response);
        }

        return BadRequest(response);
    }

    private void AppendRefreshTokenCookie(
        string? refreshToken,
        DateTimeOffset? expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || expiresAtUtc == null)
            return;

        Response.Cookies.Append(
            GetRefreshCookieName(),
            refreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = !HttpContext.Request.IsHttps ? false : true,
                SameSite = SameSiteMode.Strict,
                Expires = expiresAtUtc,
                Path = RefreshCookiePath
            });
    }

    private void DeleteRefreshTokenCookie()
    {
        Response.Cookies.Delete(
            GetRefreshCookieName(),
            new CookieOptions
            {
                Path = RefreshCookiePath
            });
    }

    private string GetRefreshCookieName()
    {
        return _configuration["Jwt:RefreshTokenCookieName"]
            ?? "PBS.ERP.RefreshToken";
    }

    private string? GetIpAddress()
    {
        if (Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            return forwardedFor
                .ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()
                ?.Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}

public sealed class RefreshTokenRequest
{
    public string? RefreshToken { get; set; }
}