namespace PedalAcrossCanada.Shared.DTOs.Strava;

public class StravaClubMemberDto
{
    public long AthleteId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
    public bool IsOwner { get; set; }
    public bool IsAdmin { get; set; }
    public bool AlreadyRegistered { get; set; }
    public string? SuggestedEmail { get; set; }
}
