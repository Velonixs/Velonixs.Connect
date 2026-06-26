namespace Velonixs.Connect.Persistence.Identity;

public sealed class AuthRefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public bool IsActive(DateTimeOffset now) =>
        RevokedAtUtc is null && ExpiresAtUtc > now;
}
