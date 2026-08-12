using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using HouseManagement.Api.Services;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;

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
    public async Task<IActionResult> GetAll([FromQuery] string? city, [FromQuery] string? skill, [FromQuery] bool? isActive, [FromQuery] int? page, [FromQuery] int? pageSize)
    {
        var items = await _svc.GetFilteredAsync(city, skill, isActive, page, pageSize);
        var dtos = items.Select(h => new HouseManagement.Api.DTOs.HouseHelpDto
        {
            Id = h.Id,
            UserId = h.UserId,
            FirstName = h.FirstName,
            LastName = h.LastName,
            Phone = h.Phone,
            City = h.City,
            Address = h.Address,
            IsActive = h.IsActive,
            Skills = h.Skills.Select(s => s.ServiceName)
        });
        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var h = await _svc.GetByIdAsync(id);
        if (h == null) return NotFound();
        var dto = new HouseManagement.Api.DTOs.HouseHelpDto
        {
            Id = h.Id,
            UserId = h.UserId,
            FirstName = h.FirstName,
            LastName = h.LastName,
            Phone = h.Phone,
            City = h.City,
            Address = h.Address,
            IsActive = h.IsActive,
            Skills = h.Skills.Select(s => s.ServiceName)
        };
        return Ok(dto);
    }

    [Authorize(Policy = "RequireManagerOrAdmin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHouseHelpRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
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
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [Authorize(Policy = "RequireManagerOrAdmin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateHouseHelpRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var existing = await _svc.GetByIdAsync(id);
        if (existing == null) return NotFound();

        existing.FirstName = req.FirstName;
        existing.LastName = req.LastName;
        existing.Phone = req.Phone;
        existing.City = req.City;
        existing.Address = req.Address;

        var ok = await _svc.UpdateAsync(existing, req.Skills);
        if (!ok) return NotFound();
        return NoContent();
    }

    [Authorize(Policy = "RequireManagerOrAdmin")]
    [HttpPut("{id}/activate")]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool active = true)
    {
        var ok = await _svc.SetActiveAsync(id, active);
        if (!ok) return NotFound();
        return NoContent();
    }
}
