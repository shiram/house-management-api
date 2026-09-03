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
    public async Task<IActionResult> GetActive([FromQuery] int? page, [FromQuery] int? pageSize)
    {
        var services = await _serviceCatalog.GetActiveAsync(page, pageSize);
        var dtos = services.Select(service => new ServiceDto
        {
            Id = service.Id,
            Code = service.Code,
            Name = service.Name,
            Description = service.Description,
            BasePrice = service.BasePrice,
            PricingMode = service.PricingMode,
            PriceRules = service.PriceRules.Select(ToPriceRuleDto),
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
            PricingMode = service.PricingMode,
            PriceRules = service.PriceRules.Select(ToPriceRuleDto),
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
            PricingMode = request.PricingMode,
            IsActive = true
        });

        if (created == null)
        {
            return Conflict(ApiResponseFactory.Create<object?>(this, null, "A service with this code already exists", StatusCodes.Status409Conflict));
        }

        var response = ApiResponseFactory.Create(this, ToDto(created), "Service created", StatusCodes.Status201Created);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateServiceRequest request)
    {
        var existing = await _serviceCatalog.GetByIdAsync(id);
        if (existing == null) return NotFound();

        if (await _serviceCatalog.CodeExistsAsync(request.Code, id))
        {
            return Conflict(ApiResponseFactory.Create<object?>(this, null, "A service with this code already exists", StatusCodes.Status409Conflict));
        }

        existing.Code = request.Code;
        existing.Name = request.Name;
        existing.Description = request.Description;
        existing.BasePrice = request.BasePrice;
        existing.PricingMode = request.PricingMode;

        await _serviceCatalog.UpdateAsync(existing);
        var response = ApiResponseFactory.Create(this, ToDto(existing), "Service updated", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPut("{id:int}/activate")]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool active = true)
    {
        var updated = await _serviceCatalog.SetActiveAsync(id, active);
        if (!updated) return NotFound();

        var response = ApiResponseFactory.Create<object?>(this, null, "Service status updated", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPost("{serviceId:int}/pricing-rules")]
    public async Task<IActionResult> CreatePriceRule(int serviceId, [FromBody] CreateServicePriceRuleRequest request)
    {
        var created = await _serviceCatalog.CreatePriceRuleAsync(serviceId, new ServicePriceRule
        {
            UnitName = request.UnitName,
            UnitPrice = request.UnitPrice,
            IsActive = true
        });
        if (created == null)
        {
            return Conflict(ApiResponseFactory.Create<object?>(
                this,
                null,
                "The service was not found or already has a pricing rule with this unit name.",
                StatusCodes.Status409Conflict));
        }

        var response = ApiResponseFactory.Create(this, ToPriceRuleDto(created), "Pricing rule created", StatusCodes.Status201Created);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPut("{serviceId:int}/pricing-rules/{ruleId:int}")]
    public async Task<IActionResult> UpdatePriceRule(
        int serviceId,
        int ruleId,
        [FromBody] UpdateServicePriceRuleRequest request)
    {
        var result = await _serviceCatalog.UpdatePriceRuleAsync(serviceId, ruleId, new ServicePriceRule
        {
            UnitName = request.UnitName,
            UnitPrice = request.UnitPrice
        });
        if (!result.Exists)
        {
            return NotFound();
        }
        if (result.HasDuplicateUnitName)
        {
            return Conflict(ApiResponseFactory.Create<object?>(
                this,
                null,
                "The service already has a pricing rule with this unit name.",
                StatusCodes.Status409Conflict));
        }

        var response = ApiResponseFactory.Create<object?>(this, null, "Pricing rule updated", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPut("{serviceId:int}/pricing-rules/{ruleId:int}/activate")]
    public async Task<IActionResult> SetPriceRuleActive(int serviceId, int ruleId, [FromQuery] bool active = true)
    {
        if (!await _serviceCatalog.SetPriceRuleActiveAsync(serviceId, ruleId, active))
        {
            return NotFound();
        }

        var response = ApiResponseFactory.Create<object?>(this, null, "Pricing rule status updated", StatusCodes.Status200OK);
        return Ok(response);
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
            PricingMode = service.PricingMode,
            PriceRules = service.PriceRules.Select(ToPriceRuleDto),
            IsActive = service.IsActive,
            CreatedAt = service.CreatedAt,
            UpdatedAt = service.UpdatedAt
        };
    }

    private static ServicePriceRuleDto ToPriceRuleDto(ServicePriceRule rule)
    {
        return new ServicePriceRuleDto
        {
            Id = rule.Id,
            UnitName = rule.UnitName,
            UnitPrice = rule.UnitPrice,
            IsActive = rule.IsActive
        };
    }
}
