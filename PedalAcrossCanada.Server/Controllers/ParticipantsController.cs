using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Participants;

namespace PedalAcrossCanada.Server.Controllers;

[Route("api/events/{eventId:guid}/participants")]
public class ParticipantsController(IParticipantService participantService) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin,TeamCaptain")]
    [ProducesResponseType(typeof(PagedResult<ParticipantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<ParticipantDto>>> GetAll(
        Guid eventId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var result = await participantService.GetAllByEventAsync(eventId, page, pageSize);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ParticipantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParticipantDto>> GetById(Guid eventId, Guid id)
    {
        var participant = await participantService.GetByIdAsync(eventId, id);
        EnsureAdminOrSelf(participant.UserId);
        return Ok(participant);
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ParticipantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParticipantDto>> GetMe(Guid eventId)
    {
        var userId = GetUserId();
        var result = await participantService.GetByUserIdAsync(eventId, userId);
        return Ok(result);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ParticipantDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ParticipantDto>> Create(
        Guid eventId, [FromBody] CreateParticipantRequest request)
    {
        var userId = GetUserId();
        var result = await participantService.CreateAsync(eventId, request, userId, userId);
        return CreatedAtAction(nameof(GetById), new { eventId, id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ParticipantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParticipantDto>> Update(
        Guid eventId, Guid id, [FromBody] UpdateParticipantRequest request)
    {
        var participant = await participantService.GetByIdAsync(eventId, id);
        EnsureAdminOrSelf(participant.UserId);

        var actor = GetUserId();
        var result = await participantService.UpdateAsync(eventId, id, request, actor);
        return Ok(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ParticipantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ParticipantDto>> Deactivate(Guid eventId, Guid id)
    {
        var actor = GetUserId();
        var result = await participantService.DeactivateAsync(eventId, id, actor);
        return Ok(result);
    }

    [HttpPost("{id:guid}/reactivate")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ParticipantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ParticipantDto>> Reactivate(Guid eventId, Guid id)
    {
        var actor = GetUserId();
        var result = await participantService.ReactivateAsync(eventId, id, actor);
        return Ok(result);
    }

    [HttpPost("{id:guid}/change-team")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ParticipantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParticipantDto>> ChangeTeam(
        Guid eventId, Guid id, [FromBody] ChangeTeamRequest request)
    {
        var actor = GetUserId();
        var result = await participantService.ChangeTeamAsync(eventId, id, request.TeamId, actor);
        return Ok(result);
    }

    private string GetUserId() =>
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new UnauthorizedAccessException("User identity not found.");

    private void EnsureAdminOrSelf(string resourceUserId)
    {
        var userId = GetUserId();
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && userId != resourceUserId)
            throw new UnauthorizedAccessException("You do not have permission to access this resource.");
    }
}
