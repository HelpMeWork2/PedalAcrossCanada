using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedalAcrossCanada.Server.Application.Interfaces;

namespace PedalAcrossCanada.Server.Controllers;

[Route("api/events/{eventId:guid}/reports")]
[Authorize(Roles = "Admin,ExecutiveViewer")]
public class ReportsController(IReportService reportService) : ApiControllerBase
{
    [HttpGet("participants")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetParticipantsReport(
        Guid eventId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? teamId = null)
    {
        var bytes = await reportService.GetParticipantsReportAsync(eventId, startDate, endDate, teamId);
        return File(bytes, "text/csv", $"participants-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("activities")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivitiesReport(
        Guid eventId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? teamId = null,
        [FromQuery] Guid? participantId = null)
    {
        var bytes = await reportService.GetActivitiesReportAsync(
            eventId, startDate, endDate, teamId, participantId);
        return File(bytes, "text/csv", $"activities-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("teams")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeamTotalsReport(
        Guid eventId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var bytes = await reportService.GetTeamTotalsReportAsync(eventId, startDate, endDate);
        return File(bytes, "text/csv", $"team-totals-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("milestones")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMilestonesReport(Guid eventId)
    {
        var bytes = await reportService.GetMilestonesReportAsync(eventId);
        return File(bytes, "text/csv", $"milestones-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("badges")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBadgeAwardsReport(
        Guid eventId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? teamId = null,
        [FromQuery] Guid? participantId = null)
    {
        var bytes = await reportService.GetBadgeAwardsReportAsync(
            eventId, startDate, endDate, teamId, participantId);
        return File(bytes, "text/csv", $"badge-awards-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("executive-summary")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExecutiveSummaryReport(Guid eventId)
    {
        var bytes = await reportService.GetExecutiveSummaryReportAsync(eventId);
        return File(bytes, "text/csv", $"executive-summary-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
