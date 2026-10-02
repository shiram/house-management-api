using System.ComponentModel.DataAnnotations;
using HouseManagement.Api.Models;

namespace HouseManagement.Api.DTOs;

public sealed class ServiceDto
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public ServicePricingMode PricingMode { get; set; }
    public bool IsTaxable { get; set; }
    public IEnumerable<ServicePriceRuleDto> PriceRules { get; set; } = [];

    // Public pricing projection (T389): lets clients understand how a service will be charged
    // (time-based rate/rounding/overtime, always-applied fees, and conditional surcharge
    // triggers) without first calling the quote endpoint. These are read-only, non-sensitive
    // summaries derived from the manager/admin pricing records owned by T388; they intentionally
    // omit internal ids, inactive records, and audit timestamps.
    public ServiceTimePricingSummaryDto? TimePricing { get; set; }
    public IEnumerable<ServiceFeeSummaryDto> Fees { get; set; } = [];
    public IEnumerable<ServiceSurchargeSummaryDto> Surcharges { get; set; } = [];
    public decimal TaxRatePercentage { get; set; }
    public string Currency { get; set; } = HouseManagement.Api.Common.PricingSettings.DefaultCurrencyCode;

    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

// Public, non-sensitive projection of a service's time-based pricing policy (T389).
public sealed class ServiceTimePricingSummaryDto
{
    public TimePricingUnit BillingUnit { get; set; }
    public decimal UnitPrice { get; set; }
    public int MinimumBillableDurationMinutes { get; set; }
    public int BillingIncrementMinutes { get; set; }
    public TimeRoundingPolicy RoundingPolicy { get; set; }
    public int? OvertimeThresholdMinutes { get; set; }
    public decimal? OvertimeUnitPrice { get; set; }
}

// Public, non-sensitive projection of an always-applied service fee (T389).
public sealed class ServiceFeeSummaryDto
{
    public string Name { get; set; } = null!;
    public PricingAdjustmentType AdjustmentType { get; set; }
    public decimal Amount { get; set; }
}

// Public, non-sensitive projection of a conditional service surcharge and the trigger context
// that activates it (T389).
public sealed class ServiceSurchargeSummaryDto
{
    public string Name { get; set; } = null!;
    public SurchargeTriggerType TriggerType { get; set; }
    public PricingAdjustmentType AdjustmentType { get; set; }
    public decimal Amount { get; set; }
    public int? AfterHoursStartMinutes { get; set; }
    public int? AfterHoursEndMinutes { get; set; }
    public int? UrgentLeadTimeMinutes { get; set; }
    public string? LocationMatch { get; set; }
}

public sealed class CreateServiceRequest
{
    [Required]
    [StringLength(64, MinimumLength = 2)]
    [RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]*$")]
    public string Code { get; set; } = null!;

    [Required]
    [StringLength(128, MinimumLength = 2)]
    public string Name { get; set; } = null!;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal BasePrice { get; set; }

    [EnumDataType(typeof(ServicePricingMode))]
    public ServicePricingMode PricingMode { get; set; } = ServicePricingMode.Fixed;

    public bool IsTaxable { get; set; } = true;
}

public sealed class UpdateServiceRequest
{
    [Required]
    [StringLength(64, MinimumLength = 2)]
    [RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]*$")]
    public string Code { get; set; } = null!;

    [Required]
    [StringLength(128, MinimumLength = 2)]
    public string Name { get; set; } = null!;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal BasePrice { get; set; }

    [EnumDataType(typeof(ServicePricingMode))]
    public ServicePricingMode PricingMode { get; set; } = ServicePricingMode.Fixed;

    public bool IsTaxable { get; set; } = true;
}

public sealed class ServicePriceRuleDto
{
    public int Id { get; set; }
    public string UnitName { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateServicePriceRuleRequest
{
    [Required]
    [StringLength(128, MinimumLength = 2)]
    public string UnitName { get; set; } = null!;

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal UnitPrice { get; set; }
}

public sealed class UpdateServicePriceRuleRequest
{
    [Required]
    [StringLength(128, MinimumLength = 2)]
    public string UnitName { get; set; } = null!;

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal UnitPrice { get; set; }
}

public sealed class ServiceQuoteRequest
{
    public DateTimeOffset ScheduledStart { get; set; }
    public DateTimeOffset ScheduledEnd { get; set; }
    public IEnumerable<BookingPriceItemRequest> PricingItems { get; set; } = [];

    // Optional booking address context, needed only to evaluate a Location surcharge trigger.
    public ServiceAddressRequest? Address { get; set; }
}

public sealed class ServiceQuoteResponse
{
    public int ServiceId { get; set; }
    public ServicePricingMode PricingMode { get; set; }
    public IEnumerable<BookingPriceLineDto> PriceLines { get; set; } = [];
    public decimal Subtotal { get; set; }
    public decimal TaxRatePercentage { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = HouseManagement.Api.Common.PricingSettings.DefaultCurrencyCode;
    public DateTimeOffset CalculatedAt { get; set; }
}
