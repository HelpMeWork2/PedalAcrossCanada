using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Shared.DTOs.Teams;

namespace PedalAcrossCanada.Server.Controllers;

[Route("api/events/{eventId:guid}/teams")]
public class TeamsController(ITeamService teamService) : ApiControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<TeamDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TeamDto>>> GetAll(Guid eventId)
    {
        var result = await teamService.GetAllByEventAsync(eventId);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> GetById(Guid eventId, Guid id)
    {
        var result = await teamService.GetByIdAsync(eventId, id);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TeamDto>> Create(Guid eventId, [FromBody] CreateTeamRequest request)
    {
        var actor = GetUserId();
        var result = await teamService.CreateAsync(eventId, request, actor);
        return CreatedAtAction(nameof(GetById), new { eventId, id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> Update(
        Guid eventId, Guid id, [FromBody] UpdateTeamRequest request)
    {
        var actor = GetUserId();
        var result = await teamService.UpdateAsync(eventId, id, request, actor);
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
        await teamService.DeleteAsync(eventId, id, actor);
        return NoContent();
    }

    [HttpPost("{id:guid}/set-captain")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> SetCaptain(
        Guid eventId, Guid id, [FromBody] SetCaptainRequest request)
    {
        var actor = GetUserId();
        var result = await teamService.SetCaptainAsync(eventId, id, request.ParticipantId, actor);
        return Ok(result);
    }

    private string GetUserId() =>
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new UnauthorizedAccessException("User identity not found.");
}
