using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Activities;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Controllers;

[Route("api/events/{eventId:guid}/activities")]
public class ActivitiesController(
    IActivityService activityService,
    IParticipantService participantService) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(PagedResult<ActivityDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ActivityDto>>> GetAll(
        Guid eventId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] ActivityStatus? status = null,
        [FromQuery] ActivitySource? source = null,
        [FromQuery] Guid? participantId = null,
        [FromQuery] Guid? teamId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] bool? duplicateFlagged = null)
    {
        var result = await activityService.GetAllAsync(
            eventId, page, pageSize, status, source,
            participantId, teamId, startDate, endDate, duplicateFlagged);
        return Ok(result);
    }

    [HttpGet("~/api/events/{eventId:guid}/participants/{participantId:guid}/activities")]
    [Authorize]
    [ProducesResponseType(typeof(PagedResult<ActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<ActivityDto>>> GetByParticipant(
        Guid eventId,
        Guid participantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        await EnsureAdminOrParticipantOwnerAsync(eventId, participantId);
        var result = await activityService.GetByParticipantAsync(eventId, participantId, page, pageSize);
        return Ok(result);
    }

    [HttpGet("my")]
    [Authorize]
    [ProducesResponseType(typeof(PagedResult<ActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<ActivityDto>>> GetMyActivities(
        Guid eventId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var participant = await GetCurrentParticipantAsync(eventId);
        var result = await activityService.GetByParticipantAsync(eventId, participant.Id, page, pageSize);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ActivityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActivityDto>> GetById(Guid eventId, Guid id)
    {
        var activity = await activityService.GetByIdAsync(eventId, id);
        await EnsureAdminOrParticipantOwnerAsync(eventId, activity.ParticipantId);
        return Ok(activity);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(CreateActivityResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateActivityResponse>> Create(
        Guid eventId, [FromBody] CreateActivityRequest request)
    {
        var participant = await GetCurrentParticipantAsync(eventId);
        var actor = GetUserId();
        var result = await activityService.CreateAsync(eventId, participant.Id, request, actor);
        return CreatedAtAction(
            nameof(GetById),
            new { eventId, id = result.Activity.Id },
            result);
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ActivityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActivityDto>> Update(
        Guid eventId, Guid id, [FromBody] UpdateActivityRequest request)
    {
        var activity = await activityService.GetByIdAsync(eventId, id);
        await EnsureAdminOrParticipantOwnerAsync(eventId, activity.ParticipantId);

        var actor = GetUserId();
        var result = await activityService.UpdateAsync(eventId, id, request, actor);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid eventId, Guid id)
    {
        var activity = await activityService.GetByIdAsync(eventId, id);
        await EnsureAdminOrParticipantOwnerAsync(eventId, activity.ParticipantId);

        var actor = GetUserId();
        await activityService.DeleteAsync(eventId, id, actor);
        return NoContent();
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ActivityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ActivityDto>> Approve(Guid eventId, Guid id)
    {
        var actor = GetUserId();
        var result = await activityService.ApproveAsync(eventId, id, actor);
        return Ok(result);
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ActivityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ActivityDto>> Reject(
        Guid eventId, Guid id, [FromBody] RejectActivityRequest request)
    {
        var actor = GetUserId();
        var result = await activityService.RejectAsync(eventId, id, request, actor);
        return Ok(result);
    }

    [HttpPost("{id:guid}/invalidate")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ActivityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ActivityDto>> Invalidate(
        Guid eventId, Guid id, [FromBody] InvalidateActivityRequest request)
    {
        var actor = GetUserId();
        var result = await activityService.InvalidateAsync(eventId, id, request, actor);
        return Ok(result);
    }

    [HttpPost("{id:guid}/lock")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ActivityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActivityDto>> Lock(Guid eventId, Guid id)
    {
        var actor = GetUserId();
        var result = await activityService.LockAsync(eventId, id, actor);
        return Ok(result);
    }

    private string GetUserId() =>
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new UnauthorizedAccessException("User identity not found.");

    private async Task<Shared.DTOs.Participants.ParticipantDto> GetCurrentParticipantAsync(Guid eventId)
    {
        var userId = GetUserId();
        return await participantService.GetByUserIdAsync(eventId, userId);
    }

    private async Task EnsureAdminOrParticipantOwnerAsync(Guid eventId, Guid participantId)
    {
        if (User.IsInRole("Admin"))
            return;

        var userId = GetUserId();
        var participant = await participantService.GetByUserIdAsync(eventId, userId);
        if (participant.Id != participantId)
            throw new UnauthorizedAccessException("You do not have permission to access this resource.");
    }
}
