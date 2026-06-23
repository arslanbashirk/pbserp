namespace PBS.ERP.Shared.Auth;

public sealed class AuthUserDto
{
    public string Id { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Name { get; set; }

    public string? Cnic { get; set; }

    public string? Pno { get; set; }

    public IList<string> Roles { get; set; } = new List<string>();
}