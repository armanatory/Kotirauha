namespace Kotirauha.Core.Domain;

public class IncidentTranslation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EntryId { get; set; }
    public IncidentEntry? Entry { get; set; }
    public string TargetLanguage { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public TranslationStatus Status { get; set; } = TranslationStatus.Pending;
    public bool IsMachineGenerated { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
