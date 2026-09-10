using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace HouseManagement.Api.Tests;

public sealed class ServiceTimePricingPolicyTests
{
    [Fact]
    public async Task TimePricingPolicy_PersistsCompleteBillingPolicy()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Services.Add(new Service
        {
            Code = "HOURLY_CLEANING",
            Name = "Hourly Cleaning",
            PricingMode = ServicePricingMode.TimeBased,
            TimePricingPolicy = new ServiceTimePricingPolicy
            {
                BillingUnit = TimePricingUnit.Hour,
                UnitPrice = 30m,
                MinimumBillableDurationMinutes = 120,
                BillingIncrementMinutes = 30,
                RoundingPolicy = TimeRoundingPolicy.Up,
                OvertimeThresholdMinutes = 480,
                OvertimeUnitPrice = 45m
            }
        });

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = await context.Services
            .Include(item => item.TimePricingPolicy)
            .SingleAsync();
        var policy = Assert.IsType<ServiceTimePricingPolicy>(service.TimePricingPolicy);

        Assert.Equal(TimePricingUnit.Hour, policy.BillingUnit);
        Assert.Equal(30m, policy.UnitPrice);
        Assert.Equal(120, policy.MinimumBillableDurationMinutes);
        Assert.Equal(30, policy.BillingIncrementMinutes);
        Assert.Equal(TimeRoundingPolicy.Up, policy.RoundingPolicy);
        Assert.Equal(480, policy.OvertimeThresholdMinutes);
        Assert.Equal(45m, policy.OvertimeUnitPrice);
    }

    [Fact]
    public void TimePricingPolicy_ModelDefinesStorageAndIntegrityConstraints()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PricingModelTest;Trusted_Connection=True;")
            .Options;

        using var context = new HouseContext(options);
        var entity = context.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(ServiceTimePricingPolicy));

        Assert.NotNull(entity);
        Assert.True(entity!.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(ServiceTimePricingPolicy.ServiceId)])).IsUnique);
        Assert.Equal(18, entity.FindProperty(nameof(ServiceTimePricingPolicy.UnitPrice))!.GetPrecision());
        Assert.Equal(2, entity.FindProperty(nameof(ServiceTimePricingPolicy.UnitPrice))!.GetScale());
        Assert.Equal(18, entity.FindProperty(nameof(ServiceTimePricingPolicy.OvertimeUnitPrice))!.GetPrecision());
        Assert.Equal(2, entity.FindProperty(nameof(ServiceTimePricingPolicy.OvertimeUnitPrice))!.GetScale());

        var constraints = entity.GetCheckConstraints().Select(constraint => constraint.Name).ToHashSet();
        Assert.Contains("CK_ServiceTimePricingPolicies_UnitPrice", constraints);
        Assert.Contains("CK_ServiceTimePricingPolicies_BillingUnit", constraints);
        Assert.Contains("CK_ServiceTimePricingPolicies_MinimumDuration", constraints);
        Assert.Contains("CK_ServiceTimePricingPolicies_BillingIncrement", constraints);
        Assert.Contains("CK_ServiceTimePricingPolicies_Overtime", constraints);
        Assert.Contains("CK_ServiceTimePricingPolicies_RoundingPolicy", constraints);
    }

    [Fact]
    public void TimePricingEnums_HaveStableValues()
    {
        Assert.Equal(0, (int)TimePricingUnit.Minute);
        Assert.Equal(1, (int)TimePricingUnit.Hour);
        Assert.Equal(2, (int)TimePricingUnit.Day);
        Assert.Equal(0, (int)TimeRoundingPolicy.None);
        Assert.Equal(1, (int)TimeRoundingPolicy.Up);
        Assert.Equal(2, (int)TimeRoundingPolicy.Down);
        Assert.Equal(3, (int)TimeRoundingPolicy.Nearest);
    }
}
