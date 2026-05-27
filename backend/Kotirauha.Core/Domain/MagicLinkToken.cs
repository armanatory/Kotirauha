namespace Kotirauha.Core.Domain;

// A one-time, short-lived email login token. We store only the hash of the
// token; the raw value lives only in the emailed link.
public class MagicLinkToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
