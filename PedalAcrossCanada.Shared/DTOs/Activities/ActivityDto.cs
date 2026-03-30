using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Shared.DTOs.Activities;

public class ActivityDto
{
    public Guid Id { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid EventId { get; set; }
    public string ParticipantDisplayName { get; set; } = string.Empty;
    public DateTime ActivityDate { get; set; }
    public decimal DistanceKm { get; set; }
    public RideType RideType { get; set; }
    public string? Notes { get; set; }
    public ActivitySource Source { get; set; }
    public ActivityStatus Status { get; set; }
    public bool CountsTowardTotal { get; set; }
    public string? ExternalActivityId { get; set; }
    public string? ExternalTitle { get; set; }
    public DateTime? ImportedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public bool IsDuplicateFlagged { get; set; }
    public bool LockedByAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
