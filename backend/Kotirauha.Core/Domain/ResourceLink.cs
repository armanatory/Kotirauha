namespace Kotirauha.Core.Domain;

// An admin-curated reference link shown to residents (e.g. a link to the Finnish
// Housing Companies Act, house rules, or other "good to know" guidance).
public class ResourceLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    // null = platform-wide link (e.g. the Housing Companies Act), managed by the
    // platform admin. Otherwise the link belongs to a building and its board
    // manages it; it is shown only to that building's residents.
    public Guid? BuildingId { get; set; }
    public Building? Building { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
