using HouseManagement.Api.Data;
using HouseManagement.Api.DTOs;
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

        var service = new BookingService(context);
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
        var service = new BookingService(context);

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

        var service = new BookingService(context);
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

        var service = new BookingService(context);
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

        var service = new BookingStatusService(context);
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

        var service = new BookingStatusService(context);
        var invalid = await service.TransitionAsync(1, BookingStatus.Completed);
        var missing = await service.TransitionAsync(999, BookingStatus.Confirmed);

        Assert.Null(invalid.Booking);
        Assert.Contains("cannot transition", invalid.Error);
        Assert.Null(missing.Booking);
        Assert.Contains("not found", missing.Error);
        Assert.Equal(BookingStatus.Requested, (await context.Bookings.SingleAsync()).Status);
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
