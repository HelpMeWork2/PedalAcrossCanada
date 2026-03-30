using System.ComponentModel.DataAnnotations;

namespace PedalAcrossCanada.Shared.DTOs.Teams;

public record SetCaptainRequest
{
    [Required]
    public Guid ParticipantId { get; init; }
}
