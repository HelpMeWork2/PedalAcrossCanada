using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Notifications;

namespace PedalAcrossCanada.Services;

public class NotificationHttpService(ApiClient apiClient)
{
    public async Task<PagedResult<NotificationDto>?> GetNotificationsAsync(int page = 1, int pageSize = 25)
    {
        return await apiClient.GetAsync<PagedResult<NotificationDto>>(
            $"api/notifications?page={page}&pageSize={pageSize}");
    }

    public async Task<UnreadCountDto?> GetUnreadCountAsync()
    {
        return await apiClient.GetAsync<UnreadCountDto>("api/notifications/unread-count");
    }

    public async Task<HttpResponseMessage> MarkAsReadAsync(Guid notificationId)
    {
        return await apiClient.PutAsync($"api/notifications/{notificationId}/read", new { });
    }

    public async Task<HttpResponseMessage> MarkAllAsReadAsync()
    {
        return await apiClient.PutAsync("api/notifications/read-all", new { });
    }
}
