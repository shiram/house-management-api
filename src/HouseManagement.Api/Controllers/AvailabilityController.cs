using HouseManagement.Api.Common.Api;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Services;
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
}
