using HouseManagement.Api.Data;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Common;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HouseManagement.Api.Tests;

public class BookingServiceTests
{
    [Fact]
    public async Task CreateAnonymousAsync_PersistsBookingClientAndAddress()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Services.Add(new Service { Id = 1, Code = "CLEANING", Name = "Cleaning", BasePrice = 25, IsActive = true });
        await context.SaveChangesAsync();

        var service = CreateBookingService(context);
        var result = await service.CreateAnonymousAsync(CreateRequest());

        Assert.NotNull(result.Booking);
        Assert.StartsWith("BK-", result.Booking!.Reference);
        Assert.Equal(BookingStatus.Requested, result.Booking.Status);
        Assert.Single(await context.Clients.ToListAsync());
        Assert.Single(await context.ServiceAddresses.ToListAsync());
        Assert.Single(await context.Bookings.ToListAsync());
    }

    [Fact]
    public async Task CreateAnonymousAsync_RejectsInactiveServiceAndInvalidSchedule()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Services.Add(new Service { Id = 1, Code = "OLD", Name = "Old", IsActive = false });
        await context.SaveChangesAsync();
        var service = CreateBookingService(context);

        var inactive = await service.CreateAnonymousAsync(CreateRequest());
        var invalid = await service.CreateAnonymousAsync(CreateRequest(1, DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow.AddHours(1)));

        Assert.Null(inactive.Booking);
        Assert.Null(invalid.Booking);
        Assert.Empty(await context.Bookings.ToListAsync());
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsBookingWithServiceAndAddress()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Services.Add(new Service { Id = 1, Code = "CLEANING", Name = "Cleaning", IsActive = true });
        context.ServiceAddresses.Add(new ServiceAddress
        {
            Id = 1,
            Line1 = "1 Main Street",
            City = "Nairobi",
            Country = "Kenya"
        });
        context.Bookings.Add(new Booking
        {
            Id = 1,
            Reference = "BK-DETAILS",
            ServiceId = 1,
            ServiceAddressId = 1,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            Status = BookingStatus.Requested
        });
        await context.SaveChangesAsync();

        var service = CreateBookingService(context);
        var booking = await service.GetByIdAsync(1);

        Assert.NotNull(booking);
        Assert.Equal("Cleaning", booking!.Service.Name);
        Assert.Equal("1 Main Street", booking.ServiceAddress.Line1);
        Assert.Null(await service.GetByIdAsync(999));
    }

    [Fact]
    public async Task GetListAsync_FiltersByStatusAndOrdersNewestFirst()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Services.Add(new Service { Id = 1, Code = "CLEANING", Name = "Cleaning", IsActive = true });
        context.ServiceAddresses.AddRange(
            new ServiceAddress { Id = 1, Line1 = "1 Main Street", City = "Nairobi", Country = "Kenya" },
            new ServiceAddress { Id = 2, Line1 = "2 Main Street", City = "Nairobi", Country = "Kenya" });
        context.Bookings.AddRange(
            new Booking
            {
                Id = 1,
                Reference = "BK-OLDER",
                ServiceId = 1,
                ServiceAddressId = 1,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
                ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
                Status = BookingStatus.Requested,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2)
            },
            new Booking
            {
                Id = 2,
                Reference = "BK-NEWER",
                ServiceId = 1,
                ServiceAddressId = 2,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(2),
                ScheduledEnd = DateTimeOffset.UtcNow.AddDays(2).AddHours(1),
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
            });
        await context.SaveChangesAsync();

        var service = CreateBookingService(context);
        var all = await service.GetListAsync();
        var confirmed = await service.GetListAsync(BookingStatus.Confirmed);

        Assert.Equal(new[] { 2, 1 }, all.Select(booking => booking.Id));
        Assert.Single(confirmed);
        Assert.Equal(2, confirmed[0].Id);
    }

    [Fact]
    public async Task TransitionAsync_UpdatesStatusAndTimestamp()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Bookings.Add(new Booking
        {
            Id = 1,
            Reference = "BK-TRANSITION",
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            Status = BookingStatus.Requested
        });
        await context.SaveChangesAsync();

        var service = new BookingStatusService(context, new BookingTransitionValidator());
        var result = await service.TransitionAsync(1, BookingStatus.Confirmed);

        Assert.NotNull(result.Booking);
        Assert.Equal(BookingStatus.Confirmed, result.Booking!.Status);
        Assert.NotNull(result.Booking.UpdatedAt);
    }

    [Fact]
    public async Task TransitionAsync_RejectsInvalidTransitionAndMissingBooking()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Bookings.Add(new Booking
        {
            Id = 1,
            Reference = "BK-INVALID",
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            Status = BookingStatus.Requested
        });
        await context.SaveChangesAsync();

        var service = new BookingStatusService(context, new BookingTransitionValidator());
        var invalid = await service.TransitionAsync(1, BookingStatus.Completed);
        var missing = await service.TransitionAsync(999, BookingStatus.Confirmed);

        Assert.Null(invalid.Booking);
        Assert.Contains("cannot transition", invalid.Error);
        Assert.Null(missing.Booking);
        Assert.Contains("not found", missing.Error);
        Assert.Equal(BookingStatus.Requested, (await context.Bookings.SingleAsync()).Status);
    }

    [Fact]
    public async Task CancelAsync_AllowsCancellableStatusesAndRejectsInProgress()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Bookings.AddRange(
            new Booking
            {
                Id = 1,
                Reference = "BK-CANCEL",
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
                ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
                Status = BookingStatus.Confirmed
            },
            new Booking
            {
                Id = 2,
                Reference = "BK-IN-PROGRESS",
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
                ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
                Status = BookingStatus.InProgress
            });
        await context.SaveChangesAsync();

        var service = new BookingStatusService(context, new BookingTransitionValidator());
        var cancelled = await service.CancelAsync(1);
        var rejected = await service.CancelAsync(2);

        Assert.Equal(BookingStatus.Cancelled, cancelled.Booking!.Status);
        Assert.Null(rejected.Booking);
        Assert.Contains("cannot transition", rejected.Error);
    }

    [Fact]
    public async Task RejectAsync_AllowsRequestedAndRejectsConfirmed()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Bookings.AddRange(
            new Booking
            {
                Id = 1,
                Reference = "BK-REJECT",
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
                ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
                Status = BookingStatus.Requested
            },
            new Booking
            {
                Id = 2,
                Reference = "BK-CONFIRMED",
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
                ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
                Status = BookingStatus.Confirmed
            });
        await context.SaveChangesAsync();

        var service = new BookingStatusService(context, new BookingTransitionValidator());
        var rejected = await service.RejectAsync(1);
        var invalid = await service.RejectAsync(2);

        Assert.Equal(BookingStatus.Rejected, rejected.Booking!.Status);
        Assert.Null(invalid.Booking);
        Assert.Contains("cannot transition", invalid.Error);
    }

    [Fact]
    public async Task ConfirmAsync_AllowsRequestedAndRejectsRejected()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Bookings.AddRange(
            new Booking
            {
                Id = 1,
                Reference = "BK-CONFIRM",
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
                ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
                Status = BookingStatus.Requested
            },
            new Booking
            {
                Id = 2,
                Reference = "BK-REJECTED",
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
                ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
                Status = BookingStatus.Rejected
            });
        await context.SaveChangesAsync();

        var service = new BookingStatusService(context, new BookingTransitionValidator());
        var confirmed = await service.ConfirmAsync(1);
        var invalid = await service.ConfirmAsync(2);

        Assert.Equal(BookingStatus.Confirmed, confirmed.Booking!.Status);
        Assert.Null(invalid.Booking);
        Assert.Contains("cannot transition", invalid.Error);
    }

    [Fact]
    public async Task CompleteAsync_AllowsInProgressAndRejectsRequested()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Bookings.AddRange(
            new Booking
            {
                Id = 1,
                Reference = "BK-COMPLETE",
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
                ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
                Status = BookingStatus.InProgress
            },
            new Booking
            {
                Id = 2,
                Reference = "BK-REQUESTED",
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
                ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
                Status = BookingStatus.Requested
            });
        await context.SaveChangesAsync();

        var service = new BookingStatusService(context, new BookingTransitionValidator());
        var completed = await service.CompleteAsync(1);
        var invalid = await service.CompleteAsync(2);

        Assert.Equal(BookingStatus.Completed, completed.Booking!.Status);
        Assert.Null(invalid.Booking);
        Assert.Contains("cannot transition", invalid.Error);
    }

    [Fact]
    public async Task CreateAnonymousAsync_NotifiesActiveManagersOnly()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Services.Add(new Service { Id = 1, Code = "CLEANING", Name = "Cleaning", BasePrice = 25, IsActive = true });
        context.Users.AddRange(
            new User { Id = 1, UserName = "active-manager", Email = "active-manager@example.com", PasswordHash = "hash", Role = Roles.Manager, IsActive = true },
            new User { Id = 2, UserName = "inactive-manager", Email = "inactive-manager@example.com", PasswordHash = "hash", Role = Roles.Manager, IsActive = false },
            new User { Id = 3, UserName = "admin", Email = "admin@example.com", PasswordHash = "hash", Role = Roles.Admin, IsActive = true });
        await context.SaveChangesAsync();

        var result = await CreateBookingService(context).CreateAnonymousAsync(CreateRequest());

        Assert.NotNull(result.Booking);
        var notification = await context.Notifications.SingleAsync();
        Assert.Equal(1, notification.UserId);
        Assert.Equal(NotificationTypes.BookingCreated, notification.Type);
        Assert.Equal("New booking request", notification.Title);
        Assert.Equal($"A new booking request ({result.Booking!.Reference}) has been submitted.", notification.Message);
        Assert.Equal("Booking", notification.RelatedEntityType);
        Assert.Equal(result.Booking.Id, notification.RelatedEntityId);
    }

    private static BookingService CreateBookingService(HouseContext context)
    {
        return new BookingService(context, new NotificationService(context));
    }

    private static CreateAnonymousBookingRequest CreateRequest(
        int serviceId = 1,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null)
    {
        return new CreateAnonymousBookingRequest
        {
            ContactName = "Anonymous Client",
            Phone = "+254700000001",
            Email = "client@example.com",
            ServiceId = serviceId,
            ScheduledStart = start ?? DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = end ?? DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            Address = new ServiceAddressRequest
            {
                Line1 = "1 Main Street",
                City = "Nairobi",
                Country = "Kenya"
            }
        };
    }
}
