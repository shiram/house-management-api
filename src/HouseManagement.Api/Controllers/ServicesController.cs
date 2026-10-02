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
    private readonly IPricingCalculationService _pricingCalculation;
    private readonly IServicePricingVersionService _pricingVersions;

    public ServicesController(
        IServiceCatalogService serviceCatalog,
        IPricingCalculationService pricingCalculation,
        IServicePricingVersionService pricingVersions)
    {
        _serviceCatalog = serviceCatalog;
        _pricingCalculation = pricingCalculation;
        _pricingVersions = pricingVersions;
    }

    [HttpGet]
    public async Task<IActionResult> GetActive([FromQuery] int? page, [FromQuery] int? pageSize)
    {
        var services = await _serviceCatalog.GetActiveAsync(page, pageSize);
        var taxRatePercentage = await _serviceCatalog.GetTaxRatePercentageAsync();
        var currency = await _serviceCatalog.GetCurrencyCodeAsync();
        var dtos = services.Select(service => ToDto(service, taxRatePercentage, currency));

        var response = ApiResponseFactory.Create(this, dtos, "Active services retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var service = await _serviceCatalog.GetActiveByIdAsync(id);
        if (service == null) return NotFound();

        var taxRatePercentage = await _serviceCatalog.GetTaxRatePercentageAsync();
        var currency = await _serviceCatalog.GetCurrencyCodeAsync();
        var dto = ToDto(service, taxRatePercentage, currency);

        var response = ApiResponseFactory.Create(this, dto, "Service retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    // Public, non-persisting price quote so anonymous and authenticated clients can see the
    // expected charge for fixed, per-unit, and time-based services before submitting a booking.
    // Uses the same calculator booking creation uses, so a quote and the resulting booking total
    // never diverge for the same inputs.
    [HttpPost("{id:int}/quote")]
    public async Task<IActionResult> Quote(int id, [FromBody] ServiceQuoteRequest request)
    {
        var service = await _serviceCatalog.GetActiveByIdAsync(id);
        if (service == null) return NotFound();

        var holidayDates = service.Surcharges.Any(surcharge => surcharge.TriggerType == SurchargeTriggerType.Holiday)
            ? await _serviceCatalog.GetHolidayDatesAsync()
            : null;

        var result = _pricingCalculation.Calculate(
            service,
            request.ScheduledStart,
            request.ScheduledEnd,
            request.PricingItems,
            request.Address,
            holidayDates);
        if (!result.Succeeded)
        {
            return BadRequest(ApiResponseFactory.Create<object?>(this, null, result.Error!, StatusCodes.Status400BadRequest));
        }

        var taxRatePercentage = service.IsTaxable ? await _serviceCatalog.GetTaxRatePercentageAsync() : 0m;
        var taxAmount = _pricingCalculation.CalculateTax(result.Subtotal, taxRatePercentage);
        var currency = await _serviceCatalog.GetCurrencyCodeAsync();
        var priceLines = result.PriceLines.Select(line => new BookingPriceLineDto
        {
            Description = line.Description,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            LineTotal = line.LineTotal
        }).ToList();

        if (taxAmount > 0)
        {
            priceLines.Add(new BookingPriceLineDto
            {
                Description = $"Tax ({taxRatePercentage:0.##}%)",
                Quantity = 1,
                UnitPrice = taxAmount,
                LineTotal = taxAmount
            });
        }

        var quote = new ServiceQuoteResponse
        {
            ServiceId = service.Id,
            PricingMode = service.PricingMode,
            PriceLines = priceLines,
            Subtotal = result.Subtotal,
            TaxRatePercentage = taxAmount > 0 ? taxRatePercentage : 0m,
            TaxAmount = taxAmount,
            Total = result.Subtotal + taxAmount,
            Currency = currency,
            CalculatedAt = DateTimeOffset.UtcNow
        };

        var response = ApiResponseFactory.Create(this, quote, "Quote calculated", StatusCodes.Status200OK);
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
            IsTaxable = request.IsTaxable,
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
        existing.IsTaxable = request.IsTaxable;

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

    // T388: manager/admin administration of the time-based rate policy, service fees,
    // surcharges, and effective-dated pricing versions. These never affect an already-created
    // booking's snapshot; see BookingService for how a booking's price is fixed at creation time.

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPut("{serviceId:int}/time-pricing-policy")]
    public async Task<IActionResult> UpsertTimePricingPolicy(int serviceId, [FromBody] UpsertServiceTimePricingPolicyRequest request)
    {
        var result = await _serviceCatalog.UpsertTimePricingPolicyAsync(serviceId, new ServiceTimePricingPolicy
        {
            BillingUnit = request.BillingUnit,
            UnitPrice = request.UnitPrice,
            MinimumBillableDurationMinutes = request.MinimumBillableDurationMinutes,
            BillingIncrementMinutes = request.BillingIncrementMinutes,
            RoundingPolicy = request.RoundingPolicy,
            OvertimeThresholdMinutes = request.OvertimeThresholdMinutes,
            OvertimeUnitPrice = request.OvertimeUnitPrice
        });

        if (!result.ServiceExists) return NotFound();
        if (!result.IsValid)
        {
            return BadRequest(ApiResponseFactory.Create<object?>(
                this,
                null,
                "The time pricing policy is invalid. Check unit price, durations, and overtime fields.",
                StatusCodes.Status400BadRequest));
        }

        var response = ApiResponseFactory.Create(this, ToTimePricingPolicyDto(result.Policy!), "Time pricing policy updated", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpGet("{serviceId:int}/fees")]
    public async Task<IActionResult> GetFees(int serviceId)
    {
        var fees = await _serviceCatalog.GetFeesAsync(serviceId);
        var response = ApiResponseFactory.Create(this, fees.Select(ToFeeDto), "Service fees retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPost("{serviceId:int}/fees")]
    public async Task<IActionResult> CreateFee(int serviceId, [FromBody] CreateServiceFeeRequest request)
    {
        var result = await _serviceCatalog.CreateFeeAsync(serviceId, new ServiceFee
        {
            Name = request.Name,
            AdjustmentType = request.AdjustmentType,
            Amount = request.Amount,
            IsActive = true
        });

        if (!result.ServiceExists) return NotFound();
        if (!result.IsValid)
        {
            return BadRequest(ApiResponseFactory.Create<object?>(
                this,
                null,
                "The fee is invalid. A percentage amount must be between 0 and 100; a fixed amount must be positive.",
                StatusCodes.Status400BadRequest));
        }

        var response = ApiResponseFactory.Create(this, ToFeeDto(result.Fee!), "Service fee created", StatusCodes.Status201Created);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPut("{serviceId:int}/fees/{feeId:int}")]
    public async Task<IActionResult> UpdateFee(int serviceId, int feeId, [FromBody] UpdateServiceFeeRequest request)
    {
        var result = await _serviceCatalog.UpdateFeeAsync(serviceId, feeId, new ServiceFee
        {
            Name = request.Name,
            AdjustmentType = request.AdjustmentType,
            Amount = request.Amount
        });

        if (!result.Exists) return NotFound();
        if (!result.IsValid)
        {
            return BadRequest(ApiResponseFactory.Create<object?>(
                this,
                null,
                "The fee is invalid. A percentage amount must be between 0 and 100; a fixed amount must be positive.",
                StatusCodes.Status400BadRequest));
        }

        var response = ApiResponseFactory.Create<object?>(this, null, "Service fee updated", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPut("{serviceId:int}/fees/{feeId:int}/activate")]
    public async Task<IActionResult> SetFeeActive(int serviceId, int feeId, [FromQuery] bool active = true)
    {
        if (!await _serviceCatalog.SetFeeActiveAsync(serviceId, feeId, active))
        {
            return NotFound();
        }

        var response = ApiResponseFactory.Create<object?>(this, null, "Service fee status updated", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpGet("{serviceId:int}/surcharges")]
    public async Task<IActionResult> GetSurcharges(int serviceId)
    {
        var surcharges = await _serviceCatalog.GetSurchargesAsync(serviceId);
        var response = ApiResponseFactory.Create(this, surcharges.Select(ToSurchargeDto), "Service surcharges retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPost("{serviceId:int}/surcharges")]
    public async Task<IActionResult> CreateSurcharge(int serviceId, [FromBody] CreateServiceSurchargeRequest request)
    {
        var result = await _serviceCatalog.CreateSurchargeAsync(serviceId, new ServiceSurcharge
        {
            Name = request.Name,
            TriggerType = request.TriggerType,
            AdjustmentType = request.AdjustmentType,
            Amount = request.Amount,
            AfterHoursStartMinutes = request.AfterHoursStartMinutes,
            AfterHoursEndMinutes = request.AfterHoursEndMinutes,
            UrgentLeadTimeMinutes = request.UrgentLeadTimeMinutes,
            LocationMatch = request.LocationMatch,
            IsActive = true
        });

        if (!result.ServiceExists) return NotFound();
        if (!result.IsValid)
        {
            return BadRequest(ApiResponseFactory.Create<object?>(
                this,
                null,
                "The surcharge is invalid. Check the amount and that only the fields matching the trigger type are set.",
                StatusCodes.Status400BadRequest));
        }

        var response = ApiResponseFactory.Create(this, ToSurchargeDto(result.Surcharge!), "Service surcharge created", StatusCodes.Status201Created);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPut("{serviceId:int}/surcharges/{surchargeId:int}")]
    public async Task<IActionResult> UpdateSurcharge(int serviceId, int surchargeId, [FromBody] UpdateServiceSurchargeRequest request)
    {
        var result = await _serviceCatalog.UpdateSurchargeAsync(serviceId, surchargeId, new ServiceSurcharge
        {
            Name = request.Name,
            TriggerType = request.TriggerType,
            AdjustmentType = request.AdjustmentType,
            Amount = request.Amount,
            AfterHoursStartMinutes = request.AfterHoursStartMinutes,
            AfterHoursEndMinutes = request.AfterHoursEndMinutes,
            UrgentLeadTimeMinutes = request.UrgentLeadTimeMinutes,
            LocationMatch = request.LocationMatch
        });

        if (!result.Exists) return NotFound();
        if (!result.IsValid)
        {
            return BadRequest(ApiResponseFactory.Create<object?>(
                this,
                null,
                "The surcharge is invalid. Check the amount and that only the fields matching the trigger type are set.",
                StatusCodes.Status400BadRequest));
        }

        var response = ApiResponseFactory.Create<object?>(this, null, "Service surcharge updated", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPut("{serviceId:int}/surcharges/{surchargeId:int}/activate")]
    public async Task<IActionResult> SetSurchargeActive(int serviceId, int surchargeId, [FromQuery] bool active = true)
    {
        if (!await _serviceCatalog.SetSurchargeActiveAsync(serviceId, surchargeId, active))
        {
            return NotFound();
        }

        var response = ApiResponseFactory.Create<object?>(this, null, "Service surcharge status updated", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpGet("{serviceId:int}/pricing-versions")]
    public async Task<IActionResult> GetPricingVersions(int serviceId)
    {
        var service = await _serviceCatalog.GetByIdAsync(serviceId);
        if (service == null) return NotFound();

        var versions = await _pricingVersions.GetVersionsAsync(serviceId);
        var response = ApiResponseFactory.Create(this, versions.Select(ToPricingVersionDto), "Pricing versions retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPost("{serviceId:int}/pricing-versions")]
    public async Task<IActionResult> CreatePricingVersionDraft(int serviceId, [FromBody] CreateServicePricingVersionRequest request)
    {
        var service = await _serviceCatalog.GetByIdAsync(serviceId);
        if (service == null) return NotFound();

        var draft = new ServicePricingVersion
        {
            EffectiveFrom = request.EffectiveFrom,
            BasePrice = request.BasePrice,
            Units = request.Units.Select(unit => new ServicePricingVersionUnit
            {
                UnitName = unit.UnitName,
                UnitPrice = unit.UnitPrice
            }).ToList(),
            TimeBillingUnit = request.TimeBillingUnit,
            TimeUnitPrice = request.TimeUnitPrice,
            MinimumBillableDurationMinutes = request.MinimumBillableDurationMinutes,
            BillingIncrementMinutes = request.BillingIncrementMinutes,
            TimeRoundingPolicy = request.TimeRoundingPolicy,
            OvertimeThresholdMinutes = request.OvertimeThresholdMinutes,
            OvertimeUnitPrice = request.OvertimeUnitPrice
        };

        var created = await _pricingVersions.CreateDraftAsync(serviceId, draft);
        if (created == null)
        {
            return BadRequest(ApiResponseFactory.Create<object?>(
                this,
                null,
                "The pricing version snapshot is invalid for this service's current pricing mode.",
                StatusCodes.Status400BadRequest));
        }

        var response = ApiResponseFactory.Create(this, ToPricingVersionDto(created), "Pricing version draft created", StatusCodes.Status201Created);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPost("pricing-versions/{versionId:int}/publish")]
    public async Task<IActionResult> PublishPricingVersion(int versionId)
    {
        var result = await _pricingVersions.PublishAsync(versionId);
        if (!result.Succeeded)
        {
            return result.Error == "The requested pricing version was not found."
                ? NotFound(ApiResponseFactory.Create<object?>(this, null, result.Error, StatusCodes.Status404NotFound))
                : Conflict(ApiResponseFactory.Create<object?>(this, null, result.Error!, StatusCodes.Status409Conflict));
        }

        var response = ApiResponseFactory.Create(this, ToPricingVersionDto(result.Version!), "Pricing version published", StatusCodes.Status200OK);
        return Ok(response);
    }

    private static ServiceDto ToDto(Service service, decimal taxRatePercentage = 0m, string? currency = null)
    {
        return new ServiceDto
        {
            Id = service.Id,
            Code = service.Code,
            Name = service.Name,
            Description = service.Description,
            BasePrice = service.BasePrice,
            PricingMode = service.PricingMode,
            IsTaxable = service.IsTaxable,
            PriceRules = service.PriceRules.Select(ToPriceRuleDto),
            TimePricing = service.TimePricingPolicy == null ? null : ToTimePricingSummaryDto(service.TimePricingPolicy),
            Fees = service.Fees.Where(fee => fee.IsActive).Select(ToFeeSummaryDto),
            Surcharges = service.Surcharges.Where(surcharge => surcharge.IsActive).Select(ToSurchargeSummaryDto),
            TaxRatePercentage = service.IsTaxable ? taxRatePercentage : 0m,
            Currency = currency ?? HouseManagement.Api.Common.PricingSettings.DefaultCurrencyCode,
            IsActive = service.IsActive,
            CreatedAt = service.CreatedAt,
            UpdatedAt = service.UpdatedAt
        };
    }

    private static ServiceTimePricingSummaryDto ToTimePricingSummaryDto(ServiceTimePricingPolicy policy)
    {
        return new ServiceTimePricingSummaryDto
        {
            BillingUnit = policy.BillingUnit,
            UnitPrice = policy.UnitPrice,
            MinimumBillableDurationMinutes = policy.MinimumBillableDurationMinutes,
            BillingIncrementMinutes = policy.BillingIncrementMinutes,
            RoundingPolicy = policy.RoundingPolicy,
            OvertimeThresholdMinutes = policy.OvertimeThresholdMinutes,
            OvertimeUnitPrice = policy.OvertimeUnitPrice
        };
    }

    private static ServiceFeeSummaryDto ToFeeSummaryDto(ServiceFee fee)
    {
        return new ServiceFeeSummaryDto
        {
            Name = fee.Name,
            AdjustmentType = fee.AdjustmentType,
            Amount = fee.Amount
        };
    }

    private static ServiceSurchargeSummaryDto ToSurchargeSummaryDto(ServiceSurcharge surcharge)
    {
        return new ServiceSurchargeSummaryDto
        {
            Name = surcharge.Name,
            TriggerType = surcharge.TriggerType,
            AdjustmentType = surcharge.AdjustmentType,
            Amount = surcharge.Amount,
            AfterHoursStartMinutes = surcharge.AfterHoursStartMinutes,
            AfterHoursEndMinutes = surcharge.AfterHoursEndMinutes,
            UrgentLeadTimeMinutes = surcharge.UrgentLeadTimeMinutes,
            LocationMatch = surcharge.LocationMatch
        };
    }

    private static ServiceTimePricingPolicyDto ToTimePricingPolicyDto(ServiceTimePricingPolicy policy)
    {
        return new ServiceTimePricingPolicyDto
        {
            Id = policy.Id,
            ServiceId = policy.ServiceId,
            BillingUnit = policy.BillingUnit,
            UnitPrice = policy.UnitPrice,
            MinimumBillableDurationMinutes = policy.MinimumBillableDurationMinutes,
            BillingIncrementMinutes = policy.BillingIncrementMinutes,
            RoundingPolicy = policy.RoundingPolicy,
            OvertimeThresholdMinutes = policy.OvertimeThresholdMinutes,
            OvertimeUnitPrice = policy.OvertimeUnitPrice,
            CreatedAt = policy.CreatedAt,
            UpdatedAt = policy.UpdatedAt
        };
    }

    private static ServiceFeeDto ToFeeDto(ServiceFee fee)
    {
        return new ServiceFeeDto
        {
            Id = fee.Id,
            ServiceId = fee.ServiceId,
            Name = fee.Name,
            AdjustmentType = fee.AdjustmentType,
            Amount = fee.Amount,
            IsActive = fee.IsActive,
            CreatedAt = fee.CreatedAt,
            UpdatedAt = fee.UpdatedAt
        };
    }

    private static ServiceSurchargeDto ToSurchargeDto(ServiceSurcharge surcharge)
    {
        return new ServiceSurchargeDto
        {
            Id = surcharge.Id,
            ServiceId = surcharge.ServiceId,
            Name = surcharge.Name,
            TriggerType = surcharge.TriggerType,
            AdjustmentType = surcharge.AdjustmentType,
            Amount = surcharge.Amount,
            IsActive = surcharge.IsActive,
            AfterHoursStartMinutes = surcharge.AfterHoursStartMinutes,
            AfterHoursEndMinutes = surcharge.AfterHoursEndMinutes,
            UrgentLeadTimeMinutes = surcharge.UrgentLeadTimeMinutes,
            LocationMatch = surcharge.LocationMatch,
            CreatedAt = surcharge.CreatedAt,
            UpdatedAt = surcharge.UpdatedAt
        };
    }

    private static ServicePricingVersionDto ToPricingVersionDto(ServicePricingVersion version)
    {
        return new ServicePricingVersionDto
        {
            Id = version.Id,
            ServiceId = version.ServiceId,
            Status = version.Status,
            PricingMode = version.PricingMode,
            EffectiveFrom = version.EffectiveFrom,
            EffectiveTo = version.EffectiveTo,
            BasePrice = version.BasePrice,
            TimeBillingUnit = version.TimeBillingUnit,
            TimeUnitPrice = version.TimeUnitPrice,
            MinimumBillableDurationMinutes = version.MinimumBillableDurationMinutes,
            BillingIncrementMinutes = version.BillingIncrementMinutes,
            TimeRoundingPolicy = version.TimeRoundingPolicy,
            OvertimeThresholdMinutes = version.OvertimeThresholdMinutes,
            OvertimeUnitPrice = version.OvertimeUnitPrice,
            Units = version.Units.Select(unit => new ServicePricingVersionUnitDto
            {
                UnitName = unit.UnitName,
                UnitPrice = unit.UnitPrice
            }),
            CreatedAt = version.CreatedAt,
            PublishedAt = version.PublishedAt
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
