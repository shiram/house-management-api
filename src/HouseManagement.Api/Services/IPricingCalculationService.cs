using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IPricingCalculationService
{
    // The single, deterministic pricing formula shared by booking creation and the public
    // pre-booking quote endpoint so the two never diverge. Callers supply an already-loaded,
    // active Service (with PriceRules, TimePricingPolicy, Fees, and Surcharges included as
    // relevant). `address` and `holidayDates` are optional context needed only to evaluate the
    // Location and Holiday surcharge triggers respectively; omit them when not applicable.
    PricingCalculationResult Calculate(
        Service service,
        DateTimeOffset scheduledStart,
        DateTimeOffset scheduledEnd,
        IEnumerable<BookingPriceItemRequest>? pricingItems,
        ServiceAddressRequest? address = null,
        IReadOnlyCollection<DateOnly>? holidayDates = null);

    // A small, deterministic, DB-free tax calculation shared by booking creation and the quote
    // endpoint. `taxableAmount` is the amount tax applies to (subtotal minus any discount, for
    // booking creation; the raw subtotal for a quote, which has no discount context).
    // `taxRatePercentage` is the effective rate to apply (0 when the service is exempt or no rate
    // is configured); it is not clamped again here so callers can persist the exact rate used.
    decimal CalculateTax(decimal taxableAmount, decimal taxRatePercentage);
}

public sealed record PricingCalculationResult(List<BookingPriceLine> PriceLines, decimal Subtotal, string? Error)
{
    public bool Succeeded => Error == null;
}
