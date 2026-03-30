namespace PedalAcrossCanada.Shared.DTOs.Teams;

public class TeamDto
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CaptainParticipantId { get; set; }
    public string? CaptainDisplayName { get; set; }
    public int MemberCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
