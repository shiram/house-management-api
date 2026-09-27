using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace HouseManagement.Api.Tests;

public sealed class ServicePricingVersionTests
{
    private static HouseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HouseContext(options);
    }

    [Fact]
    public async Task CreateDraftAsync_FixedService_RequiresPositiveBasePrice()
    {
        await using var context = CreateContext();
        context.Services.Add(new Service { Id = 1, Code = "FIXED", Name = "Fixed", PricingMode = ServicePricingMode.Fixed, BasePrice = 100m });
        await context.SaveChangesAsync();

        var service = new ServicePricingVersionService(context);

        var invalid = await service.CreateDraftAsync(1, new ServicePricingVersion
        {
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(1),
            BasePrice = 0m
        });
        Assert.Null(invalid);

        var created = await service.CreateDraftAsync(1, new ServicePricingVersion
        {
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(1),
            BasePrice = 120m
        });

        Assert.NotNull(created);
        Assert.Equal(PricingVersionStatus.Draft, created!.Status);
        Assert.Equal(ServicePricingMode.Fixed, created.PricingMode);
        Assert.Equal(120m, created.BasePrice);
    }

    [Fact]
    public async Task CreateDraftAsync_PerUnitService_RejectsDuplicateOrInvalidUnits()
    {
        await using var context = CreateContext();
        context.Services.Add(new Service { Id = 1, Code = "LAUNDRY", Name = "Laundry", PricingMode = ServicePricingMode.PerUnit });
        await context.SaveChangesAsync();

        var service = new ServicePricingVersionService(context);

        var duplicate = await service.CreateDraftAsync(1, new ServicePricingVersion
        {
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(1),
            Units = new List<ServicePricingVersionUnit>
            {
                new() { UnitName = "Kg", UnitPrice = 5m },
                new() { UnitName = "kg", UnitPrice = 6m }
            }
        });
        Assert.Null(duplicate);

        var empty = await service.CreateDraftAsync(1, new ServicePricingVersion
        {
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(1),
            Units = new List<ServicePricingVersionUnit>()
        });
        Assert.Null(empty);

        var created = await service.CreateDraftAsync(1, new ServicePricingVersion
        {
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(1),
            Units = new List<ServicePricingVersionUnit> { new() { UnitName = " Kg ", UnitPrice = 5m } }
        });

        Assert.NotNull(created);
        var unit = Assert.Single(created!.Units);
        Assert.Equal("Kg", unit.UnitName);
    }

    [Fact]
    public async Task CreateDraftAsync_TimeBasedService_RequiresPairedOvertimeAndMinimumOrdering()
    {
        await using var context = CreateContext();
        context.Services.Add(new Service { Id = 1, Code = "HOURLY", Name = "Hourly", PricingMode = ServicePricingMode.TimeBased });
        await context.SaveChangesAsync();

        var service = new ServicePricingVersionService(context);

        var unpaired = await service.CreateDraftAsync(1, new ServicePricingVersion
        {
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(1),
            TimeBillingUnit = TimePricingUnit.Hour,
            TimeUnitPrice = 30m,
            MinimumBillableDurationMinutes = 60,
            BillingIncrementMinutes = 30,
            TimeRoundingPolicy = TimeRoundingPolicy.Up,
            OvertimeThresholdMinutes = 120
        });
        Assert.Null(unpaired);

        var thresholdBelowMinimum = await service.CreateDraftAsync(1, new ServicePricingVersion
        {
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(1),
            TimeBillingUnit = TimePricingUnit.Hour,
            TimeUnitPrice = 30m,
            MinimumBillableDurationMinutes = 120,
            BillingIncrementMinutes = 30,
            TimeRoundingPolicy = TimeRoundingPolicy.Up,
            OvertimeThresholdMinutes = 60,
            OvertimeUnitPrice = 45m
        });
        Assert.Null(thresholdBelowMinimum);

        var created = await service.CreateDraftAsync(1, new ServicePricingVersion
        {
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(1),
            TimeBillingUnit = TimePricingUnit.Hour,
            TimeUnitPrice = 30m,
            MinimumBillableDurationMinutes = 120,
            BillingIncrementMinutes = 30,
            TimeRoundingPolicy = TimeRoundingPolicy.Up,
            OvertimeThresholdMinutes = 480,
            OvertimeUnitPrice = 45m
        });

        Assert.NotNull(created);
        Assert.Equal(TimePricingUnit.Hour, created!.TimeBillingUnit);
        Assert.Equal(480, created.OvertimeThresholdMinutes);
    }

    [Fact]
    public async Task CreateDraftAsync_UnknownServiceOrMissingEffectiveFrom_ReturnsNull()
    {
        await using var context = CreateContext();
        var service = new ServicePricingVersionService(context);

        Assert.Null(await service.CreateDraftAsync(999, new ServicePricingVersion { EffectiveFrom = DateTimeOffset.UtcNow.AddDays(1), BasePrice = 10m }));

        context.Services.Add(new Service { Id = 1, Code = "FIXED", Name = "Fixed", PricingMode = ServicePricingMode.Fixed });
        await context.SaveChangesAsync();

        Assert.Null(await service.CreateDraftAsync(1, new ServicePricingVersion { BasePrice = 10m }));
    }

    [Fact]
    public async Task PublishAsync_ClosesPriorOpenVersionAndRejectsOverlap()
    {
        await using var context = CreateContext();
        context.Services.Add(new Service { Id = 1, Code = "FIXED", Name = "Fixed", PricingMode = ServicePricingMode.Fixed });
        await context.SaveChangesAsync();

        var service = new ServicePricingVersionService(context);

        var first = await service.CreateDraftAsync(1, new ServicePricingVersion
        {
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30),
            BasePrice = 100m
        });
        var firstPublish = await service.PublishAsync(first!.Id);
        Assert.True(firstPublish.Succeeded);

        var second = await service.CreateDraftAsync(1, new ServicePricingVersion
        {
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(30),
            BasePrice = 150m
        });
        var secondPublish = await service.PublishAsync(second!.Id);
        Assert.True(secondPublish.Succeeded);

        var reloadedFirst = await context.ServicePricingVersions.AsNoTracking().SingleAsync(v => v.Id == first.Id);
        Assert.Equal(second.EffectiveFrom, reloadedFirst.EffectiveTo);

        // A third version effective before the now-open second version must be rejected.
        var overlapping = await service.CreateDraftAsync(1, new ServicePricingVersion
        {
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(10),
            BasePrice = 200m
        });
        var overlapResult = await service.PublishAsync(overlapping!.Id);
        Assert.False(overlapResult.Succeeded);
        Assert.NotNull(overlapResult.Error);
    }

    [Fact]
    public async Task GetEffectiveVersionAsync_ResolvesVersionCoveringRequestedInstant()
    {
        await using var context = CreateContext();
        context.Services.Add(new Service { Id = 1, Code = "FIXED", Name = "Fixed", PricingMode = ServicePricingMode.Fixed });
        await context.SaveChangesAsync();

        var service = new ServicePricingVersionService(context);

        var past = await service.CreateDraftAsync(1, new ServicePricingVersion { EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-60), BasePrice = 80m });
        await service.PublishAsync(past!.Id);

        var future = await service.CreateDraftAsync(1, new ServicePricingVersion { EffectiveFrom = DateTimeOffset.UtcNow.AddDays(10), BasePrice = 95m });
        await service.PublishAsync(future!.Id);

        var effectiveNow = await service.GetEffectiveVersionAsync(1, DateTimeOffset.UtcNow);
        Assert.NotNull(effectiveNow);
        Assert.Equal(80m, effectiveNow!.BasePrice);

        var effectiveInFuture = await service.GetEffectiveVersionAsync(1, DateTimeOffset.UtcNow.AddDays(20));
        Assert.NotNull(effectiveInFuture);
        Assert.Equal(95m, effectiveInFuture!.BasePrice);

        var beforeAnyVersion = await service.GetEffectiveVersionAsync(1, DateTimeOffset.UtcNow.AddDays(-90));
        Assert.Null(beforeAnyVersion);
    }

    [Fact]
    public void PricingVersionModel_DefinesStorageAndIntegrityConstraints()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PricingVersionModelTest;Trusted_Connection=True;")
            .Options;

        using var context = new HouseContext(options);
        var entity = context.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(ServicePricingVersion));

        Assert.NotNull(entity);
        Assert.Equal(18, entity!.FindProperty(nameof(ServicePricingVersion.BasePrice))!.GetPrecision());
        Assert.Equal(2, entity.FindProperty(nameof(ServicePricingVersion.BasePrice))!.GetScale());

        Assert.True(entity.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(ServicePricingVersion.ServiceId)])).IsUnique);

        var constraints = entity.GetCheckConstraints().Select(constraint => constraint.Name).ToHashSet();
        Assert.Contains("CK_ServicePricingVersions_EffectiveWindow", constraints);
        Assert.Contains("CK_ServicePricingVersions_Overtime", constraints);
        Assert.Contains("CK_ServicePricingVersions_Status", constraints);
        Assert.Contains("CK_ServicePricingVersions_PricingMode", constraints);
    }

    [Fact]
    public void PricingVersionStatusEnum_HasStableValues()
    {
        Assert.Equal(0, (int)PricingVersionStatus.Draft);
        Assert.Equal(1, (int)PricingVersionStatus.Published);
    }
}
