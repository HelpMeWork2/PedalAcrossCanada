namespace PedalAcrossCanada.Server.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(
        string actor,
        string action,
        string entityType,
        string entityId,
        Guid? eventId = null,
        string? beforeSummary = null,
        string? afterSummary = null);
}
