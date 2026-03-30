using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.DTOs.Strava;

namespace PedalAcrossCanada.Server.Controllers;

[Route("api/[controller]")]
public class StravaController(
    IStravaTokenService stravaTokenService,
    IStravaSyncService stravaSyncService,
    AppDbContext dbContext) : ApiControllerBase
{
    [HttpGet("auth-url")]
    [Authorize]
    [ProducesResponseType(typeof(StravaAuthUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StravaAuthUrlDto>> GetAuthUrl([FromQuery] Guid eventId)
    {
        var participant = await GetParticipantForCurrentUserAsync(eventId);
        var url = stravaTokenService.BuildAuthorizationUrl(participant.Id);
        return Ok(new StravaAuthUrlDto { AuthorizationUrl = url });
    }

    [HttpPost("callback")]
    [Authorize]
    [ProducesResponseType(typeof(StravaStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StravaStatusDto>> Callback(
        [FromQuery] string code,
        [FromQuery] Guid state)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new ProblemDetails { Title = "Missing authorization code." });
        }

        var actor = GetUserId();
        var result = await stravaTokenService.ExchangeCodeAsync(code, state, actor);
        return Ok(result);
    }

    [HttpPost("disconnect")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disconnect([FromQuery] Guid eventId)
    {
        var actor = GetUserId();
        var participant = await GetParticipantForCurrentUserAsync(eventId);
        await stravaTokenService.DisconnectAsync(participant.Id, actor);
        return NoContent();
    }

    [HttpGet("status")]
    [Authorize]
    [ProducesResponseType(typeof(StravaStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StravaStatusDto>> GetStatus([FromQuery] Guid eventId)
    {
        var participant = await GetParticipantForCurrentUserAsync(eventId);
        var status = await stravaTokenService.GetStatusAsync(participant.Id);
        return Ok(status);
    }

    [HttpPost("sync")]
    [Authorize]
    [ProducesResponseType(typeof(StravaSyncResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StravaSyncResultDto>> ManualSync([FromQuery] Guid eventId)
    {
        var actor = GetUserId();
        var participant = await GetParticipantForCurrentUserAsync(eventId);
        var result = await stravaSyncService.SyncParticipantAsync(participant.Id, actor);
        return Ok(result);
    }

    private async Task<Domain.Entities.Participant> GetParticipantForCurrentUserAsync(Guid eventId)
    {
        var userId = GetUserId();
        return await dbContext.Participants
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.EventId == eventId && p.UserId == userId)
            ?? throw new KeyNotFoundException("You are not registered for this event.");
    }

    private string GetUserId() =>
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new UnauthorizedAccessException("User identity not found.");
}
