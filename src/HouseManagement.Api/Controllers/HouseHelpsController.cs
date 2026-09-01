using System.Linq;
using System.Threading.Tasks;
using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Common.Security;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HouseManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HouseHelpsController : ControllerBase
{
    private readonly IHouseHelpService _svc;

    public HouseHelpsController(IHouseHelpService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? city, [FromQuery] string? skill, [FromQuery] int? page, [FromQuery] int? pageSize)
    {
        var items = await _svc.GetFilteredAsync(city, skill, true, page, pageSize);
        var dtos = items.Select(ToPublicDto);
        var response = ApiResponseFactory.Create(this, dtos, "HouseHelp directory retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var h = await _svc.GetByIdAsync(id);
        if (h == null || !h.IsActive) return NotFound();

        var response = ApiResponseFactory.Create(this, ToPublicDto(h), "HouseHelp retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    private static PublicHouseHelpDto ToPublicDto(HouseHelp houseHelp)
    {
        return new PublicHouseHelpDto
        {
            Id = houseHelp.Id,
            FirstName = houseHelp.FirstName,
            LastName = houseHelp.LastName,
            City = houseHelp.City,
            Skills = houseHelp.Skills.Select(s => s.ServiceName)
        };
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHouseHelpRequest req)
    {
        if (!ModelState.IsValid) return ValidationResponseFactory.Create(this, ModelState);

        var entity = new HouseHelp
        {
            UserId = req.UserId,
            FirstName = req.FirstName,
            LastName = req.LastName,
            Phone = req.Phone,
            City = req.City,
            Address = req.Address,
            IsActive = true
        };
        var created = await _svc.CreateAsync(entity, req.Skills);
        var dto = new HouseManagement.Api.DTOs.HouseHelpDto
        {
            Id = created.Id,
            UserId = created.UserId,
            FirstName = created.FirstName,
            LastName = created.LastName,
            Phone = created.Phone,
            City = created.City,
            Address = created.Address,
            IsActive = created.IsActive,
            Skills = created.Skills.Select(s => s.ServiceName)
        };
        var response = ApiResponseFactory.Create(this, dto, "HouseHelp created", StatusCodes.Status201Created);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateHouseHelpRequest req)
    {
        if (!ModelState.IsValid) return ValidationResponseFactory.Create(this, ModelState);

        var existing = await _svc.GetByIdAsync(id);
        if (existing == null) return NotFound();

        existing.FirstName = req.FirstName;
        existing.LastName = req.LastName;
        existing.Phone = req.Phone;
        existing.City = req.City;
        existing.Address = req.Address;

        var ok = await _svc.UpdateAsync(existing, req.Skills);
        if (!ok) return NotFound();

        var response = ApiResponseFactory.Create<object?>(this, null, "HouseHelp updated", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPut("{id}/activate")]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool active = true)
    {
        var ok = await _svc.SetActiveAsync(id, active);
        if (!ok) return NotFound();

        var response = ApiResponseFactory.Create<object?>(this, null, "HouseHelp status updated", StatusCodes.Status200OK);
        return Ok(response);
    }
}
