using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IPricingCalculationService
{
    // The single, deterministic pricing formula shared by booking creation and the public
    // pre-booking quote endpoint so the two never diverge. Callers supply an already-loaded,
    // active Service (with PriceRules and TimePricingPolicy included as relevant).
    PricingCalculationResult Calculate(
        Service service,
        DateTimeOffset scheduledStart,
        DateTimeOffset scheduledEnd,
        IEnumerable<BookingPriceItemRequest>? pricingItems);
}

public sealed record PricingCalculationResult(List<BookingPriceLine> PriceLines, decimal Subtotal, string? Error)
{
    public bool Succeeded => Error == null;
}
