using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Domain.Entities;

public class ExternalConnection
{
    public Guid Id { get; set; }
    public Guid ParticipantId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ExternalAthleteId { get; set; } = string.Empty;
    public string EncryptedTokenData { get; set; } = string.Empty;
    public ConnectionStatus ConnectionStatus { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public DateTime ConnectedAt { get; set; }
    public DateTime? DisconnectedAt { get; set; }

    public Participant Participant { get; set; } = null!;
}
