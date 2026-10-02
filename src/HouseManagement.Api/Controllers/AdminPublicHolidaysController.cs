using HouseManagement.Api.Common;
using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Common.Security;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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
    private readonly IAuditLogService _auditLogs;

    public AdminPublicHolidaysController(IServiceCatalogService serviceCatalog, IAuditLogService auditLogs)
    {
        _serviceCatalog = serviceCatalog;
        _auditLogs = auditLogs;
    }

    private int? CurrentUserId()
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(subject, out var userId) ? userId : null;
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

        await _auditLogs.LogAsync(
            AuditEventTypes.PublicHolidayCreated,
            nameof(PublicHoliday),
            entityId: created.Id,
            userId: CurrentUserId(),
            details: $"Date: {created.Date:yyyy-MM-dd}, Name: {created.Name}");

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

        await _auditLogs.LogAsync(
            AuditEventTypes.PublicHolidayDeleted,
            nameof(PublicHoliday),
            entityId: id,
            userId: CurrentUserId());

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
