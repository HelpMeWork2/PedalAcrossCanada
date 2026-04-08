using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Audit;

namespace PedalAcrossCanada.Services;

public class AuditHttpService(ApiClient apiClient)
{
    public async Task<PagedResult<AuditLogDto>?> GetAuditLogAsync(
        string? entityType = null,
        string? entityId = null,
        string? actor = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 25)
    {
        var query = BuildQueryString(entityType, entityId, actor, startDate, endDate, page, pageSize);
        return await apiClient.GetAsync<PagedResult<AuditLogDto>>($"api/audit{query}");
    }

    private static string BuildQueryString(
        string? entityType,
        string? entityId,
        string? actor,
        DateTime? startDate,
        DateTime? endDate,
        int page,
        int pageSize)
    {
        var parts = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(entityType))
            parts.Add($"entityType={Uri.EscapeDataString(entityType)}");

        if (!string.IsNullOrWhiteSpace(entityId))
            parts.Add($"entityId={Uri.EscapeDataString(entityId)}");

        if (!string.IsNullOrWhiteSpace(actor))
            parts.Add($"actor={Uri.EscapeDataString(actor)}");

        if (startDate.HasValue)
            parts.Add($"startDate={startDate.Value:O}");

        if (endDate.HasValue)
            parts.Add($"endDate={endDate.Value:O}");

        return "?" + string.Join("&", parts);
    }
}
