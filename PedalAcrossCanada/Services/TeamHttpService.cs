using System.Net.Http.Json;
using PedalAcrossCanada.Shared.DTOs.Teams;

namespace PedalAcrossCanada.Services;

public class TeamHttpService(ApiClient apiClient)
{
    public async Task<IReadOnlyList<TeamDto>?> GetAllAsync(Guid eventId)
    {
        return await apiClient.GetAsync<List<TeamDto>>($"api/events/{eventId}/teams");
    }

    public async Task<TeamDto?> GetByIdAsync(Guid eventId, Guid teamId)
    {
        return await apiClient.GetAsync<TeamDto>($"api/events/{eventId}/teams/{teamId}");
    }

    public async Task<TeamDto?> CreateAsync(Guid eventId, CreateTeamRequest request)
    {
        var response = await apiClient.PostAsync($"api/events/{eventId}/teams", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TeamDto>();
    }

    public async Task<TeamDto?> UpdateAsync(Guid eventId, Guid teamId, UpdateTeamRequest request)
    {
        var response = await apiClient.PutAsync($"api/events/{eventId}/teams/{teamId}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TeamDto>();
    }

    public async Task DeleteAsync(Guid eventId, Guid teamId)
    {
        var response = await apiClient.DeleteAsync($"api/events/{eventId}/teams/{teamId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<TeamDto?> SetCaptainAsync(Guid eventId, Guid teamId, SetCaptainRequest request)
    {
        var response = await apiClient.PostAsync($"api/events/{eventId}/teams/{teamId}/set-captain", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TeamDto>();
    }
}
