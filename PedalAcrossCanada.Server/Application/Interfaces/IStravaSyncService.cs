using PedalAcrossCanada.Shared.DTOs.Strava;

namespace PedalAcrossCanada.Server.Application.Interfaces;

public interface IStravaSyncService
{
    Task<StravaSyncResultDto> SyncParticipantAsync(Guid participantId, string actor);
}
