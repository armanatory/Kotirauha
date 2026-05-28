namespace Kotirauha.Core.Domain;

// An admin-curated reference link shown to residents (e.g. a link to the Finnish
// Housing Companies Act, house rules, or other "good to know" guidance).
public class ResourceLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
