using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HouseManagement.Api.Tests;

public class ClientHouseHelpPreferenceServiceTests
{
    [Fact]
    public async Task AddAsync_PersistsPreferenceForCurrentClientsActiveHouseHelp()
    {
        await using var context = CreateContext();
        await SeedAsync(context);

        var result = await CreateService(context).AddAsync(10, 1);

        var preference = Assert.IsType<ClientHouseHelpPreference>(result.Preference);
        Assert.Null(result.Error);
        Assert.Equal(10, preference.ClientId);
        Assert.Equal(1, preference.HouseHelpId);
        Assert.Single(await context.ClientHouseHelpPreferences.ToListAsync());
    }

    [Fact]
    public async Task AddAsync_RejectsInactiveHouseHelp()
    {
        await using var context = CreateContext();
        await SeedAsync(context);

        var result = await CreateService(context).AddAsync(10, 2);

        Assert.Null(result.Preference);
        Assert.Equal("The requested active HouseHelp was not found.", result.Error);
    }

    [Fact]
    public async Task AddAsync_RejectsDuplicatePreference()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var service = CreateService(context);

        var first = await service.AddAsync(10, 1);
        var second = await service.AddAsync(10, 1);

        Assert.NotNull(first.Preference);
        Assert.Null(second.Preference);
        Assert.Equal("This HouseHelp is already preferred.", second.Error);
        Assert.Single(await context.ClientHouseHelpPreferences.ToListAsync());
    }

    [Fact]
    public async Task GetForClientAsync_ReturnsOnlyCurrentClientsActivePreferences()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        context.ClientHouseHelpPreferences.AddRange(
            new ClientHouseHelpPreference
            {
                ClientId = 10,
                HouseHelpId = 1,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
            },
            new ClientHouseHelpPreference
            {
                ClientId = 10,
                HouseHelpId = 2,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new ClientHouseHelpPreference
            {
                ClientId = 11,
                HouseHelpId = 1,
                CreatedAt = DateTimeOffset.UtcNow
            });
        await context.SaveChangesAsync();

        var preferences = await CreateService(context).GetForClientAsync(10);

        var preference = Assert.Single(preferences);
        Assert.Equal(10, preference.ClientId);
        Assert.Equal(1, preference.HouseHelpId);
    }

    [Fact]
    public async Task RemoveAsync_RemovesOnlyCurrentClientsPreference()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        context.ClientHouseHelpPreferences.Add(new ClientHouseHelpPreference
        {
            ClientId = 11,
            HouseHelpId = 1,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var removed = await CreateService(context).RemoveAsync(10, 1);

        Assert.False(removed);
        Assert.Single(await context.ClientHouseHelpPreferences.ToListAsync());
    }

    private static HouseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HouseContext(options);
    }

    private static ClientHouseHelpPreferenceService CreateService(HouseContext context)
    {
        return new ClientHouseHelpPreferenceService(context);
    }

    private static async Task SeedAsync(HouseContext context)
    {
        context.Clients.AddRange(
            new Client { Id = 10, UserId = 10, Name = "Client One", Phone = "+254700000010" },
            new Client { Id = 11, UserId = 11, Name = "Client Two", Phone = "+254700000011" });
        context.HouseHelps.AddRange(
            new HouseHelp
            {
                Id = 1,
                FirstName = "Active",
                LastName = "Helper",
                Phone = "+254700000001",
                City = "Nairobi",
                IsActive = true
            },
            new HouseHelp
            {
                Id = 2,
                FirstName = "Inactive",
                LastName = "Helper",
                Phone = "+254700000002",
                City = "Nairobi",
                IsActive = false
            });
        await context.SaveChangesAsync();
    }
}
