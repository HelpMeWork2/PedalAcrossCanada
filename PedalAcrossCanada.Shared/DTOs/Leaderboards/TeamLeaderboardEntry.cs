namespace PedalAcrossCanada.Shared.DTOs.Leaderboards;

public class TeamLeaderboardEntry
{
    public int Rank { get; set; }
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public decimal TotalKm { get; set; }
    public int ActiveParticipants { get; set; }
    public decimal AverageKmPerParticipant { get; set; }
}
