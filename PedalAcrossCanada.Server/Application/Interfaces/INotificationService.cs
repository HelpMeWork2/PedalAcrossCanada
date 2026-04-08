using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Notifications;

namespace PedalAcrossCanada.Server.Application.Interfaces;

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> GetForUserAsync(string userId, int page, int pageSize);
    Task<int> GetUnreadCountForUserAsync(string userId);
    Task MarkAsReadAsync(string userId, Guid notificationId);
    Task MarkAllAsReadAsync(string userId);
}
