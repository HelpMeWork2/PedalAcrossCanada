using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Shared.DTOs.Notifications;

public class NotificationDto
{
    public Guid Id { get; set; }
    public NotificationType NotificationType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? RelatedEntityType { get; set; }
    public string? RelatedEntityId { get; set; }
}
