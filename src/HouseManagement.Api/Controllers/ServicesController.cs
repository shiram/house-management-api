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
[Route("api/services")]
public sealed class ServicesController : ControllerBase
{
    private readonly IServiceCatalogService _serviceCatalog;

    public ServicesController(IServiceCatalogService serviceCatalog)
    {
        _serviceCatalog = serviceCatalog;
    }

    [HttpGet]
    public async Task<IActionResult> GetActive()
    {
        var services = await _serviceCatalog.GetActiveAsync();
        var dtos = services.Select(service => new ServiceDto
        {
            Id = service.Id,
            Code = service.Code,
            Name = service.Name,
            Description = service.Description,
            BasePrice = service.BasePrice,
            IsActive = service.IsActive,
            CreatedAt = service.CreatedAt,
            UpdatedAt = service.UpdatedAt
        });

        var response = ApiResponseFactory.Create(this, dtos, "Active services retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var service = await _serviceCatalog.GetActiveByIdAsync(id);
        if (service == null) return NotFound();

        var dto = new ServiceDto
        {
            Id = service.Id,
            Code = service.Code,
            Name = service.Name,
            Description = service.Description,
            BasePrice = service.BasePrice,
            IsActive = service.IsActive,
            CreatedAt = service.CreatedAt,
            UpdatedAt = service.UpdatedAt
        };

        var response = ApiResponseFactory.Create(this, dto, "Service retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateServiceRequest request)
    {
        var created = await _serviceCatalog.CreateAsync(new Service
        {
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            BasePrice = request.BasePrice,
            IsActive = true
        });

        if (created == null)
        {
            return Conflict(ApiResponseFactory.Create<object?>(this, null, "A service with this code already exists", StatusCodes.Status409Conflict));
        }

        var response = ApiResponseFactory.Create(this, ToDto(created), "Service created", StatusCodes.Status201Created);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, response);
    }

    private static ServiceDto ToDto(Service service)
    {
        return new ServiceDto
        {
            Id = service.Id,
            Code = service.Code,
            Name = service.Name,
            Description = service.Description,
            BasePrice = service.BasePrice,
            IsActive = service.IsActive,
            CreatedAt = service.CreatedAt,
            UpdatedAt = service.UpdatedAt
        };
    }
}
