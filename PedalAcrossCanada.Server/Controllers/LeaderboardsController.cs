using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Leaderboards;

namespace PedalAcrossCanada.Server.Controllers;

[Route("api/events/{eventId:guid}/leaderboards")]
[Authorize]
public class LeaderboardsController(ILeaderboardService leaderboardService) : ApiControllerBase
{
    [HttpGet("individual")]
    [ProducesResponseType(typeof(PagedResult<IndividualLeaderboardEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<IndividualLeaderboardEntry>>> GetIndividual(
        Guid eventId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var result = await leaderboardService.GetIndividualLeaderboardAsync(eventId, page, pageSize);
        return Ok(result);
    }

    [HttpGet("teams")]
    [ProducesResponseType(typeof(IReadOnlyList<TeamLeaderboardEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TeamLeaderboardEntry>>> GetTeams(Guid eventId)
    {
        var result = await leaderboardService.GetTeamLeaderboardAsync(eventId);
        return Ok(result);
    }
}
