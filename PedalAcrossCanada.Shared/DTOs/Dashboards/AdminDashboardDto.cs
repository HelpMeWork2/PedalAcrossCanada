namespace PedalAcrossCanada.Shared.DTOs.Dashboards;

public class AdminDashboardDto
{
    public decimal TotalEventKm { get; set; }
    public decimal RouteDistanceKm { get; set; }
    public decimal PercentComplete { get; set; }
    public int RegisteredParticipants { get; set; }
    public int ActiveParticipants { get; set; }
    public int TotalActivities { get; set; }
    public int PendingApprovals { get; set; }
    public int DuplicateFlags { get; set; }
    public int SyncFailures { get; set; }
}
