using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Common.Security;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Services;
using HouseManagement.Api.Models;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace HouseManagement.Api.Controllers;

[ApiController]
[Route("api/availability")]
public sealed class AvailabilityController : ControllerBase
{
    private readonly IAvailabilityService _availability;

    public AvailabilityController(IAvailabilityService availability)
    {
        _availability = availability;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int houseHelpId, [FromQuery] DateTimeOffset? from = null, [FromQuery] DateTimeOffset? to = null)
    {
        if (houseHelpId <= 0 || (from.HasValue && to.HasValue && from >= to))
        {
            return BadRequest();
        }

        var result = await _availability.GetAsync(houseHelpId, from, to);
        if (result == null) return NotFound();

        var dto = new AvailabilityDto
        {
            HouseHelpId = result.HouseHelpId,
            WeeklySlots = result.WeeklySlots.Select(slot => new AvailabilitySlotDto
            {
                DayOfWeek = slot.DayOfWeek,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime
            }),
            Exceptions = result.Exceptions.Select(exception => new AvailabilityExceptionDto
            {
                StartsAt = exception.StartsAt,
                EndsAt = exception.EndsAt,
                Reason = exception.Reason
            })
        };

        var response = ApiResponseFactory.Create(this, dto, "Availability retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPut("/api/househelps/{houseHelpId:int}/availability")]
    public async Task<IActionResult> ReplaceWeekly(int houseHelpId, [FromBody] UpdateAvailabilityRequest request)
    {
        var slots = request.WeeklySlots.Select(slot => new HouseHelpAvailability
        {
            DayOfWeek = slot.DayOfWeek,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime,
            IsActive = true
        });

        var updated = await _availability.ReplaceWeeklyAsync(houseHelpId, slots);
        if (updated == AvailabilityUpdateResult.HouseHelpNotFound) return NotFound();
        if (updated == AvailabilityUpdateResult.Invalid) return BadRequest("Availability slots cannot overlap and must have a positive duration.");

        var result = await _availability.GetAsync(houseHelpId);
        var dto = new AvailabilityDto
        {
            HouseHelpId = result!.HouseHelpId,
            WeeklySlots = result.WeeklySlots.Select(slot => new AvailabilitySlotDto
            {
                DayOfWeek = slot.DayOfWeek,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime
            }),
            Exceptions = result.Exceptions.Select(exception => new AvailabilityExceptionDto
            {
                StartsAt = exception.StartsAt,
                EndsAt = exception.EndsAt,
                Reason = exception.Reason
            })
        };

        var response = ApiResponseFactory.Create(this, dto, "Availability updated", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.HouseHelpOnly)]
    [HttpPut("/api/availability/me")]
    public async Task<IActionResult> ReplaceOwnWeekly([FromBody] UpdateAvailabilityRequest request)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!int.TryParse(subject, out var userId))
        {
            return Unauthorized();
        }

        var houseHelpId = await _availability.GetHouseHelpIdForUserAsync(userId);
        if (!houseHelpId.HasValue)
        {
            return NotFound();
        }

        var slots = request.WeeklySlots.Select(slot => new HouseHelpAvailability
        {
            DayOfWeek = slot.DayOfWeek,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime,
            IsActive = true
        });

        var updated = await _availability.ReplaceWeeklyAsync(houseHelpId.Value, slots);
        if (updated == AvailabilityUpdateResult.Invalid) return BadRequest("Availability slots cannot overlap and must have a positive duration.");
        var result = await _availability.GetAsync(houseHelpId.Value);
        var dto = new AvailabilityDto
        {
            HouseHelpId = result!.HouseHelpId,
            WeeklySlots = result.WeeklySlots.Select(slot => new AvailabilitySlotDto
            {
                DayOfWeek = slot.DayOfWeek,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime
            }),
            Exceptions = result.Exceptions.Select(exception => new AvailabilityExceptionDto
            {
                StartsAt = exception.StartsAt,
                EndsAt = exception.EndsAt,
                Reason = exception.Reason
            })
        };

        var response = ApiResponseFactory.Create(this, dto, "Availability updated", StatusCodes.Status200OK);
        return Ok(response);
    }
}
