namespace PedalAcrossCanada.Server.Domain.Entities;

public class TeamHistory
{
    public Guid Id { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid TeamId { get; set; }
    public DateTime EffectiveFrom { get; set; }

    public Participant Participant { get; set; } = null!;
    public Team Team { get; set; } = null!;
}
