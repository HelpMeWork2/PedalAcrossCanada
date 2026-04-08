using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Audit;

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

    public Task<PagedResult<AuditLogDto>> GetPagedAsync(
        string? entityType = null,
        string? entityId = null,
        string? actor = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 25)
    {
        return Task.FromResult(PagedResult<AuditLogDto>.Empty(page, pageSize));
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
