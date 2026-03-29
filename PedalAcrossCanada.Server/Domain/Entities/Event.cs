using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Domain.Entities;

public class Event
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

    public ICollection<Milestone> Milestones { get; set; } = [];
    public ICollection<Team> Teams { get; set; } = [];
    public ICollection<Participant> Participants { get; set; } = [];
    public ICollection<Activity> Activities { get; set; } = [];
}
