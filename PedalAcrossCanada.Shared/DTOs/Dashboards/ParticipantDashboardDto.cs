using PedalAcrossCanada.Shared.DTOs.Activities;

namespace PedalAcrossCanada.Shared.DTOs.Dashboards;

public class ParticipantDashboardDto
{
    public decimal PersonalTotalKm { get; set; }
    public int PersonalRideCount { get; set; }
    public int? PersonalRank { get; set; }
    public decimal TeamTotalKm { get; set; }
    public int? TeamRank { get; set; }
    public decimal EventTotalKm { get; set; }
    public decimal RouteDistanceKm { get; set; }
    public decimal PercentComplete { get; set; }
    public string? NextMilestoneName { get; set; }
    public decimal? KmToNextMilestone { get; set; }
    public IReadOnlyList<ActivityDto> RecentActivities { get; set; } = [];
}
