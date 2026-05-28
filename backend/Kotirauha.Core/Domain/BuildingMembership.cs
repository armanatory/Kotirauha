namespace Kotirauha.Core.Domain;

public class BuildingMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid BuildingId { get; set; }
    public Building? Building { get; set; }
    public MembershipRole Role { get; set; } = MembershipRole.Resident;
    public string? ApartmentNumber { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    // How this member came to belong: "code", "invite", "request", or "admin".
    public string? JoinedVia { get; set; }
    // The invitation link used, when JoinedVia == "invite".
    public Guid? InviteId { get; set; }
}
