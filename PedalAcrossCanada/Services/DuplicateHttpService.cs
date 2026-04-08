using System.Net.Http.Json;
using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Activities;

namespace PedalAcrossCanada.Services;

public class DuplicateHttpService(ApiClient apiClient)
{
    public async Task<PagedResult<DuplicatePairDto>?> GetFlaggedPairsAsync(
        Guid eventId,
        int page = 1,
        int pageSize = 25)
    {
        return await apiClient.GetAsync<PagedResult<DuplicatePairDto>>(
            $"api/events/{eventId}/activities/duplicates?page={page}&pageSize={pageSize}");
    }

    public async Task<DuplicatePairDto?> ResolveAsync(
        Guid eventId,
        Guid activityId,
        DuplicateResolution resolution)
    {
        var request = new ResolveDuplicateRequest { Resolution = resolution };
        var response = await apiClient.PostAsync(
            $"api/events/{eventId}/activities/duplicates/{activityId}/resolve", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DuplicatePairDto>();
    }
}
