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
    public IEnumerable<ServicePriceRuleDto> PriceRules { get; set; } = [];
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
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
    public DateTimeOffset CalculatedAt { get; set; }
}
