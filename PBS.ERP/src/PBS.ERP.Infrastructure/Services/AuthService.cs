using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PBS.ERP.Infrastructure.Tokens;
using PBS.ERP.Shared.Auth;
using PBS.ERP.Shared.Identity;

namespace PBS.ERP.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly SecurityLogService _securityLogService;
    private readonly IConfiguration _configuration;

    public AuthService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        SecurityLogService securityLogService,
        IConfiguration configuration)
    {
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
        _securityLogService = securityLogService;
        _configuration = configuration;
    }

    public async Task<AuthResult> LoginAsync(
        LoginRequest request,
        bool signInCookie = false,
        bool issueRefreshToken = true,
        string? ipAddress = null)
    {
        if (request == null)
            return AuthResult.Fail("Invalid login request.");

        var username = request.Username?.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(request.Password))
            return AuthResult.Fail("Login ID and password are required.");

        var user = await _userManager.Users
            .FirstOrDefaultAsync(u =>
                u.Email == username ||
                u.CNIC == username ||
                u.PNO == username);

        if (user == null)
        {
            await _securityLogService.LogLoginAttempt(username, false, null);
            return AuthResult.Fail("User not found.");
        }

        if (user.IsDeleted || !user.IsActive)
        {
            await _securityLogService.LogLoginAttempt(username, false, user.Id);
            return AuthResult.Fail("User account is inactive or deleted.");
        }

        SignInResult signInResult;

        if (signInCookie)
        {
            signInResult = await _signInManager.PasswordSignInAsync(
                user.UserName!,
                request.Password,
                request.RememberMe,
                lockoutOnFailure: true);
        }
        else
        {
            signInResult = await _signInManager.CheckPasswordSignInAsync(
                user,
                request.Password,
                lockoutOnFailure: true);
        }

        await _securityLogService.LogLoginAttempt(username, signInResult.Succeeded, user.Id);

        if (!signInResult.Succeeded)
        {
            if (signInResult.IsLockedOut)
            {
                var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);

                double? minutes = null;

                if (lockoutEnd.HasValue)
                {
                    minutes = Math.Max(
                        0,
                        Math.Ceiling((lockoutEnd.Value - DateTimeOffset.UtcNow).TotalMinutes));
                }

                return new AuthResult
                {
                    Succeeded = false,
                    IsLockedOut = true,
                    LockoutMinutes = minutes,
                    Error = minutes.HasValue
                        ? $"Account locked. Try again in {minutes} minutes."
                        : "Account locked."
                };
            }

            if (signInResult.IsNotAllowed)
                return AuthResult.Fail("User is not allowed to sign in.");

            if (signInResult.RequiresTwoFactor)
                return AuthResult.Fail("Two-factor authentication is required.");

            return AuthResult.Fail("Invalid credentials.");
        }

        return await BuildSuccessResultAsync(
            user,
            issueRefreshToken,
            request.RememberMe,
            ipAddress);
    }

    public async Task<AuthResult> RegisterAsync(
        RegisterRequest request,
        bool signInCookie = false,
        bool issueRefreshToken = true,
        string? ipAddress = null)
    {
        if (request == null)
            return AuthResult.Fail("Invalid registration request.");

        var email = request.Email?.Trim();
        var password = request.Password;
        var name = request.Name?.Trim();
        var cnic = request.Cnic?.Trim();

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(cnic))
        {
            return AuthResult.Fail("CNIC, Name, Email, and Password are required.");
        }

        var existingEmail = await _userManager.FindByEmailAsync(email);

        if (existingEmail != null)
            return AuthResult.Fail("Email is already registered.");

        var existingCnic = await _userManager.Users.AnyAsync(u => u.CNIC == cnic);

        if (existingCnic)
            return AuthResult.Fail("CNIC is already registered.");

        var uid = Guid.NewGuid().ToString();

        var assignedRole = await TryLinkWithErpHrStaffAsync(uid, cnic);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            Name = name,
            CNIC = cnic,
            Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim(),
            Mobile = string.IsNullOrWhiteSpace(request.Mobile) ? null : request.Mobile.Trim(),
            PNO = null,
            EmailConfirmed = true,
            IsActive = true,
            IsDeleted = false,
            CreatedTime = DateTime.UtcNow,
            UID = uid
        };

        var createResult = await _userManager.CreateAsync(user, password);

        if (!createResult.Succeeded)
        {
            return AuthResult.Fail(
                "User registration failed.",
                createResult.Errors.Select(e => e.Description));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, assignedRole);

        if (!roleResult.Succeeded)
        {
            return AuthResult.Fail(
                "User created, but role assignment failed.",
                roleResult.Errors.Select(e => e.Description));
        }

        await _securityLogService.LogAsync("UserRegistration", user.Id);

        if (signInCookie)
            await _signInManager.SignInAsync(user, isPersistent: false);

        return await BuildSuccessResultAsync(
            user,
            issueRefreshToken,
            rememberMe: false,
            ipAddress,
            assignedRole);
    }

    public async Task<AuthResult> RefreshAsync(
        string refreshToken,
        string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return AuthResult.Fail("Refresh token is missing.");

        var tokenHash = HashToken(refreshToken);

        var storedToken = await _context.UserRefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

        if (storedToken == null)
            return AuthResult.Fail("Invalid session. Please login again.");

        if (!storedToken.IsActive)
        {
            if (!string.IsNullOrWhiteSpace(storedToken.UserId))
            {
                await RevokeAllUserSessionsAsync(
                    storedToken.UserId,
                    "Refresh token reuse or expired token attempted.",
                    ipAddress);
            }

            return AuthResult.Fail("Session expired. Please login again.");
        }

        var user = storedToken.User;

        if (user == null || user.IsDeleted || !user.IsActive)
            return AuthResult.Fail("User account is inactive or deleted.");

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var roles = await _userManager.GetRolesAsync(user);
            var jwt = GenerateJwtToken(user, roles);

            var newRefreshToken = GenerateSecureRefreshToken();
            var newRefreshTokenHash = HashToken(newRefreshToken);
            var refreshExpiry = GetRefreshTokenExpiry(rememberMe: false);

            storedToken.RevokedAtUtc = DateTimeOffset.UtcNow;
            storedToken.RevokedByIp = ipAddress;
            storedToken.ReasonRevoked = "Rotated by refresh endpoint.";
            storedToken.ReplacedByTokenHash = newRefreshTokenHash;

            var newStoredToken = new UserRefreshToken
            {
                UserId = user.Id,
                TokenHash = newRefreshTokenHash,
                JwtId = jwt.JwtId,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                ExpiresAtUtc = refreshExpiry,
                CreatedByIp = ipAddress
            };

            _context.UserRefreshTokens.Add(newStoredToken);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new AuthResult
            {
                Succeeded = true,
                Token = jwt.Token,
                AccessTokenExpiresAtUtc = jwt.ExpiresAtUtc,
                RefreshToken = newRefreshToken,
                RefreshTokenExpiresAtUtc = refreshExpiry,
                RedirectUrl = GetRedirectUrl(roles),
                User = new AuthUserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    Name = user.Name,
                    Cnic = user.CNIC,
                    Pno = user.PNO,
                    Roles = roles
                }
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task LogoutAsync(
        string? refreshToken,
        ClaimsPrincipal? currentUser,
        bool signOutCookie = false,
        string? ipAddress = null)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var tokenHash = HashToken(refreshToken);

            var storedToken = await _context.UserRefreshTokens
                .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

            if (storedToken != null && storedToken.IsActive)
            {
                storedToken.RevokedAtUtc = DateTimeOffset.UtcNow;
                storedToken.RevokedByIp = ipAddress;
                storedToken.ReasonRevoked = "User logout.";

                await _context.SaveChangesAsync();
            }
        }

        if (signOutCookie)
        {
            var user = currentUser == null
                ? null
                : await _userManager.GetUserAsync(currentUser);

            await _signInManager.SignOutAsync();
            await _securityLogService.LogLogout(user?.Id);
        }
    }

    public async Task RevokeAllUserSessionsAsync(
        string userId,
        string reason,
        string? ipAddress = null)
    {
        var activeTokens = await _context.UserRefreshTokens
            .Where(x =>
                x.UserId == userId &&
                x.RevokedAtUtc == null &&
                x.ExpiresAtUtc > DateTimeOffset.UtcNow)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = DateTimeOffset.UtcNow;
            token.RevokedByIp = ipAddress;
            token.ReasonRevoked = reason;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<UserSessionDto>> GetActiveSessionsAsync(string userId)
    {
        return await _context.UserRefreshTokens
            .Where(x =>
                x.UserId == userId &&
                x.RevokedAtUtc == null &&
                x.ExpiresAtUtc > DateTimeOffset.UtcNow)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new UserSessionDto
            {
                Id = x.Id,
                CreatedAtUtc = x.CreatedAtUtc,
                ExpiresAtUtc = x.ExpiresAtUtc,
                CreatedByIp = x.CreatedByIp,
                IsActive = true
            })
            .ToListAsync();
    }

    private async Task<AuthResult> BuildSuccessResultAsync(
        ApplicationUser user,
        bool issueRefreshToken,
        bool rememberMe,
        string? ipAddress,
        string? assignedRole = null)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var jwt = GenerateJwtToken(user, roles);

        string? refreshToken = null;
        DateTimeOffset? refreshExpiry = null;

        if (issueRefreshToken)
        {
            refreshToken = GenerateSecureRefreshToken();
            refreshExpiry = GetRefreshTokenExpiry(rememberMe);

            var storedRefreshToken = new UserRefreshToken
            {
                UserId = user.Id,
                TokenHash = HashToken(refreshToken),
                JwtId = jwt.JwtId,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                ExpiresAtUtc = refreshExpiry.Value,
                CreatedByIp = ipAddress
            };

            _context.UserRefreshTokens.Add(storedRefreshToken);
            await _context.SaveChangesAsync();
        }

        return new AuthResult
        {
            Succeeded = true,
            Token = jwt.Token,
            AccessTokenExpiresAtUtc = jwt.ExpiresAtUtc,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAtUtc = refreshExpiry,
            RedirectUrl = GetRedirectUrl(roles),
            AssignedRole = assignedRole,
            User = new AuthUserDto
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                Cnic = user.CNIC,
                Pno = user.PNO,
                Roles = roles
            }
        };
    }

    private JwtTokenResult GenerateJwtToken(
        ApplicationUser user,
        IList<string> roles)
    {
        var jwtSection = _configuration.GetSection("Jwt");

        var keyValue = jwtSection["Key"]
            ?? throw new InvalidOperationException("Jwt:Key is missing.");

        var issuer = jwtSection["Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer is missing.");

        var audience = jwtSection["Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience is missing.");

        var accessMinutes = Convert.ToDouble(jwtSection["AccessTokenMinutes"] ?? jwtSection["ExpiryMinutes"] ?? "15");

        var jwtId = Guid.NewGuid().ToString("N");
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(accessMinutes);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Jti, jwtId),
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id),
            new Claim("UID", user.UID ?? string.Empty),
            new Claim("CNIC", user.CNIC ?? string.Empty)
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyValue));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new JwtTokenResult
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            JwtId = jwtId,
            ExpiresAtUtc = expiresAt
        };
    }

    private DateTimeOffset GetRefreshTokenExpiry(bool rememberMe)
    {
        var jwtSection = _configuration.GetSection("Jwt");

        var normalDays = Convert.ToDouble(jwtSection["RefreshTokenDays"] ?? "1");
        var rememberMeDays = Convert.ToDouble(jwtSection["RefreshTokenRememberMeDays"] ?? "30");

        return DateTimeOffset.UtcNow.AddDays(rememberMe ? rememberMeDays : normalDays);
    }

    private static string GenerateSecureRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return WebEncoders.Base64UrlEncode(bytes);
    }

    private static string GetRedirectUrl(IList<string>? roles)
    {
        roles ??= new List<string>();

        if (roles.Contains("Root") ||
            roles.Contains("Super") ||
            roles.Contains("Admin"))
        {
            return "/Super/Index";
        }

        if (roles.Contains("Manager"))
            return "/Manager/Index";

        if (roles.Contains("Staff"))
            return "/Staff/Index";

        return "/Home/Anonymous";
    }

    private async Task<string> TryLinkWithErpHrStaffAsync(string uid, string cnic)
    {
        var assignedRole = "User";

        try
        {
            var connectionString = _configuration.GetConnectionString("ERPHRConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
                return assignedRole;

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            const string query = @"
                UPDATE Staff
                SET [User] = @UID
                WHERE CNIC = @CNIC";

            await conn.ExecuteAsync(query, new
            {
                UID = uid,
                CNIC = cnic
            });

            const string checkQuery = @"
                SELECT COUNT(1)
                FROM Staff
                WHERE CNIC = @CNIC
                  AND [User] = @UID";

            var linked = await conn.ExecuteScalarAsync<int>(checkQuery, new
            {
                UID = uid,
                CNIC = cnic
            });

            if (linked > 0)
                assignedRole = "Staff";
        }
        catch
        {
            assignedRole = "User";
        }

        return assignedRole;
    }

    private sealed class JwtTokenResult
    {
        public string Token { get; set; } = string.Empty;

        public string JwtId { get; set; } = string.Empty;

        public DateTimeOffset ExpiresAtUtc { get; set; }
    }
}