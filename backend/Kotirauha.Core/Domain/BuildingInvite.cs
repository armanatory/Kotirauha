namespace Kotirauha.Core.Domain;

// A shareable invitation link. Anyone who opens the link and signs in is added
// to the building as a resident, without board approval, until the link's
// registration limit or time limit is reached (or the board revokes it).
public class BuildingInvite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BuildingId { get; set; }
    public Building? Building { get; set; }

    // URL-safe random key carried in the share link.
    public string Token { get; set; } = string.Empty;

    // Optional label so the board can tell links apart (e.g. "Spring 2026").
    public string? Title { get; set; }

    // Registration limit: how many people may join with this link (null = no limit).
    public int? MaxUses { get; set; }
    public int UsedCount { get; set; }

    // Time limit: when the link stops working (null = no expiry).
    public DateTimeOffset? ExpiresAt { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsUsable(DateTimeOffset now) =>
        RevokedAt is null
        && (ExpiresAt is null || ExpiresAt > now)
        && (MaxUses is null || UsedCount < MaxUses);
}
