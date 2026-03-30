using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Shared.DTOs.Milestones;

namespace PedalAcrossCanada.Server.Controllers;

[Route("api/events/{eventId:guid}/milestones")]
public class MilestonesController(IMilestoneService milestoneService) : ApiControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<MilestoneDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MilestoneDto>>> GetAll(Guid eventId)
    {
        var result = await milestoneService.GetAllByEventAsync(eventId);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(MilestoneDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MilestoneDto>> GetById(Guid eventId, Guid id)
    {
        var result = await milestoneService.GetByIdAsync(eventId, id);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(MilestoneDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MilestoneDto>> Create(Guid eventId, [FromBody] CreateMilestoneRequest request)
    {
        var actor = GetUserId();
        var result = await milestoneService.CreateAsync(eventId, request, actor);
        return CreatedAtAction(nameof(GetById), new { eventId, id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(MilestoneDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MilestoneDto>> Update(
        Guid eventId, Guid id, [FromBody] UpdateMilestoneRequest request)
    {
        var actor = GetUserId();
        var result = await milestoneService.UpdateAsync(eventId, id, request, actor);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid eventId, Guid id)
    {
        var actor = GetUserId();
        await milestoneService.DeleteAsync(eventId, id, actor);
        return NoContent();
    }

    [HttpPost("{id:guid}/announce")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(MilestoneDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MilestoneDto>> Announce(Guid eventId, Guid id)
    {
        var actor = GetUserId();
        var result = await milestoneService.AnnounceAsync(eventId, id, actor);
        return Ok(result);
    }

    private string GetUserId() =>
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new UnauthorizedAccessException("User identity not found.");
}
