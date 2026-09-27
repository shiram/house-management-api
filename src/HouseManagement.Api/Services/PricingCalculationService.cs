using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public sealed class PricingCalculationService : IPricingCalculationService
{
    // Matches the maximum value BookingPriceLine/Booking money columns can persist (decimal(18,2)).
    private const decimal MaximumPersistablePrice = 9999999999999999.99m;

    public PricingCalculationResult Calculate(
        Service service,
        DateTimeOffset scheduledStart,
        DateTimeOffset scheduledEnd,
        IEnumerable<BookingPriceItemRequest>? pricingItems)
    {
        // The requested schedule is validated once, here, for every pricing mode. Fixed and
        // per-unit services do not use the duration to calculate price, but the schedule still
        // represents the real booking window, so every caller (booking creation, repeat, and the
        // public quote endpoint) must reject the same invalid or past windows before pricing.
        if (scheduledStart >= scheduledEnd || scheduledStart <= DateTimeOffset.UtcNow)
        {
            return new PricingCalculationResult([], 0, "The requested service time must be a future range.");
        }

        var items = pricingItems?.ToList() ?? [];

        return service.PricingMode switch
        {
            ServicePricingMode.Fixed => CalculateFixed(service, items),
            ServicePricingMode.PerUnit => CalculatePerUnit(service, items),
            ServicePricingMode.TimeBased => CalculateTimeBased(service, scheduledStart, scheduledEnd, items),
            _ => new PricingCalculationResult([], 0, "The service pricing mode is not supported.")
        };
    }

    private static PricingCalculationResult CalculateFixed(Service service, List<BookingPriceItemRequest> items)
    {
        if (items.Count > 0)
        {
            return new PricingCalculationResult([], 0, "This service uses fixed pricing and does not accept pricing items.");
        }

        return new PricingCalculationResult(
            [new BookingPriceLine
            {
                Description = service.Name,
                Quantity = 1,
                UnitPrice = service.BasePrice,
                LineTotal = service.BasePrice
            }],
            service.BasePrice,
            null);
    }

    private static PricingCalculationResult CalculatePerUnit(Service service, List<BookingPriceItemRequest> items)
    {
        if (items.Count == 0)
        {
            return new PricingCalculationResult([], 0, "At least one pricing item is required for this service.");
        }

        if (items.GroupBy(item => item.PriceRuleId).Any(group => group.Count() > 1))
        {
            return new PricingCalculationResult([], 0, "Each pricing item can only be selected once.");
        }

        var rulesById = service.PriceRules
            .Where(rule => rule.IsActive)
            .ToDictionary(rule => rule.Id);
        var lines = new List<BookingPriceLine>();
        decimal totalPrice = 0;

        foreach (var item in items)
        {
            if (item.Quantity is < 1 or > 100000)
            {
                return new PricingCalculationResult([], 0, "Each pricing item quantity must be between 1 and 100000.");
            }

            if (!rulesById.TryGetValue(item.PriceRuleId, out var rule))
            {
                return new PricingCalculationResult([], 0, "One or more selected pricing items are not available for this service.");
            }

            if (rule.UnitPrice > MaximumPersistablePrice / item.Quantity)
            {
                return new PricingCalculationResult([], 0, "The calculated booking price is too large.");
            }

            var lineTotal = rule.UnitPrice * item.Quantity;
            if (lineTotal > MaximumPersistablePrice - totalPrice)
            {
                return new PricingCalculationResult([], 0, "The calculated booking price is too large.");
            }

            lines.Add(new BookingPriceLine
            {
                Description = rule.UnitName,
                Quantity = item.Quantity,
                UnitPrice = rule.UnitPrice,
                LineTotal = lineTotal
            });
            totalPrice += lineTotal;
        }

        return new PricingCalculationResult(lines, totalPrice, null);
    }

    private static PricingCalculationResult CalculateTimeBased(
        Service service,
        DateTimeOffset scheduledStart,
        DateTimeOffset scheduledEnd,
        List<BookingPriceItemRequest> items)
    {
        var policy = service.TimePricingPolicy;
        if (policy == null)
        {
            return new PricingCalculationResult([], 0, "Time-based pricing is not configured for this service.");
        }

        if (items.Count > 0)
        {
            return new PricingCalculationResult([], 0, "This service uses time-based pricing and does not accept pricing items.");
        }

        // Round up to the nearest whole minute so a partial minute is never charged less than a full one.
        var requestedMinutes = (int)Math.Ceiling((scheduledEnd - scheduledStart).TotalMinutes);
        var billableMinutes = Math.Max(requestedMinutes, policy.MinimumBillableDurationMinutes);
        billableMinutes = ApplyRoundingPolicy(billableMinutes, policy.BillingIncrementMinutes, policy.RoundingPolicy);
        if (billableMinutes < policy.MinimumBillableDurationMinutes)
        {
            billableMinutes = policy.MinimumBillableDurationMinutes;
        }

        var overtimeThreshold = policy.OvertimeThresholdMinutes;
        var regularMinutes = overtimeThreshold.HasValue ? Math.Min(billableMinutes, overtimeThreshold.Value) : billableMinutes;
        var overtimeMinutes = overtimeThreshold.HasValue ? Math.Max(0, billableMinutes - overtimeThreshold.Value) : 0;

        var unitLengthMinutes = policy.BillingUnit switch
        {
            TimePricingUnit.Minute => 1m,
            TimePricingUnit.Hour => 60m,
            TimePricingUnit.Day => 1440m,
            _ => 1m
        };

        var lines = new List<BookingPriceLine>();
        decimal totalPrice = 0;

        if (regularMinutes > 0)
        {
            var regularTotal = Math.Round((regularMinutes / unitLengthMinutes) * policy.UnitPrice, 2, MidpointRounding.AwayFromZero);
            if (regularTotal > MaximumPersistablePrice)
            {
                return new PricingCalculationResult([], 0, "The calculated booking price is too large.");
            }

            lines.Add(new BookingPriceLine
            {
                Description = $"{service.Name} ({FormatUnitLabel(policy.BillingUnit)})",
                Quantity = regularMinutes,
                UnitPrice = policy.UnitPrice,
                LineTotal = regularTotal
            });
            totalPrice += regularTotal;
        }

        if (overtimeMinutes > 0)
        {
            var overtimeTotal = Math.Round((overtimeMinutes / unitLengthMinutes) * policy.OvertimeUnitPrice!.Value, 2, MidpointRounding.AwayFromZero);
            if (overtimeTotal > MaximumPersistablePrice - totalPrice)
            {
                return new PricingCalculationResult([], 0, "The calculated booking price is too large.");
            }

            lines.Add(new BookingPriceLine
            {
                Description = $"{service.Name} overtime ({FormatUnitLabel(policy.BillingUnit)})",
                Quantity = overtimeMinutes,
                UnitPrice = policy.OvertimeUnitPrice.Value,
                LineTotal = overtimeTotal
            });
            totalPrice += overtimeTotal;
        }

        return new PricingCalculationResult(lines, totalPrice, null);
    }

    // Snaps billable minutes to the configured billing increment. "Up"/"Down" always move toward
    // the respective boundary; "Nearest" rounds to the closer boundary, favoring the upper boundary
    // on an exact tie so a booking is never undercharged.
    private static int ApplyRoundingPolicy(int minutes, int incrementMinutes, TimeRoundingPolicy policy)
    {
        if (policy == TimeRoundingPolicy.None)
        {
            return minutes;
        }

        var remainder = minutes % incrementMinutes;
        if (remainder == 0)
        {
            return minutes;
        }

        return policy switch
        {
            TimeRoundingPolicy.Up => minutes + (incrementMinutes - remainder),
            TimeRoundingPolicy.Down => minutes - remainder,
            TimeRoundingPolicy.Nearest => remainder * 2 >= incrementMinutes
                ? minutes + (incrementMinutes - remainder)
                : minutes - remainder,
            _ => minutes
        };
    }

    private static string FormatUnitLabel(TimePricingUnit unit) => unit switch
    {
        TimePricingUnit.Minute => "per minute",
        TimePricingUnit.Hour => "per hour",
        TimePricingUnit.Day => "per day",
        _ => string.Empty
    };
}
