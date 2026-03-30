using System.ComponentModel.DataAnnotations;

namespace PedalAcrossCanada.Shared.DTOs.Participants;

public record CreateParticipantRequest
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string WorkEmail { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; init; } = string.Empty;

    public Guid? TeamId { get; init; }

    public bool LeaderboardOptIn { get; init; } = true;
}
