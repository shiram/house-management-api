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
}
