using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HouseManagement.Api.Tests;

public class ServiceCatalogTests
{
    [Fact]
    public async Task GetActiveAsync_ReturnsOnlyActiveServicesOrderedByName()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Services.AddRange(
            new Service { Code = "LAUNDRY", Name = "Laundry", IsActive = true },
            new Service { Code = "CLEANING", Name = "Cleaning", IsActive = true },
            new Service { Code = "OLD", Name = "Old Service", IsActive = false });
        await context.SaveChangesAsync();

        var service = new ServiceCatalogService(context);
        var results = (await service.GetActiveAsync()).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal("Cleaning", results[0].Name);
        Assert.Equal("Laundry", results[1].Name);
        Assert.DoesNotContain(results, item => item.Code == "OLD");
    }

    [Fact]
    public async Task GetActiveByIdAsync_DoesNotReturnInactiveService()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Services.AddRange(
            new Service { Id = 1, Code = "ACTIVE", Name = "Active", IsActive = true },
            new Service { Id = 2, Code = "INACTIVE", Name = "Inactive", IsActive = false });
        await context.SaveChangesAsync();

        var service = new ServiceCatalogService(context);

        Assert.NotNull(await service.GetActiveByIdAsync(1));
        Assert.Null(await service.GetActiveByIdAsync(2));
        Assert.Null(await service.GetActiveByIdAsync(999));
    }

    [Fact]
    public async Task CreateAsync_NormalizesValuesAndRejectsDuplicateCode()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        var service = new ServiceCatalogService(context);

        var created = await service.CreateAsync(new Service
        {
            Code = "  CLEANING  ",
            Name = "  House Cleaning  ",
            Description = "  Standard service  ",
            BasePrice = 30
        });
        var duplicate = await service.CreateAsync(new Service
        {
            Code = "CLEANING",
            Name = "Another Name",
            BasePrice = 40
        });

        Assert.NotNull(created);
        Assert.Equal("CLEANING", created.Code);
        Assert.Equal("House Cleaning", created.Name);
        Assert.Equal("Standard service", created.Description);
        Assert.Null(duplicate);
    }

    [Fact]
    public async Task UpdateAsync_NormalizesValuesAndUpdatesTimestamp()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        var originalTimestamp = DateTimeOffset.UtcNow.AddMinutes(-1);
        context.Services.Add(new Service
        {
            Id = 1,
            Code = "OLD",
            Name = "Old Name",
            BasePrice = 10,
            IsActive = false,
            CreatedAt = originalTimestamp
        });
        await context.SaveChangesAsync();

        var service = new ServiceCatalogService(context);
        var updated = await service.UpdateAsync(new Service
        {
            Id = 1,
            Code = "  NEW  ",
            Name = "  New Name  ",
            BasePrice = 20,
            IsActive = true
        });

        var stored = await context.Services.SingleAsync(item => item.Id == 1);
        Assert.True(updated);
        Assert.Equal("NEW", stored.Code);
        Assert.Equal("New Name", stored.Name);
        Assert.Equal(20, stored.BasePrice);
        Assert.False(stored.IsActive);
        Assert.True(stored.UpdatedAt > originalTimestamp);
    }

    [Fact]
    public async Task SetActiveAsync_ChangesStatusAndTimestamp()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        var originalTimestamp = DateTimeOffset.UtcNow.AddMinutes(-1);
        context.Services.Add(new Service
        {
            Id = 1,
            Code = "CLEANING",
            Name = "Cleaning",
            IsActive = true,
            CreatedAt = originalTimestamp
        });
        await context.SaveChangesAsync();

        var service = new ServiceCatalogService(context);

        Assert.True(await service.SetActiveAsync(1, false));
        Assert.False(await context.Services.Where(item => item.Id == 1).Select(item => item.IsActive).SingleAsync());
        Assert.True((await context.Services.SingleAsync(item => item.Id == 1)).UpdatedAt > originalTimestamp);
        Assert.False(await service.SetActiveAsync(999, true));
    }

    [Fact]
    public async Task CreatePriceRuleAsync_RejectsDuplicateUnitNames()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Services.Add(new Service
        {
            Id = 1,
            Code = "LAUNDRY",
            Name = "Laundry",
            IsActive = true,
            PricingMode = ServicePricingMode.PerUnit,
            BasePrice = 0m
        });
        await context.SaveChangesAsync();

        var service = new ServiceCatalogService(context);
        var first = await service.CreatePriceRuleAsync(1, new ServicePriceRule { UnitName = "Load", UnitPrice = 12.5m, IsActive = true });
        var duplicate = await service.CreatePriceRuleAsync(1, new ServicePriceRule { UnitName = " load ", UnitPrice = 15m, IsActive = true });

        Assert.NotNull(first);
        Assert.Null(duplicate);
        Assert.Equal(1, await context.ServicePriceRules.CountAsync());
    }

    [Fact]
    public async Task UpdatePriceRuleAsync_RejectsDuplicateUnitNamesWithinSameService()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Services.Add(new Service
        {
            Id = 1,
            Code = "CLEANING",
            Name = "Cleaning",
            IsActive = true,
            PricingMode = ServicePricingMode.PerUnit,
            BasePrice = 0m,
            PriceRules =
            [
                new ServicePriceRule { Id = 1, UnitName = "Room", UnitPrice = 18m, IsActive = true },
                new ServicePriceRule { Id = 2, UnitName = "Hour", UnitPrice = 25m, IsActive = true }
            ]
        });
        await context.SaveChangesAsync();

        var service = new ServiceCatalogService(context);
        var result = await service.UpdatePriceRuleAsync(1, 1, new ServicePriceRule { UnitName = "hour", UnitPrice = 30m });

        Assert.True(result.Exists);
        Assert.True(result.HasDuplicateUnitName);
        Assert.Equal("Room", (await context.ServicePriceRules.SingleAsync(item => item.Id == 1)).UnitName);
    }

    [Fact]
    public async Task UpsertTimePricingPolicyAsync_CreatesThenUpdatesAndRejectsInvalidOvertime()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Services.Add(new Service { Id = 1, Code = "CLEAN", Name = "Cleaning", PricingMode = ServicePricingMode.TimeBased });
        await context.SaveChangesAsync();

        var service = new ServiceCatalogService(context);

        var missingService = await service.UpsertTimePricingPolicyAsync(999, new ServiceTimePricingPolicy
        {
            BillingUnit = TimePricingUnit.Hour,
            UnitPrice = 20m,
            MinimumBillableDurationMinutes = 60,
            BillingIncrementMinutes = 15
        });
        Assert.False(missingService.ServiceExists);

        var invalidOvertime = await service.UpsertTimePricingPolicyAsync(1, new ServiceTimePricingPolicy
        {
            BillingUnit = TimePricingUnit.Hour,
            UnitPrice = 20m,
            MinimumBillableDurationMinutes = 60,
            BillingIncrementMinutes = 15,
            OvertimeThresholdMinutes = 30 // below minimum duration, must be rejected
        });
        Assert.True(invalidOvertime.ServiceExists);
        Assert.False(invalidOvertime.IsValid);

        var created = await service.UpsertTimePricingPolicyAsync(1, new ServiceTimePricingPolicy
        {
            BillingUnit = TimePricingUnit.Hour,
            UnitPrice = 20m,
            MinimumBillableDurationMinutes = 60,
            BillingIncrementMinutes = 15,
            RoundingPolicy = TimeRoundingPolicy.Nearest
        });
        Assert.True(created.IsValid);
        Assert.Equal(20m, created.Policy!.UnitPrice);

        var updated = await service.UpsertTimePricingPolicyAsync(1, new ServiceTimePricingPolicy
        {
            BillingUnit = TimePricingUnit.Hour,
            UnitPrice = 25m,
            MinimumBillableDurationMinutes = 60,
            BillingIncrementMinutes = 15,
            RoundingPolicy = TimeRoundingPolicy.Nearest
        });
        Assert.True(updated.IsValid);
        Assert.Equal(25m, updated.Policy!.UnitPrice);
        Assert.Equal(1, await context.ServiceTimePricingPolicies.CountAsync());
    }

    [Fact]
    public async Task CreateFeeAsync_RejectsPercentageAboveOneHundredAndNonPositiveFixedAmount()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Services.Add(new Service { Id = 1, Code = "CLEAN", Name = "Cleaning" });
        await context.SaveChangesAsync();

        var service = new ServiceCatalogService(context);

        var missingService = await service.CreateFeeAsync(999, new ServiceFee { Name = "Platform Fee", AdjustmentType = PricingAdjustmentType.FixedAmount, Amount = 5m });
        Assert.False(missingService.ServiceExists);

        var invalidPercentage = await service.CreateFeeAsync(1, new ServiceFee { Name = "Service Fee", AdjustmentType = PricingAdjustmentType.Percentage, Amount = 150m });
        Assert.True(invalidPercentage.ServiceExists);
        Assert.False(invalidPercentage.IsValid);

        var valid = await service.CreateFeeAsync(1, new ServiceFee { Name = "Service Fee", AdjustmentType = PricingAdjustmentType.Percentage, Amount = 5m });
        Assert.True(valid.IsValid);
        Assert.NotNull(valid.Fee);
        Assert.True(valid.Fee!.IsActive);
    }

    [Fact]
    public async Task UpdateSurchargeAsync_RejectsTriggerFieldsThatDoNotMatchTheTriggerType()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Services.Add(new Service { Id = 1, Code = "CLEAN", Name = "Cleaning" });
        context.ServiceSurcharges.Add(new ServiceSurcharge
        {
            Id = 1,
            ServiceId = 1,
            Name = "Weekend",
            TriggerType = SurchargeTriggerType.Weekend,
            AdjustmentType = PricingAdjustmentType.Percentage,
            Amount = 10m,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new ServiceCatalogService(context);

        // Location field set on a Weekend trigger must be rejected (mirrors CK_ServiceSurcharges_TriggerFields).
        var invalid = await service.UpdateSurchargeAsync(1, 1, new ServiceSurcharge
        {
            Name = "Weekend",
            TriggerType = SurchargeTriggerType.Weekend,
            AdjustmentType = PricingAdjustmentType.Percentage,
            Amount = 10m,
            LocationMatch = "Nairobi"
        });
        Assert.True(invalid.Exists);
        Assert.False(invalid.IsValid);

        var valid = await service.UpdateSurchargeAsync(1, 1, new ServiceSurcharge
        {
            Name = "Weekend Premium",
            TriggerType = SurchargeTriggerType.Location,
            AdjustmentType = PricingAdjustmentType.Percentage,
            Amount = 15m,
            LocationMatch = "Nairobi"
        });
        Assert.True(valid.IsValid);

        var stored = await context.ServiceSurcharges.SingleAsync(item => item.Id == 1);
        Assert.Equal(SurchargeTriggerType.Location, stored.TriggerType);
        Assert.Equal("Nairobi", stored.LocationMatch);
    }

    [Fact]
    public async Task CreatePublicHolidayAsync_RejectsDuplicateDate()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        var service = new ServiceCatalogService(context);
        var date = new DateOnly(2026, 12, 25);

        var created = await service.CreatePublicHolidayAsync(new PublicHoliday { Date = date, Name = "Christmas Day" });
        var duplicate = await service.CreatePublicHolidayAsync(new PublicHoliday { Date = date, Name = "Christmas (again)" });

        Assert.NotNull(created);
        Assert.Null(duplicate);
        Assert.Equal(1, await context.PublicHolidays.CountAsync());

        Assert.True(await service.DeletePublicHolidayAsync(created!.Id));
        Assert.False(await service.DeletePublicHolidayAsync(created.Id));
    }
}
