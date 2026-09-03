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
}
