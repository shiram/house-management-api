using System.ComponentModel.DataAnnotations;
using HouseManagement.Api.Models;

namespace HouseManagement.Api.DTOs;

// DTOs for manager/admin pricing administration (T388): time-based rate policy, fees,
// surcharges, pricing versions, and the public holiday calendar. These are distinct from the
// public-facing ServiceDto/ServiceQuoteResponse projections, which remain T389's scope.

public sealed class ServiceTimePricingPolicyDto
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public TimePricingUnit BillingUnit { get; set; }
    public decimal UnitPrice { get; set; }
    public int MinimumBillableDurationMinutes { get; set; }
    public int BillingIncrementMinutes { get; set; }
    public TimeRoundingPolicy RoundingPolicy { get; set; }
    public int? OvertimeThresholdMinutes { get; set; }
    public decimal? OvertimeUnitPrice { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class UpsertServiceTimePricingPolicyRequest
{
    [EnumDataType(typeof(TimePricingUnit))]
    public TimePricingUnit BillingUnit { get; set; } = TimePricingUnit.Hour;

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal UnitPrice { get; set; }

    [Range(1, int.MaxValue)]
    public int MinimumBillableDurationMinutes { get; set; }

    [Range(1, int.MaxValue)]
    public int BillingIncrementMinutes { get; set; }

    [EnumDataType(typeof(TimeRoundingPolicy))]
    public TimeRoundingPolicy RoundingPolicy { get; set; } = TimeRoundingPolicy.Up;

    [Range(0, int.MaxValue)]
    public int? OvertimeThresholdMinutes { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal? OvertimeUnitPrice { get; set; }
}

public sealed class ServiceFeeDto
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public string Name { get; set; } = null!;
    public PricingAdjustmentType AdjustmentType { get; set; }
    public decimal Amount { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class CreateServiceFeeRequest
{
    [Required]
    [StringLength(128, MinimumLength = 2)]
    public string Name { get; set; } = null!;

    [EnumDataType(typeof(PricingAdjustmentType))]
    public PricingAdjustmentType AdjustmentType { get; set; } = PricingAdjustmentType.FixedAmount;

    // A fixed currency amount, or a percentage (0-100] of the base charge subtotal, depending on
    // AdjustmentType. The exact range is enforced server-side since it depends on AdjustmentType.
    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal Amount { get; set; }
}

public sealed class UpdateServiceFeeRequest
{
    [Required]
    [StringLength(128, MinimumLength = 2)]
    public string Name { get; set; } = null!;

    [EnumDataType(typeof(PricingAdjustmentType))]
    public PricingAdjustmentType AdjustmentType { get; set; } = PricingAdjustmentType.FixedAmount;

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal Amount { get; set; }
}

public sealed class ServiceSurchargeDto
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public string Name { get; set; } = null!;
    public SurchargeTriggerType TriggerType { get; set; }
    public PricingAdjustmentType AdjustmentType { get; set; }
    public decimal Amount { get; set; }
    public bool IsActive { get; set; }
    public int? AfterHoursStartMinutes { get; set; }
    public int? AfterHoursEndMinutes { get; set; }
    public int? UrgentLeadTimeMinutes { get; set; }
    public string? LocationMatch { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class CreateServiceSurchargeRequest
{
    [Required]
    [StringLength(128, MinimumLength = 2)]
    public string Name { get; set; } = null!;

    [EnumDataType(typeof(SurchargeTriggerType))]
    public SurchargeTriggerType TriggerType { get; set; }

    [EnumDataType(typeof(PricingAdjustmentType))]
    public PricingAdjustmentType AdjustmentType { get; set; } = PricingAdjustmentType.FixedAmount;

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal Amount { get; set; }

    // Only used when TriggerType is AfterHours; otherwise ignored and cleared server-side.
    [Range(0, 1439)]
    public int? AfterHoursStartMinutes { get; set; }

    [Range(0, 1439)]
    public int? AfterHoursEndMinutes { get; set; }

    // Only used when TriggerType is Urgent; otherwise ignored and cleared server-side.
    [Range(1, int.MaxValue)]
    public int? UrgentLeadTimeMinutes { get; set; }

    // Only used when TriggerType is Location; otherwise ignored and cleared server-side.
    [StringLength(128)]
    public string? LocationMatch { get; set; }
}

public sealed class UpdateServiceSurchargeRequest
{
    [Required]
    [StringLength(128, MinimumLength = 2)]
    public string Name { get; set; } = null!;

    [EnumDataType(typeof(SurchargeTriggerType))]
    public SurchargeTriggerType TriggerType { get; set; }

    [EnumDataType(typeof(PricingAdjustmentType))]
    public PricingAdjustmentType AdjustmentType { get; set; } = PricingAdjustmentType.FixedAmount;

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal Amount { get; set; }

    [Range(0, 1439)]
    public int? AfterHoursStartMinutes { get; set; }

    [Range(0, 1439)]
    public int? AfterHoursEndMinutes { get; set; }

    [Range(1, int.MaxValue)]
    public int? UrgentLeadTimeMinutes { get; set; }

    [StringLength(128)]
    public string? LocationMatch { get; set; }
}

public sealed class ServicePricingVersionUnitDto
{
    public string UnitName { get; set; } = null!;
    public decimal UnitPrice { get; set; }
}

public sealed class ServicePricingVersionDto
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public PricingVersionStatus Status { get; set; }
    public ServicePricingMode PricingMode { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public decimal? BasePrice { get; set; }
    public TimePricingUnit? TimeBillingUnit { get; set; }
    public decimal? TimeUnitPrice { get; set; }
    public int? MinimumBillableDurationMinutes { get; set; }
    public int? BillingIncrementMinutes { get; set; }
    public TimeRoundingPolicy? TimeRoundingPolicy { get; set; }
    public int? OvertimeThresholdMinutes { get; set; }
    public decimal? OvertimeUnitPrice { get; set; }
    public IEnumerable<ServicePricingVersionUnitDto> Units { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

// Carries only the fields relevant to the service's current pricing mode; fields for other modes
// are ignored. EffectiveTo is intentionally not accepted here — a draft's end is always set
// automatically when the next version is published (see ServicePricingVersionService.PublishAsync).
public sealed class CreateServicePricingVersionRequest
{
    [Required]
    public DateTimeOffset EffectiveFrom { get; set; }

    // Fixed-mode snapshot.
    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal? BasePrice { get; set; }

    // Per-unit-mode snapshot.
    public IEnumerable<CreateServicePricingVersionUnitRequest> Units { get; set; } = [];

    // Time-based-mode snapshot, mirroring ServiceTimePricingPolicy.
    [EnumDataType(typeof(TimePricingUnit))]
    public TimePricingUnit? TimeBillingUnit { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal? TimeUnitPrice { get; set; }

    [Range(1, int.MaxValue)]
    public int? MinimumBillableDurationMinutes { get; set; }

    [Range(1, int.MaxValue)]
    public int? BillingIncrementMinutes { get; set; }

    [EnumDataType(typeof(TimeRoundingPolicy))]
    public TimeRoundingPolicy? TimeRoundingPolicy { get; set; }

    [Range(0, int.MaxValue)]
    public int? OvertimeThresholdMinutes { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal? OvertimeUnitPrice { get; set; }
}

public sealed class CreateServicePricingVersionUnitRequest
{
    [Required]
    [StringLength(128, MinimumLength = 2)]
    public string UnitName { get; set; } = null!;

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal UnitPrice { get; set; }
}

public sealed class PublicHolidayDto
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class CreatePublicHolidayRequest
{
    [Required]
    public DateOnly Date { get; set; }

    [Required]
    [StringLength(128, MinimumLength = 2)]
    public string Name { get; set; } = null!;
}
