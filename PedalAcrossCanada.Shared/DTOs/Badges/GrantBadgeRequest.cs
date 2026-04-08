using System.ComponentModel.DataAnnotations;

namespace PedalAcrossCanada.Shared.DTOs.Badges;

public record GrantBadgeRequest
{
    [Required]
    public Guid ParticipantId { get; init; }
}
