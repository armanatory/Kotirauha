namespace Kotirauha.Core.Domain;

// A one-time, short-lived email login token. We store only the hash of the
// token; the raw value lives only in the emailed link.
public class MagicLinkToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    // Hash of the long token embedded in the clickable link.
    public string TokenHash { get; set; } = string.Empty;
    // Hash of the short 6-digit code the user can type into the app instead.
    public string CodeHash { get; set; } = string.Empty;
    // Failed code attempts; the row is burned after too many to bound brute force.
    public int Attempts { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
