using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Events;

namespace PedalAcrossCanada.Server.Controllers;

public class EventsController(IEventService eventService) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin,ExecutiveViewer")]
    [ProducesResponseType(typeof(PagedResult<EventDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<EventDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var result = await eventService.GetAllAsync(page, pageSize);
        return Ok(result);
    }

    [HttpGet("{eventId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDto>> GetById(Guid eventId)
    {
        var result = await eventService.GetByIdAsync(eventId);
        return Ok(result);
    }

    [HttpGet("active")]
    [Authorize]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetActive()
    {
        var result = await eventService.GetActiveEventAsync();
        return result is null ? NoContent() : Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EventDto>> Create([FromBody] CreateEventRequest request)
    {
        var actor = GetUserId();
        var result = await eventService.CreateAsync(request, actor);
        return CreatedAtAction(nameof(GetById), new { eventId = result.Id }, result);
    }

    [HttpPut("{eventId:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDto>> Update(Guid eventId, [FromBody] UpdateEventRequest request)
    {
        var actor = GetUserId();
        var result = await eventService.UpdateAsync(eventId, request, actor);
        return Ok(result);
    }

    [HttpPost("{eventId:guid}/activate")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventDto>> Activate(Guid eventId)
    {
        var actor = GetUserId();
        var result = await eventService.ActivateAsync(eventId, actor);
        return Ok(result);
    }

    [HttpPost("{eventId:guid}/close")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventDto>> Close(Guid eventId)
    {
        var actor = GetUserId();
        var result = await eventService.CloseAsync(eventId, actor);
        return Ok(result);
    }

    [HttpPost("{eventId:guid}/archive")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventDto>> Archive(Guid eventId)
    {
        var actor = GetUserId();
        var result = await eventService.ArchiveAsync(eventId, actor);
        return Ok(result);
    }

    [HttpPost("{eventId:guid}/revert-to-draft")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventDto>> RevertToDraft(Guid eventId)
    {
        var actor = GetUserId();
        var result = await eventService.RevertToDraftAsync(eventId, actor);
        return Ok(result);
    }

    private string GetUserId() =>
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new UnauthorizedAccessException("User identity not found.");
}
