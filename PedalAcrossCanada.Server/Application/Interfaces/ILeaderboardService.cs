using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Leaderboards;

namespace PedalAcrossCanada.Server.Application.Interfaces;

public interface ILeaderboardService
{
    Task<PagedResult<IndividualLeaderboardEntry>> GetIndividualLeaderboardAsync(
        Guid eventId, int page, int pageSize);

    Task<IReadOnlyList<TeamLeaderboardEntry>> GetTeamLeaderboardAsync(Guid eventId);

    Task<int?> GetParticipantRankAsync(Guid eventId, Guid participantId);

    Task<int?> GetTeamRankAsync(Guid eventId, Guid teamId);
}
