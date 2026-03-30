using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Server.Infrastructure.Data;

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
}
