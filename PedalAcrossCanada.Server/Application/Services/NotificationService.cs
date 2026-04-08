using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Notifications;

namespace PedalAcrossCanada.Server.Application.Services;

public class NotificationService(AppDbContext dbContext) : INotificationService
{
    public async Task<PagedResult<NotificationDto>> GetForUserAsync(
        string userId, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var participantIds = await dbContext.Participants
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.Id)
            .ToListAsync();

        var query = dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.ParticipantId.HasValue && participantIds.Contains(n.ParticipantId.Value));

        var totalCount = await query.CountAsync();

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => MapToDto(n))
            .ToListAsync();

        return PagedResult<NotificationDto>.Create(notifications, page, pageSize, totalCount);
    }

    public async Task<int> GetUnreadCountForUserAsync(string userId)
    {
        var participantIds = await dbContext.Participants
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.Id)
            .ToListAsync();

        return await dbContext.Notifications
            .CountAsync(n => n.ParticipantId.HasValue
                             && participantIds.Contains(n.ParticipantId.Value)
                             && !n.IsRead);
    }

    public async Task MarkAsReadAsync(string userId, Guid notificationId)
    {
        var participantIds = await dbContext.Participants
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.Id)
            .ToListAsync();

        var notification = await dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId
                                      && n.ParticipantId.HasValue
                                      && participantIds.Contains(n.ParticipantId.Value))
            ?? throw new KeyNotFoundException($"Notification '{notificationId}' not found.");

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        var participantIds = await dbContext.Participants
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.Id)
            .ToListAsync();

        var unread = await dbContext.Notifications
            .Where(n => n.ParticipantId.HasValue
                        && participantIds.Contains(n.ParticipantId.Value)
                        && !n.IsRead)
            .ToListAsync();

        if (unread.Count == 0) return;

        foreach (var n in unread)
            n.IsRead = true;

        await dbContext.SaveChangesAsync();
    }

    private static NotificationDto MapToDto(Domain.Entities.Notification n) => new()
    {
        Id = n.Id,
        NotificationType = n.NotificationType,
        Title = n.Title,
        Message = n.Message,
        IsRead = n.IsRead,
        CreatedAt = n.CreatedAt,
        RelatedEntityType = n.RelatedEntityType,
        RelatedEntityId = n.RelatedEntityId
    };
}
