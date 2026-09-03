using HouseManagement.Api.Data;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HouseManagement.Api.Tests;

public class RepeatBookingServiceTests
{
    [Fact]
    public async Task RepeatAsync_CreatesRequestedBookingWithCopiedServiceAddressAndNotes()
    {
        await using var context = CreateContext();
        await SeedBookingAsync(context, BookingStatus.Completed, clientUserId: 10);
        var scheduledStart = DateTimeOffset.UtcNow.AddDays(7);

        var result = await CreateService(context).RepeatAsync(10, 1, new RepeatBookingRequest
        {
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledStart.AddHours(2)
        });

        var repeated = Assert.IsType<Booking>(result.Booking);
        Assert.Null(result.Error);
        Assert.NotEqual(1, repeated.Id);
        Assert.NotEqual("BK-SOURCE", repeated.Reference);
        Assert.Equal(BookingStatus.Requested, repeated.Status);
        Assert.Equal(1, repeated.ServiceId);
        Assert.Equal(10, repeated.ClientId);
        Assert.Equal("Repeat Street", repeated.ServiceAddress.Line1);
        Assert.Equal("Nairobi", repeated.ServiceAddress.City);
        Assert.Equal("Please use the side entrance.", repeated.Notes);
        Assert.Equal(scheduledStart, repeated.ScheduledStart);
    }

    [Fact]
    public async Task RepeatAsync_RejectsAnotherClientsCompletedBooking()
    {
        await using var context = CreateContext();
        await SeedBookingAsync(context, BookingStatus.Completed, clientUserId: 10);

        var result = await CreateService(context).RepeatAsync(99, 1, ValidRequest());

        Assert.Null(result.Booking);
        Assert.Equal("The requested completed booking was not found.", result.Error);
        Assert.Single(await context.Bookings.ToListAsync());
    }

    [Fact]
    public async Task RepeatAsync_RejectsBookingThatIsNotCompleted()
    {
        await using var context = CreateContext();
        await SeedBookingAsync(context, BookingStatus.Cancelled, clientUserId: 10);

        var result = await CreateService(context).RepeatAsync(10, 1, ValidRequest());

        Assert.Null(result.Booking);
        Assert.Equal("The requested completed booking was not found.", result.Error);
        Assert.Single(await context.Bookings.ToListAsync());
    }

    [Fact]
    public async Task RepeatAsync_RejectsNonFutureOrInvalidSchedule()
    {
        await using var context = CreateContext();
        await SeedBookingAsync(context, BookingStatus.Completed, clientUserId: 10);
        var service = CreateService(context);

        var result = await service.RepeatAsync(10, 1, new RepeatBookingRequest
        {
            ScheduledStart = DateTimeOffset.UtcNow.AddHours(2),
            ScheduledEnd = DateTimeOffset.UtcNow.AddHours(1)
        });

        Assert.Null(result.Booking);
        Assert.Equal("The requested service time must be a future range.", result.Error);
        Assert.Single(await context.Bookings.ToListAsync());
    }

    private static HouseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HouseContext(options);
    }

    private static BookingService CreateService(HouseContext context)
    {
        return new BookingService(
            context,
            new NotificationService(context),
            new AuditLogService(context));
    }

    private static RepeatBookingRequest ValidRequest()
    {
        var start = DateTimeOffset.UtcNow.AddDays(7);
        return new RepeatBookingRequest
        {
            ScheduledStart = start,
            ScheduledEnd = start.AddHours(2)
        };
    }

    private static async Task SeedBookingAsync(HouseContext context, BookingStatus status, int clientUserId)
    {
        var client = new Client
        {
            Id = 10,
            UserId = clientUserId,
            Name = "Repeat Client",
            Phone = "+254700000010",
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Services.Add(new Service
        {
            Id = 1,
            Code = "CLEANING",
            Name = "Cleaning",
            BasePrice = 25m,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.Clients.Add(client);
        context.Bookings.Add(new Booking
        {
            Id = 1,
            Reference = "BK-SOURCE",
            ServiceId = 1,
            ClientId = client.Id,
            ServiceAddress = new ServiceAddress
            {
                Line1 = "Repeat Street",
                City = "Nairobi",
                Country = "Kenya"
            },
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(-3),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(-3).AddHours(2),
            Status = status,
            Notes = "Please use the side entrance.",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-4)
        });
        await context.SaveChangesAsync();
    }
}
