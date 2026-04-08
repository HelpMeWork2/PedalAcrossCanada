using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Leaderboards;

namespace PedalAcrossCanada.Services;

public class LeaderboardHttpService(ApiClient apiClient)
{
    public async Task<PagedResult<IndividualLeaderboardEntry>?> GetIndividualAsync(
        Guid eventId,
        int page = 1,
        int pageSize = 25)
    {
        return await apiClient.GetAsync<PagedResult<IndividualLeaderboardEntry>>(
            $"api/events/{eventId}/leaderboards/individual?page={page}&pageSize={pageSize}");
    }

    public async Task<IReadOnlyList<TeamLeaderboardEntry>?> GetTeamsAsync(Guid eventId)
    {
        return await apiClient.GetAsync<List<TeamLeaderboardEntry>>(
            $"api/events/{eventId}/leaderboards/teams");
    }
}
