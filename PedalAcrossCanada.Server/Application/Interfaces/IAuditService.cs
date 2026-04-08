using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Audit;

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

    Task<PagedResult<AuditLogDto>> GetPagedAsync(
        string? entityType = null,
        string? entityId = null,
        string? actor = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 25);
}
