using System.ComponentModel.DataAnnotations;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Shared.DTOs.Events;

public record CreateEventRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; init; }

    [Required]
    public DateTime StartDate { get; init; }

    [Required]
    public DateTime EndDate { get; init; }

    [Required]
    [Range(0.01, 100000)]
    public decimal RouteDistanceKm { get; init; }

    public ManualEntryMode ManualEntryMode { get; init; } = ManualEntryMode.AllowedWithApproval;

    public bool StravaEnabled { get; init; }

    [MaxLength(1000)]
    public string? BannerMessage { get; init; }

    [Range(0.01, 10000)]
    public decimal MaxSingleRideKm { get; init; } = 300m;

    public bool LeaderboardPublic { get; init; } = true;

    public bool ShowTeamAverage { get; init; } = true;
}
