using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Common.Security;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseManagement.Api.Controllers;

[ApiController]
[Route("api/admin/services")]
[Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
public sealed class AdminServicesController : ControllerBase
{
    private readonly IServiceCatalogService _serviceCatalog;

    public AdminServicesController(IServiceCatalogService serviceCatalog)
    {
        _serviceCatalog = serviceCatalog;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] bool? isActive)
    {
        var services = await _serviceCatalog.GetAllAsync(page, pageSize, isActive);
        var dtos = services.Select(ToDto);

        var response = ApiResponseFactory.Create(this, dtos, "Services retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var service = await _serviceCatalog.GetByIdAsync(id);
        if (service == null) return NotFound();

        var response = ApiResponseFactory.Create(this, ToDto(service), "Service retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    private static ServiceDto ToDto(Models.Service service)
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
