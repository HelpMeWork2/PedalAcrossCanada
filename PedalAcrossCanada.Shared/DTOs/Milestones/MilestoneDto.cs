using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Shared.DTOs.Milestones;

public class MilestoneDto
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string StopName { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public decimal CumulativeDistanceKm { get; set; }
    public string? Description { get; set; }
    public string? RewardText { get; set; }
    public DateTime? AchievedAt { get; set; }
    public decimal? TotalKmAtAchievement { get; set; }
    public AnnouncementStatus AnnouncementStatus { get; set; }
    public string? AnnouncedBy { get; set; }
    public DateTime? AnnouncedAt { get; set; }
}
