using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Shared.DTOs.Badges;

namespace PedalAcrossCanada.Server.Controllers;

[Route("api/events/{eventId:guid}/badges")]
[Authorize]
public class BadgesController(
    IBadgeService badgeService,
    IParticipantService participantService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BadgeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BadgeDto>>> GetAll(Guid eventId)
    {
        var result = await badgeService.GetAllAsync(eventId);
        return Ok(result);
    }

    [HttpGet("my-awards")]
    [ProducesResponseType(typeof(IReadOnlyList<BadgeAwardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BadgeAwardDto>>> GetMyAwards(Guid eventId)
    {
        var userId = GetUserId();
        var participant = await participantService.GetByUserIdAsync(eventId, userId);
        var result = await badgeService.GetAwardsForParticipantAsync(eventId, participant.Id);
        return Ok(result);
    }

    [HttpGet("{participantId:guid}/awards")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<BadgeAwardDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BadgeAwardDto>>> GetParticipantAwards(
        Guid eventId, Guid participantId)
    {
        var result = await badgeService.GetAwardsForParticipantAsync(eventId, participantId);
        return Ok(result);
    }

    [HttpPost("{badgeId:guid}/award")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GrantBadge(
        Guid eventId, Guid badgeId, [FromBody] GrantBadgeRequest request)
    {
        var actor = GetUserId();
        await badgeService.GrantBadgeAsync(eventId, badgeId, request.ParticipantId, actor);
        return NoContent();
    }

    private string GetUserId() =>
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new UnauthorizedAccessException("User identity not found.");
}
