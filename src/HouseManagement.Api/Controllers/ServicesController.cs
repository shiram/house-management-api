using HouseManagement.Api.Common.Api;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Services;
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
}
