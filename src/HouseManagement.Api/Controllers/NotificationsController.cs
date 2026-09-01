using HouseManagement.Api.Common.Api;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace HouseManagement.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMine(
        [FromQuery] bool? unreadOnly,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var notifications = await _notifications.GetForUserAsync(userId, unreadOnly, page, pageSize, cancellationToken);
        var response = ApiResponseFactory.Create(this, notifications.Select(ToDto), "Notifications retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [HttpGet("me/{id:int}")]
    public async Task<IActionResult> GetMineById(int id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var notification = await _notifications.GetByIdForUserAsync(id, userId, cancellationToken);
        if (notification == null) return NotFound();

        var response = ApiResponseFactory.Create(this, ToDto(notification), "Notification retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [HttpPatch("me/{id:int}/read")]
    public async Task<IActionResult> MarkMineAsRead(int id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var notification = await _notifications.MarkAsReadAsync(id, userId, cancellationToken);
        if (notification == null) return NotFound();

        var response = ApiResponseFactory.Create(this, ToDto(notification), "Notification marked as read", StatusCodes.Status200OK);
        return Ok(response);
    }

    [HttpGet("me/unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var count = await _notifications.GetUnreadCountAsync(userId, cancellationToken);
        var response = ApiResponseFactory.Create(
            this,
            new UnreadNotificationCountDto { Count = count },
            "Unread notification count retrieved",
            StatusCodes.Status200OK);
        return Ok(response);
    }

    private bool TryGetUserId(out int userId)
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(subject, out userId);
    }

    private static NotificationDto ToDto(Models.Notification notification)
    {
        return new NotificationDto
        {
            Id = notification.Id,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            RelatedEntityType = notification.RelatedEntityType,
            RelatedEntityId = notification.RelatedEntityId,
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt,
            CreatedAt = notification.CreatedAt
        };
    }
}
