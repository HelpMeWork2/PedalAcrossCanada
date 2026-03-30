using System.ComponentModel.DataAnnotations;

namespace PedalAcrossCanada.Shared.DTOs.Participants;

public record UpdateParticipantRequest
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; init; } = string.Empty;

    public bool LeaderboardOptIn { get; init; } = true;
}
