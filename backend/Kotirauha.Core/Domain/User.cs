namespace Kotirauha.Core.Domain;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PreferredLanguage { get; set; } = "en";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;
    public bool IsPlatformAdmin { get; set; }

    public ICollection<BuildingMembership> Memberships { get; set; } = new List<BuildingMembership>();
}
