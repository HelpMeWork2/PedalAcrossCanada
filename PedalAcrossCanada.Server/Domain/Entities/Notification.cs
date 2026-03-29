using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; }
    public Guid? ParticipantId { get; set; }
    public NotificationType NotificationType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RelatedEntityType { get; set; }
    public string? RelatedEntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool EmailSent { get; set; }
    public DateTime? EmailSentAt { get; set; }

    public Participant? Participant { get; set; }
}
