namespace Kotirauha.Core.Domain;

public class Building
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string SharedLanguage { get; set; } = "fi";
    public string JoinCode { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<BuildingMembership> Memberships { get; set; } = new List<BuildingMembership>();
}
