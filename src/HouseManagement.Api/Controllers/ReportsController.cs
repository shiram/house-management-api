using System.Text;
using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Common.Security;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseManagement.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
public sealed class ReportsController : ControllerBase
{
    private readonly IAdvancedReportingService _reports;

    public ReportsController(IAdvancedReportingService reports)
    {
        _reports = reports;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to)
    {
        if (!IsValidDateRange(from, to))
        {
            return BadRequest(ApiResponseFactory.Create<object?>(
                this,
                null,
                "The reporting date range is invalid.",
                StatusCodes.Status400BadRequest));
        }

        var summary = await _reports.GetSummaryAsync(from, to);
        var response = ApiResponseFactory.Create(this, summary, "Report summary retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [HttpGet("bookings/export")]
    public async Task<IActionResult> ExportBookings(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] BookingStatus? status)
    {
        if (!IsValidDateRange(from, to))
        {
            return BadRequest(ApiResponseFactory.Create<object?>(
                this,
                null,
                "The reporting date range is invalid.",
                StatusCodes.Status400BadRequest));
        }

        var csv = await _reports.ExportBookingsCsvAsync(from, to, status);
        var fileName = $"booking-report-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    private static bool IsValidDateRange(DateTimeOffset? from, DateTimeOffset? to)
    {
        return !from.HasValue || !to.HasValue || from.Value <= to.Value;
    }
}
