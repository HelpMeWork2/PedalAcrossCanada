using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Notifications;

namespace PedalAcrossCanada.Server.Controllers;

[Route("api/notifications")]
[Authorize]
public class NotificationsController(INotificationService notificationService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<NotificationDto>>> GetMy(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var userId = GetUserId();
        var result = await notificationService.GetForUserAsync(userId, page, pageSize);
        return Ok(result);
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadCountDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount()
    {
        var userId = GetUserId();
        var count = await notificationService.GetUnreadCountForUserAsync(userId);
        return Ok(new UnreadCountDto { Count = count });
    }

    [HttpPut("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = GetUserId();
        await notificationService.MarkAsReadAsync(userId, id);
        return NoContent();
    }

    [HttpPut("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetUserId();
        await notificationService.MarkAllAsReadAsync(userId);
        return NoContent();
    }

    private string GetUserId() =>
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new UnauthorizedAccessException("User identity not found.");
}
