using HouseManagement.Api.Common;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Data;

public static class DevelopmentDataSeeder
{
    private static readonly Service[] SampleServices =
    {
        new()
        {
            Code = "HOUSE_CLEANING",
            Name = "House Cleaning",
            Description = "Standard residential house cleaning",
            BasePrice = 35m
        },
        new()
        {
            Code = "LAUNDRY",
            Name = "Laundry",
            Description = "Residential laundry and folding service",
            BasePrice = 25m
        }
    };

    public static async Task SeedRolesAsync(
        HouseContext db,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        Microsoft.Extensions.Logging.ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var password = configuration["DevelopmentSeed:Password"]
            ?? Environment.GetEnvironmentVariable("DEV_SEED_PASSWORD");

        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Development role seeding skipped because DEV_SEED_PASSWORD is not configured.");
            return;
        }

        var users = new[]
        {
            (UserName: "dev-admin", Email: "dev-admin@housemanagement.local", Role: Roles.Admin),
            (UserName: "dev-manager", Email: "dev-manager@housemanagement.local", Role: Roles.Manager),
            (UserName: "dev-househelp", Email: "dev-househelp@housemanagement.local", Role: Roles.HouseHelp)
        };

        foreach (var seed in users)
        {
            var exists = await db.Users.AnyAsync(
                user => user.Email == seed.Email || user.UserName == seed.UserName,
                cancellationToken);

            if (!exists)
            {
                db.Users.Add(new User
                {
                    UserName = seed.UserName,
                    Email = seed.Email,
                    PasswordHash = passwordHasher.Hash(password),
                    Role = seed.Role,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Development role seed completed.");
    }

    public static async Task SeedServicesAsync(
        HouseContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        foreach (var sample in SampleServices)
        {
            if (!await db.Services.AnyAsync(service => service.Code == sample.Code, cancellationToken))
            {
                db.Services.Add(new Service
                {
                    Code = sample.Code,
                    Name = sample.Name,
                    Description = sample.Description,
                    BasePrice = sample.BasePrice,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Development service seed completed.");
    }
}
