using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Shared.DTOs.Events;

public class EventDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal RouteDistanceKm { get; set; }
    public EventStatus Status { get; set; }
    public ManualEntryMode ManualEntryMode { get; set; }
    public bool StravaEnabled { get; set; }
    public string? BannerMessage { get; set; }
    public decimal MaxSingleRideKm { get; set; }
    public bool LeaderboardPublic { get; set; }
    public bool ShowTeamAverage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int MilestoneCount { get; set; }
    public int ParticipantCount { get; set; }
}
