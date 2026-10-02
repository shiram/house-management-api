using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Common.Security;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseManagement.Api.Controllers;

// Manager/admin administration of the global public holiday calendar (T388), used to evaluate
// every service's Holiday surcharge trigger. Not service-specific, so it lives outside
// ServicesController's /api/services/{serviceId}/... routes.
[ApiController]
[Route("api/admin/public-holidays")]
[Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
public sealed class AdminPublicHolidaysController : ControllerBase
{
    private readonly IServiceCatalogService _serviceCatalog;

    public AdminPublicHolidaysController(IServiceCatalogService serviceCatalog)
    {
        _serviceCatalog = serviceCatalog;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var holidays = await _serviceCatalog.GetPublicHolidaysAsync();
        var response = ApiResponseFactory.Create(this, holidays.Select(ToDto), "Public holidays retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePublicHolidayRequest request)
    {
        var created = await _serviceCatalog.CreatePublicHolidayAsync(new PublicHoliday
        {
            Date = request.Date,
            Name = request.Name
        });

        if (created == null)
        {
            return Conflict(ApiResponseFactory.Create<object?>(
                this,
                null,
                "A public holiday already exists for this date.",
                StatusCodes.Status409Conflict));
        }

        var response = ApiResponseFactory.Create(this, ToDto(created), "Public holiday created", StatusCodes.Status201Created);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _serviceCatalog.DeletePublicHolidayAsync(id))
        {
            return NotFound();
        }

        var response = ApiResponseFactory.Create<object?>(this, null, "Public holiday removed", StatusCodes.Status200OK);
        return Ok(response);
    }

    private static PublicHolidayDto ToDto(PublicHoliday holiday)
    {
        return new PublicHolidayDto
        {
            Id = holiday.Id,
            Date = holiday.Date,
            Name = holiday.Name,
            CreatedAt = holiday.CreatedAt
        };
    }
}
