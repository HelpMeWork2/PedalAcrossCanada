using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Activities;

namespace PedalAcrossCanada.Server.Controllers;

[Route("api/events/{eventId:guid}/activities/duplicates")]
public class DuplicatesController(IDuplicateService duplicateService) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(PagedResult<DuplicatePairDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<DuplicatePairDto>>> GetFlagged(
        Guid eventId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var result = await duplicateService.GetFlaggedPairsAsync(eventId, page, pageSize);
        return Ok(result);
    }

    [HttpPost("{id:guid}/resolve")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(DuplicatePairDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DuplicatePairDto>> Resolve(
        Guid eventId,
        Guid id,
        [FromBody] ResolveDuplicateRequest request)
    {
        var actor = User.FindFirst("sub")?.Value ?? "unknown";
        var result = await duplicateService.ResolveAsync(eventId, id, request.Resolution, actor);
        return Ok(result);
    }
}
