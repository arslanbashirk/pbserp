using System.ComponentModel.DataAnnotations;
using PBS.ERP.Shared.Identity;

namespace PBS.ERP.Infrastructure.Tokens;

public sealed class UserRefreshToken
{
    public long Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    [Required]
    [MaxLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string JwtId { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(100)]
    public string? CreatedByIp { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    [MaxLength(100)]
    public string? RevokedByIp { get; set; }

    [MaxLength(128)]
    public string? ReplacedByTokenHash { get; set; }

    [MaxLength(250)]
    public string? ReasonRevoked { get; set; }

    public bool IsRevoked => RevokedAtUtc.HasValue;

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAtUtc;

    public bool IsActive => !IsRevoked && !IsExpired;
}