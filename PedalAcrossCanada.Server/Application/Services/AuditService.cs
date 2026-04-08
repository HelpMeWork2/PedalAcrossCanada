using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Audit;

namespace PedalAcrossCanada.Server.Application.Services;

public class AuditService(AppDbContext dbContext) : IAuditService
{
    public async Task LogAsync(
        string actor,
        string action,
        string entityType,
        string entityId,
        Guid? eventId = null,
        string? beforeSummary = null,
        string? afterSummary = null)
    {
        var entry = new AuditLog
        {
            Actor = actor,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Timestamp = DateTime.UtcNow,
            EventId = eventId,
            BeforeSummary = beforeSummary,
            AfterSummary = afterSummary
        };

        dbContext.AuditLogs.Add(entry);
        await dbContext.SaveChangesAsync();
    }

    public async Task<PagedResult<AuditLogDto>> GetPagedAsync(
        string? entityType = null,
        string? entityId = null,
        string? actor = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 25)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);

        if (!string.IsNullOrWhiteSpace(entityId))
            query = query.Where(a => a.EntityId == entityId);

        if (!string.IsNullOrWhiteSpace(actor))
            query = query.Where(a => a.Actor == actor);

        if (startDate.HasValue)
            query = query.Where(a => a.Timestamp >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(a => a.Timestamp <= endDate.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                Actor = a.Actor,
                Action = a.Action,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Timestamp = a.Timestamp,
                BeforeSummary = a.BeforeSummary,
                AfterSummary = a.AfterSummary,
                EventId = a.EventId
            })
            .ToListAsync();

        return PagedResult<AuditLogDto>.Create(items, page, pageSize, totalCount);
    }
}
