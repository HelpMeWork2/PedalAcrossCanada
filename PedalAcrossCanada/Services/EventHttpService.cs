using System.Net.Http.Json;
using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Events;

namespace PedalAcrossCanada.Services;

public class EventHttpService(ApiClient apiClient)
{
    public async Task<PagedResult<EventDto>?> GetAllAsync(int page = 1, int pageSize = 25)
    {
        return await apiClient.GetAsync<PagedResult<EventDto>>($"api/events?page={page}&pageSize={pageSize}");
    }

    public async Task<EventDto?> GetByIdAsync(Guid eventId)
    {
        return await apiClient.GetAsync<EventDto>($"api/events/{eventId}");
    }

    public async Task<EventDto?> GetActiveEventAsync()
    {
        return await apiClient.GetAsync<EventDto>("api/events/active");
    }

    public async Task<EventDto?> CreateAsync(CreateEventRequest request)
    {
        var response = await apiClient.PostAsync("api/events", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EventDto>();
    }

    public async Task<EventDto?> UpdateAsync(Guid eventId, UpdateEventRequest request)
    {
        var response = await apiClient.PutAsync($"api/events/{eventId}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EventDto>();
    }

    public async Task<EventDto?> ActivateAsync(Guid eventId)
    {
        var response = await apiClient.PostAsync($"api/events/{eventId}/activate", new { });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EventDto>();
    }

    public async Task<EventDto?> CloseAsync(Guid eventId)
    {
        var response = await apiClient.PostAsync($"api/events/{eventId}/close", new { });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EventDto>();
    }

    public async Task<EventDto?> ArchiveAsync(Guid eventId)
    {
        var response = await apiClient.PostAsync($"api/events/{eventId}/archive", new { });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EventDto>();
    }

    public async Task<EventDto?> RevertToDraftAsync(Guid eventId)
    {
        var response = await apiClient.PostAsync($"api/events/{eventId}/revert-to-draft", new { });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EventDto>();
    }
}
