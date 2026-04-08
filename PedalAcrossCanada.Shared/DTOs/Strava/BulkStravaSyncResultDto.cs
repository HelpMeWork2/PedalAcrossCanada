namespace PedalAcrossCanada.Shared.DTOs.Strava;

public class BulkStravaSyncResultDto
{
    public int TotalParticipants { get; set; }
    public int SuccessfulSyncs { get; set; }
    public int FailedSyncs { get; set; }
    public int TotalImported { get; set; }
    public int TotalSkippedDuplicates { get; set; }
    public int TotalSkippedOutOfRange { get; set; }
    public List<string> Errors { get; set; } = [];
}
