using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HouseManagement.Api.Tests;

public class AvailabilityServiceTests
{
    [Fact]
    public async Task GetAsync_ReturnsActiveSlotsAndOverlappingExceptionsOnly()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.HouseHelps.Add(new HouseHelp { Id = 1, FirstName = "A", LastName = "B", Phone = "1", City = "Nairobi" });
        context.HouseHelpAvailabilities.AddRange(
            new HouseHelpAvailability { HouseHelpId = 1, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(8), EndTime = new TimeOnly(12), IsActive = true },
            new HouseHelpAvailability { HouseHelpId = 1, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeOnly(8), EndTime = new TimeOnly(12), IsActive = false });
        context.HouseHelpAvailabilityExceptions.AddRange(
            new HouseHelpAvailabilityException { HouseHelpId = 1, StartsAt = new DateTimeOffset(2026, 8, 28, 10, 0, 0, TimeSpan.Zero), EndsAt = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero), Reason = "Leave", IsActive = true },
            new HouseHelpAvailabilityException { HouseHelpId = 1, StartsAt = new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero), EndsAt = new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero), Reason = "Inactive", IsActive = false });
        await context.SaveChangesAsync();

        var service = new AvailabilityService(context);
        var result = await service.GetAsync(
            1,
            new DateTimeOffset(2026, 8, 28, 11, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 29, 0, 0, 0, TimeSpan.Zero));

        Assert.NotNull(result);
        Assert.Single(result!.WeeklySlots);
        Assert.Single(result.Exceptions);
        Assert.Equal("Leave", result.Exceptions.Single().Reason);
        Assert.Null(await service.GetAsync(999));
    }

    [Fact]
    public async Task ReplaceWeeklyAsync_ReplacesExistingSlots()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.HouseHelps.Add(new HouseHelp { Id = 1, FirstName = "A", LastName = "B", Phone = "1", City = "Nairobi" });
        context.HouseHelpAvailabilities.Add(new HouseHelpAvailability
        {
            HouseHelpId = 1,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeOnly(8),
            EndTime = new TimeOnly(12)
        });
        await context.SaveChangesAsync();

        var service = new AvailabilityService(context);
        var updated = await service.ReplaceWeeklyAsync(1, new[]
        {
            new HouseHelpAvailability
            {
                DayOfWeek = DayOfWeek.Friday,
                StartTime = new TimeOnly(9),
                EndTime = new TimeOnly(17),
                IsActive = true
            }
        });

        var slots = await context.HouseHelpAvailabilities.ToListAsync();
        Assert.True(updated);
        Assert.Single(slots);
        Assert.Equal(DayOfWeek.Friday, slots[0].DayOfWeek);
        Assert.False(await service.ReplaceWeeklyAsync(999, Array.Empty<HouseHelpAvailability>()));
    }
}
