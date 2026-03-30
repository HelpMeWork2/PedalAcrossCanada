using System.ComponentModel.DataAnnotations;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Shared.DTOs.Activities;

public record CreateActivityRequest
{
    [Required]
    public DateTime ActivityDate { get; init; }

    [Required]
    [Range(0.01, 100000)]
    public decimal DistanceKm { get; init; }

    public RideType RideType { get; init; } = RideType.Leisure;

    [MaxLength(500)]
    public string? Notes { get; init; }
}
