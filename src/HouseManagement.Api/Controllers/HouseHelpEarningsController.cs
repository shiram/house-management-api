using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Common.Security;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseManagement.Api.Controllers;

[ApiController]
[Route("api/househelp-earnings")]
public sealed class HouseHelpEarningsController : ControllerBase
{
    private readonly IHouseHelpEarningsService _earnings;

    public HouseHelpEarningsController(IHouseHelpEarningsService earnings)
    {
        _earnings = earnings;
    }

    [Authorize(Policy = AuthorizationPolicies.HouseHelpOnly)]
    [HttpGet("me")]
    public async Task<IActionResult> GetMine([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to)
    {
        if (!IsValidDateRange(from, to))
        {
            return BadRequest(ApiResponseFactory.Create<object?>(
                this,
                null,
                "The earnings date range is invalid.",
                StatusCodes.Status400BadRequest));
        }

        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!int.TryParse(subject, out var userId))
        {
            return Unauthorized();
        }

        var report = await _earnings.GetForUserAsync(userId, from, to);
        if (report == null)
        {
            return NotFound();
        }

        var response = ApiResponseFactory.Create(this, report, "HouseHelp earnings retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpGet("{houseHelpId:int}")]
    public async Task<IActionResult> GetForHouseHelp(
        int houseHelpId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to)
    {
        if (!IsValidDateRange(from, to))
        {
            return BadRequest(ApiResponseFactory.Create<object?>(
                this,
                null,
                "The earnings date range is invalid.",
                StatusCodes.Status400BadRequest));
        }

        var report = await _earnings.GetForHouseHelpAsync(houseHelpId, from, to);
        if (report == null)
        {
            return NotFound();
        }

        var response = ApiResponseFactory.Create(this, report, "HouseHelp earnings retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpGet]
    public async Task<IActionResult> GetSummaries([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to)
    {
        if (!IsValidDateRange(from, to))
        {
            return BadRequest(ApiResponseFactory.Create<object?>(
                this,
                null,
                "The earnings date range is invalid.",
                StatusCodes.Status400BadRequest));
        }

        var summaries = await _earnings.GetSummariesAsync(from, to);
        var response = ApiResponseFactory.Create(this, summaries, "HouseHelp earnings summary retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    private static bool IsValidDateRange(DateTimeOffset? from, DateTimeOffset? to)
    {
        return !from.HasValue || !to.HasValue || from.Value <= to.Value;
    }
}
