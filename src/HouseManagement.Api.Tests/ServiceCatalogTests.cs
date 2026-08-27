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
}
