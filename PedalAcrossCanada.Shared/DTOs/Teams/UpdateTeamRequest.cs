using System.ComponentModel.DataAnnotations;

namespace PedalAcrossCanada.Shared.DTOs.Teams;

public record UpdateTeamRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; init; }
}
