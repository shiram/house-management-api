using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Xunit;

namespace HouseManagement.Api.Tests;

public sealed class PricingCalculationServiceTests
{
    private static readonly PricingCalculationService Calculator = new();

    private static Service TimeBasedService(
        TimePricingUnit billingUnit,
        decimal unitPrice,
        int minimumMinutes,
        int incrementMinutes,
        TimeRoundingPolicy roundingPolicy,
        int? overtimeThresholdMinutes = null,
        decimal? overtimeUnitPrice = null)
    {
        return new Service
        {
            Id = 1,
            Code = "HOURLY",
            Name = "Hourly Cleaning",
            PricingMode = ServicePricingMode.TimeBased,
            IsActive = true,
            TimePricingPolicy = new ServiceTimePricingPolicy
            {
                BillingUnit = billingUnit,
                UnitPrice = unitPrice,
                MinimumBillableDurationMinutes = minimumMinutes,
                BillingIncrementMinutes = incrementMinutes,
                RoundingPolicy = roundingPolicy,
                OvertimeThresholdMinutes = overtimeThresholdMinutes,
                OvertimeUnitPrice = overtimeUnitPrice
            }
        };
    }

    [Fact]
    public void Calculate_TimeBased_AppliesMinimumBillableDurationForAShortRequest()
    {
        var service = TimeBasedService(TimePricingUnit.Hour, 30m, minimumMinutes: 120, incrementMinutes: 30, TimeRoundingPolicy.Up);
        var start = DateTimeOffset.UtcNow.AddDays(1);

        // Requested duration (30 minutes) is below the 2-hour minimum, so 2 hours are billed.
        var result = Calculator.Calculate(service, start, start.AddMinutes(30), null);

        Assert.True(result.Succeeded);
        Assert.Equal(60m, result.Subtotal);
        Assert.Equal(120, Assert.Single(result.PriceLines).Quantity);
    }

    [Theory]
    [InlineData(TimeRoundingPolicy.Up, 75, 90)]
    [InlineData(TimeRoundingPolicy.Down, 75, 60)]
    [InlineData(TimeRoundingPolicy.Nearest, 75, 90)]
    [InlineData(TimeRoundingPolicy.Nearest, 65, 60)]
    [InlineData(TimeRoundingPolicy.None, 75, 75)]
    public void Calculate_TimeBased_AppliesConfiguredRoundingPolicyToTheBillingIncrement(
        TimeRoundingPolicy policy, int requestedMinutes, int expectedBillableMinutes)
    {
        var service = TimeBasedService(TimePricingUnit.Minute, 1m, minimumMinutes: 1, incrementMinutes: 30, policy);
        var start = DateTimeOffset.UtcNow.AddDays(1);

        var result = Calculator.Calculate(service, start, start.AddMinutes(requestedMinutes), null);

        Assert.True(result.Succeeded);
        Assert.Equal(expectedBillableMinutes, Assert.Single(result.PriceLines).Quantity);
        Assert.Equal(expectedBillableMinutes * 1m, result.Subtotal);
    }

    [Fact]
    public void Calculate_TimeBased_SplitsRegularAndOvertimeAtTheConfiguredThreshold()
    {
        var service = TimeBasedService(
            TimePricingUnit.Hour,
            unitPrice: 30m,
            minimumMinutes: 60,
            incrementMinutes: 30,
            roundingPolicy: TimeRoundingPolicy.Up,
            overtimeThresholdMinutes: 480,
            overtimeUnitPrice: 45m);
        var start = DateTimeOffset.UtcNow.AddDays(1);

        // 9 hours (540 minutes) requested: 8 regular hours + 1 overtime hour.
        var result = Calculator.Calculate(service, start, start.AddMinutes(540), null);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.PriceLines.Count);
        var regular = result.PriceLines[0];
        var overtime = result.PriceLines[1];
        Assert.Equal(480, regular.Quantity);
        Assert.Equal(30m, regular.UnitPrice);
        Assert.Equal(240m, regular.LineTotal);
        Assert.Equal(60, overtime.Quantity);
        Assert.Equal(45m, overtime.UnitPrice);
        Assert.Equal(45m, overtime.LineTotal);
        Assert.Equal(285m, result.Subtotal);
    }

    [Fact]
    public void Calculate_TimeBased_RejectsPricingItems()
    {
        var service = TimeBasedService(TimePricingUnit.Hour, 30m, 60, 30, TimeRoundingPolicy.Up);
        var start = DateTimeOffset.UtcNow.AddDays(1);

        var result = Calculator.Calculate(service, start, start.AddHours(2), [new BookingPriceItemRequest { PriceRuleId = 1, Quantity = 1 }]);

        Assert.False(result.Succeeded);
        Assert.Equal("This service uses time-based pricing and does not accept pricing items.", result.Error);
    }

    [Fact]
    public void Calculate_TimeBased_RejectsMissingPolicy()
    {
        var service = new Service { Id = 1, Code = "HOURLY", Name = "Hourly", PricingMode = ServicePricingMode.TimeBased, IsActive = true };
        var start = DateTimeOffset.UtcNow.AddDays(1);

        var result = Calculator.Calculate(service, start, start.AddHours(1), null);

        Assert.False(result.Succeeded);
        Assert.Equal("Time-based pricing is not configured for this service.", result.Error);
    }

    [Fact]
    public void Calculate_TimeBased_RejectsNonPositiveDuration()
    {
        var service = TimeBasedService(TimePricingUnit.Hour, 30m, 60, 30, TimeRoundingPolicy.Up);
        var start = DateTimeOffset.UtcNow.AddDays(1);

        var result = Calculator.Calculate(service, start, start, null);

        Assert.False(result.Succeeded);
        Assert.Equal("The requested service time must be a future range.", result.Error);
    }

    [Fact]
    public void Calculate_RejectsAPastScheduledStartRegardlessOfPricingMode()
    {
        var service = new Service { Id = 1, Code = "FIXED", Name = "Fixed", PricingMode = ServicePricingMode.Fixed, BasePrice = 40m, IsActive = true };
        var start = DateTimeOffset.UtcNow.AddMinutes(-30);

        var result = Calculator.Calculate(service, start, start.AddHours(1), null);

        Assert.False(result.Succeeded);
        Assert.Equal("The requested service time must be a future range.", result.Error);
    }

    [Fact]
    public void Calculate_RejectsAnEndBeforeStartRegardlessOfPricingMode()
    {
        var service = new Service { Id = 1, Code = "UNIT", Name = "Per Unit", PricingMode = ServicePricingMode.PerUnit, IsActive = true };
        var start = DateTimeOffset.UtcNow.AddDays(1);

        var result = Calculator.Calculate(service, start, start.AddMinutes(-1), null);

        Assert.False(result.Succeeded);
        Assert.Equal("The requested service time must be a future range.", result.Error);
    }

    [Fact]
    public void Calculate_Fixed_RejectsPricingItems()
    {
        var service = new Service { Id = 1, Code = "FIXED", Name = "Fixed", PricingMode = ServicePricingMode.Fixed, BasePrice = 40m, IsActive = true };
        var start = DateTimeOffset.UtcNow.AddDays(1);

        var result = Calculator.Calculate(service, start, start.AddHours(1), [new BookingPriceItemRequest { PriceRuleId = 1, Quantity = 1 }]);

        Assert.False(result.Succeeded);
        Assert.Equal("This service uses fixed pricing and does not accept pricing items.", result.Error);
    }
}
