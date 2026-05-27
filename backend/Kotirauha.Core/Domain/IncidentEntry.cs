namespace Kotirauha.Core.Domain;

public class IncidentEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BuildingId { get; set; }
    public Building? Building { get; set; }
    public Guid ReporterUserId { get; set; }
    public User? Reporter { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
    public IncidentCategory Category { get; set; }
    public string OriginalText { get; set; } = string.Empty;
    public string OriginalLanguage { get; set; } = string.Empty;
    public string? SubjectApartment { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EditedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public Guid? ArchivedByUserId { get; set; }

    public ICollection<IncidentTranslation> Translations { get; set; } = new List<IncidentTranslation>();
    public ICollection<IncidentAttachment> Attachments { get; set; } = new List<IncidentAttachment>();
    public ICollection<IncidentRevision> Revisions { get; set; } = new List<IncidentRevision>();
}
