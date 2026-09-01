using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HouseManagement.Api.Tests;

public class HouseHelpRatingServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsRating_ForCompletedBookingOwnedByClient()
    {
        await using var context = CreateContext();
        await SeedCompletedBookingAsync(context, bookingId: 1, clientUserId: 10, houseHelpUserId: 20);

        var service = CreateService(context);
        var result = await service.CreateAsync(clientUserId: 10, bookingId: 1, score: 5, comment: "Great job");

        Assert.NotNull(result.Rating);
        Assert.Null(result.Error);
        Assert.Equal(5, result.Rating!.Score);
        Assert.Equal("Great job", result.Rating.Comment);
        Assert.Equal(1, result.Rating.HouseHelpId);
        Assert.Single(await context.HouseHelpRatings.ToListAsync());

        var notification = await context.Notifications.SingleOrDefaultAsync();
        Assert.NotNull(notification);
        Assert.Equal(20, notification!.UserId);

        var audit = await context.AuditLogs.SingleOrDefaultAsync();
        Assert.NotNull(audit);
        Assert.Equal("househelp_rating.submitted", audit!.Action);
    }

    [Fact]
    public async Task CreateAsync_RejectsScoreOutsideRange()
    {
        await using var context = CreateContext();
        await SeedCompletedBookingAsync(context, bookingId: 1, clientUserId: 10, houseHelpUserId: 20);

        var service = CreateService(context);
        var result = await service.CreateAsync(clientUserId: 10, bookingId: 1, score: 6, comment: null);

        Assert.Null(result.Rating);
        Assert.Equal("Score must be between 1 and 5.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_RejectsWhenBookingNotOwnedByClient()
    {
        await using var context = CreateContext();
        await SeedCompletedBookingAsync(context, bookingId: 1, clientUserId: 10, houseHelpUserId: 20);

        var service = CreateService(context);
        var result = await service.CreateAsync(clientUserId: 999, bookingId: 1, score: 5, comment: null);

        Assert.Null(result.Rating);
        Assert.Equal("You can only rate your own bookings.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_RejectsWhenBookingNotCompleted()
    {
        await using var context = CreateContext();
        await SeedCompletedBookingAsync(context, bookingId: 1, clientUserId: 10, houseHelpUserId: 20, status: BookingStatus.Confirmed);

        var service = CreateService(context);
        var result = await service.CreateAsync(clientUserId: 10, bookingId: 1, score: 5, comment: null);

        Assert.Null(result.Rating);
        Assert.Equal("Only completed bookings with an assigned HouseHelp can be rated.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateRatingForSameBooking()
    {
        await using var context = CreateContext();
        await SeedCompletedBookingAsync(context, bookingId: 1, clientUserId: 10, houseHelpUserId: 20);

        var service = CreateService(context);
        var first = await service.CreateAsync(clientUserId: 10, bookingId: 1, score: 4, comment: null);
        var second = await service.CreateAsync(clientUserId: 10, bookingId: 1, score: 2, comment: null);

        Assert.NotNull(first.Rating);
        Assert.Null(second.Rating);
        Assert.Equal("This booking has already been rated.", second.Error);
        Assert.Single(await context.HouseHelpRatings.ToListAsync());
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsAverageAndCount()
    {
        await using var context = CreateContext();
        await SeedCompletedBookingAsync(context, bookingId: 1, clientUserId: 10, houseHelpUserId: 20);
        await SeedCompletedBookingAsync(context, bookingId: 2, clientUserId: 11, houseHelpUserId: 20, houseHelpId: 1);

        var service = CreateService(context);
        await service.CreateAsync(clientUserId: 10, bookingId: 1, score: 5, comment: null);
        await service.CreateAsync(clientUserId: 11, bookingId: 2, score: 3, comment: null);

        var summary = await service.GetSummaryAsync(1);

        Assert.Equal(2, summary.RatingCount);
        Assert.Equal(4.0, summary.AverageScore);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsNullAverage_WhenNoRatingsExist()
    {
        await using var context = CreateContext();

        var service = CreateService(context);
        var summary = await service.GetSummaryAsync(1);

        Assert.Equal(0, summary.RatingCount);
        Assert.Null(summary.AverageScore);
    }

    private static HouseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HouseContext(options);
    }

    private static HouseHelpRatingService CreateService(HouseContext context)
    {
        return new HouseHelpRatingService(context, new NotificationService(context), new AuditLogService(context));
    }

    private static async Task SeedCompletedBookingAsync(
        HouseContext context,
        int bookingId,
        int clientUserId,
        int houseHelpUserId,
        int houseHelpId = 1,
        BookingStatus status = BookingStatus.Completed)
    {
        if (!await context.Services.AnyAsync(service => service.Id == 1))
        {
            context.Services.Add(new Service { Id = 1, Code = "CLEANING", Name = "Cleaning", IsActive = true });
        }

        if (!await context.HouseHelps.AnyAsync(houseHelp => houseHelp.Id == houseHelpId))
        {
            context.HouseHelps.Add(new HouseHelp
            {
                Id = houseHelpId,
                UserId = houseHelpUserId,
                FirstName = "Helper",
                LastName = "One",
                Phone = "+254700000100",
                City = "Nairobi",
                IsActive = true
            });
        }

        var client = new Client
        {
            Id = clientUserId,
            UserId = clientUserId,
            Name = "Client " + clientUserId,
            Phone = "+254700000" + clientUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        context.Clients.Add(client);

        context.Bookings.Add(new Booking
        {
            Id = bookingId,
            Reference = "BK-RATE-" + bookingId,
            ServiceId = 1,
            ClientId = client.Id,
            AssignedHouseHelpId = houseHelpId,
            ServiceAddress = new ServiceAddress { Line1 = "1 Main Street", City = "Nairobi", Country = "Kenya" },
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(-2),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(-2).AddHours(2),
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-3)
        });

        await context.SaveChangesAsync();
    }
}
