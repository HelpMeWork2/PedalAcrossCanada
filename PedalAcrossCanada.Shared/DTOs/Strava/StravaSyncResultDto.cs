namespace PedalAcrossCanada.Shared.DTOs.Strava;

public class StravaSyncResultDto
{
    public int ImportedCount { get; set; }
    public int SkippedDuplicateCount { get; set; }
    public int SkippedOutOfRangeCount { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorMessage { get; set; }
}
