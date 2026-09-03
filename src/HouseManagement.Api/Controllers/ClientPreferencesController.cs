using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HouseManagement.Api.Common.Api;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/clients/me/preferred-househelps")]
public sealed class ClientPreferencesController : ControllerBase
{
    private readonly IClientHouseHelpPreferenceService _preferences;

    public ClientPreferencesController(IClientHouseHelpPreferenceService preferences)
    {
        _preferences = preferences;
    }

    [HttpPost("{houseHelpId:int}")]
    public async Task<IActionResult> Add(int houseHelpId)
    {
        if (!TryGetAuthenticatedUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _preferences.AddAsync(userId, houseHelpId);
        if (result.Preference == null)
        {
            return BadRequest(ApiResponseFactory.Create<object?>(this, null, result.Error!, StatusCodes.Status400BadRequest));
        }

        var response = ApiResponseFactory.Create<object?>(
            this,
            null,
            "HouseHelp added to preferences",
            StatusCodes.Status201Created);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpDelete("{houseHelpId:int}")]
    public async Task<IActionResult> Remove(int houseHelpId)
    {
        if (!TryGetAuthenticatedUserId(out var userId))
        {
            return Unauthorized();
        }

        if (!await _preferences.RemoveAsync(userId, houseHelpId))
        {
            return NotFound();
        }

        var response = ApiResponseFactory.Create<object?>(
            this,
            null,
            "HouseHelp removed from preferences",
            StatusCodes.Status200OK);
        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? page, [FromQuery] int? pageSize)
    {
        if (!TryGetAuthenticatedUserId(out var userId))
        {
            return Unauthorized();
        }

        var preferences = await _preferences.GetForClientAsync(userId, page, pageSize);
        var response = ApiResponseFactory.Create(
            this,
            preferences.Select(ToDto),
            "Preferred HouseHelps retrieved",
            StatusCodes.Status200OK);
        return Ok(response);
    }

    private bool TryGetAuthenticatedUserId(out int userId)
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(subject, out userId);
    }

    private static PreferredHouseHelpDto ToDto(ClientHouseHelpPreference preference)
    {
        return new PreferredHouseHelpDto
        {
            Id = preference.HouseHelp.Id,
            FirstName = preference.HouseHelp.FirstName,
            LastName = preference.HouseHelp.LastName,
            City = preference.HouseHelp.City,
            Skills = preference.HouseHelp.Skills.Select(skill => skill.ServiceName),
            PreferredAt = preference.CreatedAt
        };
    }
}
