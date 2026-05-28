namespace Kotirauha.Core.Domain;

// A single page visit, for first-party product analytics. We never store raw
// IPs: only a salted hash (to count unique visitors) and a coarse country code
// resolved offline. No third party sees this data.
public class VisitEvent
{
    public long Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public string? Referrer { get; set; }
    public string? Language { get; set; }
    public string? Country { get; set; }
    public string? VisitorHash { get; set; }
    public Guid? UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
