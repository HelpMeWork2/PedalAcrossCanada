using PedalAcrossCanada.Server.Application.Interfaces;

namespace PedalAcrossCanada.Server.Tests.Fakes;

/// <summary>
/// Records audit log calls for assertion without persisting to a database.
/// </summary>
public sealed class FakeAuditService : IAuditService
{
    public List<AuditEntry> Entries { get; } = [];

    public Task LogAsync(
        string actor,
        string action,
        string entityType,
        string entityId,
        Guid? eventId = null,
        string? beforeSummary = null,
        string? afterSummary = null)
    {
        Entries.Add(new AuditEntry(actor, action, entityType, entityId, eventId, beforeSummary, afterSummary));
        return Task.CompletedTask;
    }

    public record AuditEntry(
        string Actor,
        string Action,
        string EntityType,
        string EntityId,
        Guid? EventId,
        string? BeforeSummary,
        string? AfterSummary);
}
