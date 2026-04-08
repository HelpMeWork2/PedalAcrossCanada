using PedalAcrossCanada.Shared.DTOs.Milestones;

namespace PedalAcrossCanada.Shared.DTOs.Dashboards;

public class EventDashboardDto
{
    public decimal TotalEventKm { get; set; }
    public decimal RouteDistanceKm { get; set; }
    public decimal PercentComplete { get; set; }
    public int TimesAroundRoute { get; set; }
    public string? NearestCity { get; set; }
    public int RegisteredParticipants { get; set; }
    public int ActiveParticipants { get; set; }
    public int TotalActivities { get; set; }
    public IReadOnlyList<MilestoneDto> CompletedMilestones { get; set; } = [];
    public MilestoneDto? NextMilestone { get; set; }
}
