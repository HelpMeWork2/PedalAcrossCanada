namespace PedalAcrossCanada.Server.Domain.Entities;

public class BadgeAward
{
    public Guid Id { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid BadgeId { get; set; }
    public Guid EventId { get; set; }
    public DateTime AwardedAt { get; set; }
    public string? AwardedBy { get; set; }
    public bool IsManual { get; set; }

    public Participant Participant { get; set; } = null!;
    public Badge Badge { get; set; } = null!;
    public Event Event { get; set; } = null!;
}
