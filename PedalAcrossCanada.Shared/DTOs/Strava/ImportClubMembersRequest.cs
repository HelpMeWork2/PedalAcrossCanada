using System.ComponentModel.DataAnnotations;

namespace PedalAcrossCanada.Shared.DTOs.Strava;

public record ImportClubMemberEntry
{
    public long AthleteId { get; init; }

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
}

public record ImportClubMembersRequest
{
    [Required]
    public List<ImportClubMemberEntry> Members { get; init; } = [];
}
