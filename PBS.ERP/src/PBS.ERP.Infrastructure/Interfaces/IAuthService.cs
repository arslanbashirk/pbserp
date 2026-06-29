using System.Security.Claims;
using PBS.ERP.Shared.Auth;
using PBS.ERP.Shared.Models;

namespace PBS.ERP.Infrastructure.Interfaces;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(
        LoginRequest request,
        bool signInCookie = false,
        bool issueRefreshToken = true,
        string? ipAddress = null);

    Task<AuthResult> RegisterAsync(
        RegisterRequest request,
        bool signInCookie = false,
        bool issueRefreshToken = true,
        string? ipAddress = null);

    Task<AuthResult> RefreshAsync(
        string refreshToken,
        string? ipAddress = null);

    Task LogoutAsync(
        string? refreshToken,
        ClaimsPrincipal? currentUser,
        bool signOutCookie = false,
        string? ipAddress = null);

    Task RevokeAllUserSessionsAsync(
        string userId,
        string reason,
        string? ipAddress = null);

    Task<ApiResponse<object>> UpdateProfileAsync(
        string? userId,
        UpdateProfileRequest model,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserSessionDto>> GetActiveSessionsAsync(string userId);
}