
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBS.ERP.Infrastructure.Services;
using PBS.ERP.Shared.Auth;

namespace PBS.ERP.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthApiController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthApiController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [HttpPost("/api/auth/login")] // keeps your old API route working
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _authService.LoginAsync(
            request,
            signInCookie: false);

        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                return StatusCode(423, new
                {
                    message = result.Error,
                    lockoutMinutes = result.LockoutMinutes
                });
            }

            return Unauthorized(new
            {
                message = result.Error
            });
        }

        return Ok(new
        {
            token = result.Token,
            user = result.User
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _authService.RegisterAsync(
            request,
            signInCookie: false);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = result.Error,
                errors = result.Errors
            });
        }

        return Ok(new
        {
            token = result.Token,
            user = result.User,
            assignedRole = result.AssignedRole
        });
    }
}