using System.ComponentModel.DataAnnotations;

namespace PedalAcrossCanada.Shared.DTOs.Milestones;

public record UpdateMilestoneRequest
{
    [Required]
    [MaxLength(200)]
    public string StopName { get; init; } = string.Empty;

    [Required]
    [Range(0, int.MaxValue)]
    public int OrderIndex { get; init; }

    [Required]
    [Range(0.01, 100000)]
    public decimal CumulativeDistanceKm { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }

    [MaxLength(500)]
    public string? RewardText { get; init; }
}
