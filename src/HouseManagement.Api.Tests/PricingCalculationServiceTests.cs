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

    [Theory]
    [InlineData(100, 18, 18)]
    [InlineData(100, 0, 0)]
    [InlineData(0, 18, 0)]
    [InlineData(-50, 18, 0)]
    [InlineData(33.33, 15, 5)]
    public void CalculateTax_RoundsAndReturnsZeroForNonPositiveInputs(decimal taxableAmount, decimal taxRatePercentage, decimal expectedTax)
    {
        var tax = Calculator.CalculateTax(taxableAmount, taxRatePercentage);

        Assert.Equal(expectedTax, tax);
    }

    private static Service FixedService(decimal basePrice = 40m) =>
        new() { Id = 1, Code = "FIXED", Name = "Fixed", PricingMode = ServicePricingMode.Fixed, BasePrice = basePrice, IsActive = true };

    private static DateTimeOffset NextDayOfWeek(DateTimeOffset from, DayOfWeek dayOfWeek)
    {
        var candidate = from;
        while (candidate.DayOfWeek != dayOfWeek)
        {
            candidate = candidate.AddDays(1);
        }

        return candidate;
    }

    [Fact]
    public void Calculate_AppliesAnActiveFixedFeeOnTopOfTheBaseCharge()
    {
        var service = FixedService(40m);
        service.Fees.Add(new ServiceFee { Name = "Platform fee", AdjustmentType = PricingAdjustmentType.FixedAmount, Amount = 5m, IsActive = true });
        var start = DateTimeOffset.UtcNow.AddDays(1);

        var result = Calculator.Calculate(service, start, start.AddHours(1), null);

        Assert.True(result.Succeeded);
        Assert.Equal(45m, result.Subtotal);
        Assert.Equal(2, result.PriceLines.Count);
        Assert.Equal("Platform fee", result.PriceLines[1].Description);
        Assert.Equal(5m, result.PriceLines[1].LineTotal);
    }

    [Fact]
    public void Calculate_AppliesAPercentageFeeAgainstTheBaseSubtotal()
    {
        var service = FixedService(40m);
        service.Fees.Add(new ServiceFee { Name = "Service fee", AdjustmentType = PricingAdjustmentType.Percentage, Amount = 10m, IsActive = true });
        var start = DateTimeOffset.UtcNow.AddDays(1);

        var result = Calculator.Calculate(service, start, start.AddHours(1), null);

        Assert.True(result.Succeeded);
        Assert.Equal(44m, result.Subtotal);
        Assert.Equal(4m, Assert.Single(result.PriceLines, line => line.Description == "Service fee").LineTotal);
    }

    [Fact]
    public void Calculate_IgnoresAnInactiveFee()
    {
        var service = FixedService(40m);
        service.Fees.Add(new ServiceFee { Name = "Disabled fee", AdjustmentType = PricingAdjustmentType.FixedAmount, Amount = 5m, IsActive = false });
        var start = DateTimeOffset.UtcNow.AddDays(1);

        var result = Calculator.Calculate(service, start, start.AddHours(1), null);

        Assert.True(result.Succeeded);
        Assert.Equal(40m, result.Subtotal);
        Assert.Single(result.PriceLines);
    }

    [Fact]
    public void Calculate_AppliesWeekendSurchargeWhenScheduledOnAWeekend()
    {
        var service = FixedService(40m);
        service.Surcharges.Add(new ServiceSurcharge
        {
            Name = "Weekend surcharge",
            TriggerType = SurchargeTriggerType.Weekend,
            AdjustmentType = PricingAdjustmentType.FixedAmount,
            Amount = 10m,
            IsActive = true
        });
        var start = NextDayOfWeek(DateTimeOffset.UtcNow.AddDays(1), DayOfWeek.Saturday);

        var result = Calculator.Calculate(service, start, start.AddHours(1), null);

        Assert.True(result.Succeeded);
        Assert.Equal(50m, result.Subtotal);
    }

    [Fact]
    public void Calculate_DoesNotApplyWeekendSurchargeOnAWeekday()
    {
        var service = FixedService(40m);
        service.Surcharges.Add(new ServiceSurcharge
        {
            Name = "Weekend surcharge",
            TriggerType = SurchargeTriggerType.Weekend,
            AdjustmentType = PricingAdjustmentType.FixedAmount,
            Amount = 10m,
            IsActive = true
        });
        var start = NextDayOfWeek(DateTimeOffset.UtcNow.AddDays(1), DayOfWeek.Wednesday);

        var result = Calculator.Calculate(service, start, start.AddHours(1), null);

        Assert.True(result.Succeeded);
        Assert.Equal(40m, result.Subtotal);
    }

    [Fact]
    public void Calculate_AppliesHolidaySurchargeWhenScheduledDateIsAConfiguredHoliday()
    {
        var service = FixedService(40m);
        service.Surcharges.Add(new ServiceSurcharge
        {
            Name = "Holiday surcharge",
            TriggerType = SurchargeTriggerType.Holiday,
            AdjustmentType = PricingAdjustmentType.FixedAmount,
            Amount = 15m,
            IsActive = true
        });
        var start = DateTimeOffset.UtcNow.AddDays(3);
        var holidayDates = new List<DateOnly> { DateOnly.FromDateTime(start.Date) };

        var result = Calculator.Calculate(service, start, start.AddHours(1), null, holidayDates: holidayDates);

        Assert.True(result.Succeeded);
        Assert.Equal(55m, result.Subtotal);

        var notHolidayResult = Calculator.Calculate(service, start, start.AddHours(1), null, holidayDates: []);
        Assert.Equal(40m, notHolidayResult.Subtotal);
    }

    [Theory]
    [InlineData(20, true)]  // 20:00 falls within the 18:00-08:00 wraparound window
    [InlineData(10, false)] // 10:00 does not
    public void Calculate_AppliesAfterHoursSurchargeForAWraparoundWindow(int hourOfDay, bool expectTriggered)
    {
        var service = FixedService(40m);
        service.Surcharges.Add(new ServiceSurcharge
        {
            Name = "After-hours surcharge",
            TriggerType = SurchargeTriggerType.AfterHours,
            AdjustmentType = PricingAdjustmentType.FixedAmount,
            Amount = 8m,
            IsActive = true,
            AfterHoursStartMinutes = 18 * 60,
            AfterHoursEndMinutes = 8 * 60
        });
        var start = new DateTimeOffset(DateTimeOffset.UtcNow.AddDays(2).Date, TimeSpan.Zero).AddHours(hourOfDay);

        var result = Calculator.Calculate(service, start, start.AddHours(1), null);

        Assert.True(result.Succeeded);
        Assert.Equal(expectTriggered ? 48m : 40m, result.Subtotal);
    }

    [Fact]
    public void Calculate_AppliesUrgentSurchargeWhenLeadTimeIsBelowTheThreshold()
    {
        var service = FixedService(40m);
        service.Surcharges.Add(new ServiceSurcharge
        {
            Name = "Urgent surcharge",
            TriggerType = SurchargeTriggerType.Urgent,
            AdjustmentType = PricingAdjustmentType.FixedAmount,
            Amount = 12m,
            IsActive = true,
            UrgentLeadTimeMinutes = 180
        });

        var urgentStart = DateTimeOffset.UtcNow.AddMinutes(90);
        var urgentResult = Calculator.Calculate(service, urgentStart, urgentStart.AddHours(1), null);
        Assert.True(urgentResult.Succeeded);
        Assert.Equal(52m, urgentResult.Subtotal);

        var plannedStart = DateTimeOffset.UtcNow.AddDays(3);
        var plannedResult = Calculator.Calculate(service, plannedStart, plannedStart.AddHours(1), null);
        Assert.True(plannedResult.Succeeded);
        Assert.Equal(40m, plannedResult.Subtotal);
    }

    [Fact]
    public void Calculate_AppliesLocationSurchargeWhenTheAddressCityOrRegionMatches()
    {
        var service = FixedService(40m);
        service.Surcharges.Add(new ServiceSurcharge
        {
            Name = "Remote area surcharge",
            TriggerType = SurchargeTriggerType.Location,
            AdjustmentType = PricingAdjustmentType.FixedAmount,
            Amount = 20m,
            IsActive = true,
            LocationMatch = "Rural County"
        });
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var matchingAddress = new ServiceAddressRequest { Line1 = "1 Farm Rd", City = "Someville", Region = "Rural County", Country = "Testland" };
        var nonMatchingAddress = new ServiceAddressRequest { Line1 = "1 Main St", City = "Metro City", Region = "Metro Region", Country = "Testland" };

        var matchingResult = Calculator.Calculate(service, start, start.AddHours(1), null, matchingAddress);
        Assert.Equal(60m, matchingResult.Subtotal);

        var nonMatchingResult = Calculator.Calculate(service, start, start.AddHours(1), null, nonMatchingAddress);
        Assert.Equal(40m, nonMatchingResult.Subtotal);

        var noAddressResult = Calculator.Calculate(service, start, start.AddHours(1), null);
        Assert.Equal(40m, noAddressResult.Subtotal);
    }

    [Fact]
    public void Calculate_CombinesMultipleFeesAndTriggeredSurchargesWithoutCompoundingPercentages()
    {
        var service = FixedService(100m);
        service.Fees.Add(new ServiceFee { Name = "Platform fee", AdjustmentType = PricingAdjustmentType.FixedAmount, Amount = 5m, IsActive = true });
        service.Fees.Add(new ServiceFee { Name = "Service fee", AdjustmentType = PricingAdjustmentType.Percentage, Amount = 10m, IsActive = true });
        service.Surcharges.Add(new ServiceSurcharge
        {
            Name = "Weekend surcharge",
            TriggerType = SurchargeTriggerType.Weekend,
            AdjustmentType = PricingAdjustmentType.Percentage,
            Amount = 20m,
            IsActive = true
        });
        var start = NextDayOfWeek(DateTimeOffset.UtcNow.AddDays(1), DayOfWeek.Sunday);

        var result = Calculator.Calculate(service, start, start.AddHours(1), null);

        // Base 100 + fixed fee 5 + 10% of base (10) + 20% of base (20) = 135. Percentages are
        // computed from the 100 base subtotal, not from the running total, so they do not compound.
        Assert.True(result.Succeeded);
        Assert.Equal(135m, result.Subtotal);
        Assert.Equal(4, result.PriceLines.Count);
    }
}
