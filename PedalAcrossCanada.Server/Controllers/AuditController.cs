using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Audit;

namespace PedalAcrossCanada.Server.Controllers;

[Route("api/audit")]
[Authorize(Roles = "Admin")]
public class AuditController(IAuditService auditService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> GetAuditLog(
        [FromQuery] string? entityType = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? actor = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var result = await auditService.GetPagedAsync(
            entityType, entityId, actor, startDate, endDate, page, pageSize);
        return Ok(result);
    }
}
