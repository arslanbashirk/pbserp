using System.ComponentModel.DataAnnotations;

namespace PBS.ERP.Shared.Auth;

public sealed class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Cnic { get; set; } = string.Empty;

    public string? Gender { get; set; }

    public string? Mobile { get; set; }
}