namespace PBS.ERP.Shared.Auth;
public sealed class AuthResult
{
    public bool Succeeded { get; set; }

    public string? Error { get; set; }

    public IEnumerable<string> Errors { get; set; } = new List<string>();

    public bool IsLockedOut { get; set; }

    public double? LockoutMinutes { get; set; }

    public string? Token { get; set; }

    public DateTimeOffset? AccessTokenExpiresAtUtc { get; set; }

    public string? RefreshToken { get; set; }

    public DateTimeOffset? RefreshTokenExpiresAtUtc { get; set; }

    public string? RedirectUrl { get; set; }

    public AuthUserDto? User { get; set; }

    public string? AssignedRole { get; set; }

    public static AuthResult Fail(string error, IEnumerable<string>? errors = null)
    {
        return new AuthResult
        {
            Succeeded = false,
            Error = error,
            Errors = errors ?? new List<string>()
        };
    }
}