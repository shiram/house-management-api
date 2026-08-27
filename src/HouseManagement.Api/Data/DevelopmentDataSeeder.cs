using HouseManagement.Api.Common;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Data;

public static class DevelopmentDataSeeder
{
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
}
