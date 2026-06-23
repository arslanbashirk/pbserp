namespace PBS.ERP.Shared.Auth;

public sealed class UserSessionDto
{
    public long Id { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public string? CreatedByIp { get; set; }

    public bool IsActive { get; set; }
}