using System.Net.Http.Json;
using PedalAcrossCanada.Shared.DTOs.Milestones;

namespace PedalAcrossCanada.Services;

public class MilestoneHttpService(ApiClient apiClient)
{
    public async Task<IReadOnlyList<MilestoneDto>?> GetAllAsync(Guid eventId)
    {
        return await apiClient.GetAsync<List<MilestoneDto>>($"api/events/{eventId}/milestones");
    }

    public async Task<MilestoneDto?> GetByIdAsync(Guid eventId, Guid milestoneId)
    {
        return await apiClient.GetAsync<MilestoneDto>($"api/events/{eventId}/milestones/{milestoneId}");
    }

    public async Task<MilestoneDto?> CreateAsync(Guid eventId, CreateMilestoneRequest request)
    {
        var response = await apiClient.PostAsync($"api/events/{eventId}/milestones", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MilestoneDto>();
    }

    public async Task<MilestoneDto?> UpdateAsync(Guid eventId, Guid milestoneId, UpdateMilestoneRequest request)
    {
        var response = await apiClient.PutAsync($"api/events/{eventId}/milestones/{milestoneId}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MilestoneDto>();
    }

    public async Task DeleteAsync(Guid eventId, Guid milestoneId)
    {
        var response = await apiClient.DeleteAsync($"api/events/{eventId}/milestones/{milestoneId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<MilestoneDto?> AnnounceAsync(Guid eventId, Guid milestoneId)
    {
        var response = await apiClient.PostAsync($"api/events/{eventId}/milestones/{milestoneId}/announce", new { });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MilestoneDto>();
    }
}
