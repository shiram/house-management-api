using HouseManagement.Api.Data;
using HouseManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HouseManagement.Api.Tests;

public class DevelopmentDataSeederTests
{
    [Fact]
    public async Task SeedRolesAsync_IsIdempotentAndCreatesExpectedRoles()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DevelopmentSeed:Password"] = "DevelopmentPassword123!"
            })
            .Build();

        await DevelopmentDataSeeder.SeedRolesAsync(
            context,
            new PasswordHasher(),
            configuration,
            NullLogger.Instance);
        await DevelopmentDataSeeder.SeedRolesAsync(
            context,
            new PasswordHasher(),
            configuration,
            NullLogger.Instance);

        var users = await context.Users.OrderBy(user => user.Role).ToListAsync();
        Assert.Equal(3, users.Count);
        Assert.Equal(new[] { "admin", "househelp", "manager" }, users.Select(user => user.Role));
        Assert.All(users, user => Assert.True(new PasswordHasher().Verify(user.PasswordHash, "DevelopmentPassword123!")));
    }

    [Fact]
    public async Task SeedServicesAsync_IsIdempotentAndCreatesActiveSamples()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);

        await DevelopmentDataSeeder.SeedServicesAsync(context, NullLogger.Instance);
        await DevelopmentDataSeeder.SeedServicesAsync(context, NullLogger.Instance);

        var services = await context.Services.OrderBy(service => service.Code).ToListAsync();
        Assert.Equal(new[] { "HOUSE_CLEANING", "LAUNDRY" }, services.Select(service => service.Code));
        Assert.All(services, service => Assert.True(service.IsActive));
    }
}
