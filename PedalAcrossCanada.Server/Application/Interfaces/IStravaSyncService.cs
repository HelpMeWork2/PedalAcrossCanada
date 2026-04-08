using PedalAcrossCanada.Shared.DTOs.Strava;

namespace PedalAcrossCanada.Server.Application.Interfaces;

public interface IStravaSyncService
{
    Task<StravaSyncResultDto> SyncParticipantAsync(Guid participantId, string actor);
    Task<BulkStravaSyncResultDto> SyncAllForEventAsync(Guid eventId, string actor);
    Task<ClubActivitySyncResultDto> SyncClubActivitiesAsync(Guid eventId, string accessToken, string clubId, string actor);
}
