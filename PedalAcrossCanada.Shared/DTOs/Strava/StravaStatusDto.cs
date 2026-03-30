using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Shared.DTOs.Strava;

public class StravaStatusDto
{
    public bool IsConnected { get; set; }
    public ConnectionStatus? ConnectionStatus { get; set; }
    public string? ExternalAthleteId { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public DateTime? ConnectedAt { get; set; }
}
