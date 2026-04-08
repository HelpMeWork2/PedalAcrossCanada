namespace PedalAcrossCanada.Shared.DTOs.Strava;

public class ImportClubMembersResultDto
{
    public int TotalRequested { get; set; }
    public int Imported { get; set; }
    public int SkippedAlreadyRegistered { get; set; }
    public List<string> Errors { get; set; } = [];
}
