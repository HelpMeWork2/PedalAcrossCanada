using System.ComponentModel.DataAnnotations;

namespace PedalAcrossCanada.Shared.DTOs.Participants;

public record ChangeTeamRequest
{
    [Required]
    public Guid TeamId { get; init; }
}
