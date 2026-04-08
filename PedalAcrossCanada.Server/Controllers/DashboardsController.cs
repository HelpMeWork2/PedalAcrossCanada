using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Shared.DTOs.Dashboards;

namespace PedalAcrossCanada.Server.Controllers;

[Route("api/events/{eventId:guid}/dashboards")]
[Authorize]
public class DashboardsController(
    IDashboardService dashboardService,
    IParticipantService participantService) : ApiControllerBase
{
    [HttpGet("event")]
    [ProducesResponseType(typeof(EventDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDashboardDto>> GetEvent(Guid eventId)
    {
        var result = await dashboardService.GetEventDashboardAsync(eventId);
        return Ok(result);
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(ParticipantDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParticipantDashboardDto>> GetMy(Guid eventId)
    {
        var userId = GetUserId();
        var participant = await participantService.GetByUserIdAsync(eventId, userId);
        var result = await dashboardService.GetParticipantDashboardAsync(eventId, participant.Id);
        return Ok(result);
    }

    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(AdminDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminDashboardDto>> GetAdmin(Guid eventId)
    {
        var result = await dashboardService.GetAdminDashboardAsync(eventId);
        return Ok(result);
    }

    private string GetUserId() =>
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new UnauthorizedAccessException("User identity not found.");
}
