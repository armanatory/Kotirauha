namespace Kotirauha.Core.Domain;

public class IncidentRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EntryId { get; set; }
    public IncidentEntry? Entry { get; set; }
    public Guid EditedByUserId { get; set; }
    public string PreviousText { get; set; } = string.Empty;
    public DateTimeOffset EditedAt { get; set; } = DateTimeOffset.UtcNow;
}
