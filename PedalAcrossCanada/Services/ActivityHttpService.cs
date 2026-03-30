using System.Net.Http.Json;
using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Activities;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Services;

public class ActivityHttpService(ApiClient apiClient)
{
    public async Task<PagedResult<ActivityDto>?> GetAllAsync(
        Guid eventId,
        int page = 1,
        int pageSize = 25,
        ActivityStatus? status = null,
        ActivitySource? source = null,
        Guid? participantId = null)
    {
        var url = $"api/events/{eventId}/activities?page={page}&pageSize={pageSize}";

        if (status.HasValue)
            url += $"&status={status.Value}";

        if (source.HasValue)
            url += $"&source={source.Value}";

        if (participantId.HasValue)
            url += $"&participantId={participantId.Value}";

        return await apiClient.GetAsync<PagedResult<ActivityDto>>(url);
    }

    public async Task<PagedResult<ActivityDto>?> GetMyActivitiesAsync(
        Guid eventId,
        int page = 1,
        int pageSize = 25)
    {
        return await apiClient.GetAsync<PagedResult<ActivityDto>>(
            $"api/events/{eventId}/activities/my?page={page}&pageSize={pageSize}");
    }

    public async Task<ActivityDto?> GetByIdAsync(Guid eventId, Guid activityId)
    {
        return await apiClient.GetAsync<ActivityDto>(
            $"api/events/{eventId}/activities/{activityId}");
    }

    public async Task<CreateActivityResponse?> CreateAsync(
        Guid eventId, CreateActivityRequest request)
    {
        var response = await apiClient.PostAsync(
            $"api/events/{eventId}/activities", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CreateActivityResponse>();
    }

    public async Task<ActivityDto?> UpdateAsync(
        Guid eventId, Guid activityId, UpdateActivityRequest request)
    {
        var response = await apiClient.PutAsync(
            $"api/events/{eventId}/activities/{activityId}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ActivityDto>();
    }

    public async Task DeleteAsync(Guid eventId, Guid activityId)
    {
        var response = await apiClient.DeleteAsync(
            $"api/events/{eventId}/activities/{activityId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<ActivityDto?> ApproveAsync(Guid eventId, Guid activityId)
    {
        var response = await apiClient.PostAsync(
            $"api/events/{eventId}/activities/{activityId}/approve", new { });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ActivityDto>();
    }

    public async Task<ActivityDto?> RejectAsync(
        Guid eventId, Guid activityId, RejectActivityRequest request)
    {
        var response = await apiClient.PostAsync(
            $"api/events/{eventId}/activities/{activityId}/reject", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ActivityDto>();
    }

    public async Task<ActivityDto?> InvalidateAsync(
        Guid eventId, Guid activityId, InvalidateActivityRequest request)
    {
        var response = await apiClient.PostAsync(
            $"api/events/{eventId}/activities/{activityId}/invalidate", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ActivityDto>();
    }

    public async Task<ActivityDto?> LockAsync(Guid eventId, Guid activityId)
    {
        var response = await apiClient.PostAsync(
            $"api/events/{eventId}/activities/{activityId}/lock", new { });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ActivityDto>();
    }
}
