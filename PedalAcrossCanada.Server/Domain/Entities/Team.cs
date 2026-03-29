namespace PedalAcrossCanada.Server.Domain.Entities;

public class Team
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CaptainParticipantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Event Event { get; set; } = null!;
    public Participant? Captain { get; set; }
    public ICollection<Participant> Participants { get; set; } = [];
    public ICollection<TeamHistory> TeamHistories { get; set; } = [];
}
