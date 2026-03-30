using System.Net.Http.Json;
using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Participants;

namespace PedalAcrossCanada.Services;

public class ParticipantHttpService(ApiClient apiClient)
{
    public async Task<PagedResult<ParticipantDto>?> GetAllAsync(Guid eventId, int page = 1, int pageSize = 25)
    {
        return await apiClient.GetAsync<PagedResult<ParticipantDto>>(
            $"api/events/{eventId}/participants?page={page}&pageSize={pageSize}");
    }

    public async Task<ParticipantDto?> GetByIdAsync(Guid eventId, Guid participantId)
    {
        return await apiClient.GetAsync<ParticipantDto>(
            $"api/events/{eventId}/participants/{participantId}");
    }

    public async Task<ParticipantDto?> GetMeAsync(Guid eventId)
    {
        return await apiClient.GetAsync<ParticipantDto>(
            $"api/events/{eventId}/participants/me");
    }

    public async Task<ParticipantDto?> CreateAsync(Guid eventId, CreateParticipantRequest request)
    {
        var response = await apiClient.PostAsync($"api/events/{eventId}/participants", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ParticipantDto>();
    }

    public async Task<ParticipantDto?> UpdateAsync(
        Guid eventId, Guid participantId, UpdateParticipantRequest request)
    {
        var response = await apiClient.PutAsync(
            $"api/events/{eventId}/participants/{participantId}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ParticipantDto>();
    }

    public async Task<ParticipantDto?> DeactivateAsync(Guid eventId, Guid participantId)
    {
        var response = await apiClient.PostAsync(
            $"api/events/{eventId}/participants/{participantId}/deactivate", new { });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ParticipantDto>();
    }

    public async Task<ParticipantDto?> ReactivateAsync(Guid eventId, Guid participantId)
    {
        var response = await apiClient.PostAsync(
            $"api/events/{eventId}/participants/{participantId}/reactivate", new { });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ParticipantDto>();
    }

    public async Task<ParticipantDto?> ChangeTeamAsync(
        Guid eventId, Guid participantId, ChangeTeamRequest request)
    {
        var response = await apiClient.PostAsync(
            $"api/events/{eventId}/participants/{participantId}/change-team", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ParticipantDto>();
    }
}
