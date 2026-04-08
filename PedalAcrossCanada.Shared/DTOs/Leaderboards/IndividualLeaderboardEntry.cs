namespace PedalAcrossCanada.Shared.DTOs.Leaderboards;

public class IndividualLeaderboardEntry
{
    public int Rank { get; set; }
    public Guid ParticipantId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? TeamName { get; set; }
    public decimal TotalKm { get; set; }
    public int RideCount { get; set; }
    public DateTime JoinedAt { get; set; }
}
