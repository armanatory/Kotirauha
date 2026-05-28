namespace Kotirauha.Core.Domain;

// A resident's request to join a building. Board members approve it, which
// creates the membership. The instant join-code path bypasses this entirely.
public class BuildingJoinRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BuildingId { get; set; }
    public Building? Building { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string? ApartmentNumber { get; set; }
    public JoinRequestStatus Status { get; set; } = JoinRequestStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DecidedAt { get; set; }
    public Guid? DecidedByUserId { get; set; }
}
