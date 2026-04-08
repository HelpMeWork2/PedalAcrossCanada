namespace PedalAcrossCanada.Shared.DTOs.Strava;

public class ClubActivitySyncResultDto
{
    public int TotalActivitiesFetched { get; set; }
    public int ImportedCount { get; set; }
    public int SkippedDuplicateCount { get; set; }
    public int SkippedUnsupportedTypeCount { get; set; }
    public int SkippedOutOfRangeCount { get; set; }
    public int UnmatchedCount { get; set; }
    public List<string> Errors { get; set; } = [];
    public string? ErrorMessage { get; set; }
}
