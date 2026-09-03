using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Common.Security;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseManagement.Api.Controllers;

[ApiController]
[Route("api/admin/promotions")]
[Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
public sealed class PromotionsController : ControllerBase
{
    private readonly IPromotionService _promotions;

    public PromotionsController(IPromotionService promotions)
    {
        _promotions = promotions;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] bool? isActive)
    {
        var promotions = await _promotions.GetAllAsync(page, pageSize, isActive);
        var response = ApiResponseFactory.Create(
            this,
            promotions.Select(ToDto),
            "Promotions retrieved",
            StatusCodes.Status200OK);
        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var promotion = await _promotions.GetByIdAsync(id);
        if (promotion == null)
        {
            return NotFound();
        }

        var response = ApiResponseFactory.Create(this, ToDto(promotion), "Promotion retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePromotionRequest request)
    {
        var result = await _promotions.CreateAsync(new Promotion
        {
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            EligibleServiceId = request.EligibleServiceId,
            UsageLimit = request.UsageLimit,
            IsActive = true
        });

        if (result.Promotion == null)
        {
            return BadRequest(ApiResponseFactory.Create<object?>(this, null, result.Error!, StatusCodes.Status400BadRequest));
        }

        var response = ApiResponseFactory.Create(this, ToDto(result.Promotion), "Promotion created", StatusCodes.Status201Created);
        return CreatedAtAction(nameof(Get), new { id = result.Promotion.Id }, response);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePromotionRequest request)
    {
        var result = await _promotions.UpdateAsync(new Promotion
        {
            Id = id,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            EligibleServiceId = request.EligibleServiceId,
            UsageLimit = request.UsageLimit
        });

        if (!result.Exists)
        {
            return NotFound();
        }

        if (result.Error != null)
        {
            return BadRequest(ApiResponseFactory.Create<object?>(this, null, result.Error, StatusCodes.Status400BadRequest));
        }

        var promotion = await _promotions.GetByIdAsync(id);
        var response = ApiResponseFactory.Create(this, ToDto(promotion!), "Promotion updated", StatusCodes.Status200OK);
        return Ok(response);
    }

    [HttpPut("{id:int}/activate")]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool active = true)
    {
        if (!await _promotions.SetActiveAsync(id, active))
        {
            return NotFound();
        }

        var response = ApiResponseFactory.Create<object?>(this, null, "Promotion status updated", StatusCodes.Status200OK);
        return Ok(response);
    }

    private static PromotionDto ToDto(Promotion promotion)
    {
        return new PromotionDto
        {
            Id = promotion.Id,
            Code = promotion.Code,
            Name = promotion.Name,
            Description = promotion.Description,
            DiscountType = promotion.DiscountType,
            DiscountValue = promotion.DiscountValue,
            StartsAt = promotion.StartsAt,
            EndsAt = promotion.EndsAt,
            EligibleServiceId = promotion.EligibleServiceId,
            UsageLimit = promotion.UsageLimit,
            TimesUsed = promotion.TimesUsed,
            IsActive = promotion.IsActive,
            CreatedAt = promotion.CreatedAt,
            UpdatedAt = promotion.UpdatedAt
        };
    }
}
