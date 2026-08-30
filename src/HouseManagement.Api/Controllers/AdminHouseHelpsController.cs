using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Common.Security;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseManagement.Api.Controllers;

[ApiController]
[Route("api/admin/househelps")]
[Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
public sealed class AdminHouseHelpsController : ControllerBase
{
    private readonly IHouseHelpService _svc;

    public AdminHouseHelpsController(IHouseHelpService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? city,
        [FromQuery] string? skill,
        [FromQuery] bool? isActive,
        [FromQuery] int? userId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize)
    {
        var items = await _svc.GetFilteredAsync(city, skill, isActive, page, pageSize, userId);
        var dtos = items.Select(ToDto);

        var response = ApiResponseFactory.Create(this, dtos, "HouseHelp directory retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var houseHelp = await _svc.GetByIdAsync(id);
        if (houseHelp == null) return NotFound();

        var response = ApiResponseFactory.Create(this, ToDto(houseHelp), "HouseHelp retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    private static HouseHelpDto ToDto(Models.HouseHelp houseHelp)
    {
        return new HouseHelpDto
        {
            Id = houseHelp.Id,
            UserId = houseHelp.UserId,
            FirstName = houseHelp.FirstName,
            LastName = houseHelp.LastName,
            Phone = houseHelp.Phone,
            City = houseHelp.City,
            Address = houseHelp.Address,
            IsActive = houseHelp.IsActive,
            Skills = houseHelp.Skills.Select(s => s.ServiceName)
        };
    }
}
