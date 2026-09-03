using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HouseManagement.Api.Tests;

public class HouseHelpEarningsServiceTests
{
    [Fact]
    public async Task GetForHouseHelpAsync_ReturnsCompletedAssignedBookingEarnings()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        var includedDate = DateTimeOffset.UtcNow.AddDays(-2);
        var excludedDate = DateTimeOffset.UtcNow.AddDays(-10);
        SeedReportData(context, includedDate, excludedDate);
        await context.SaveChangesAsync();

        var service = new HouseHelpEarningsService(context);

        var report = await service.GetForHouseHelpAsync(
            1,
            DateTimeOffset.UtcNow.AddDays(-5),
            DateTimeOffset.UtcNow);

        Assert.NotNull(report);
        Assert.Equal(1, report!.HouseHelpId);
        Assert.Equal("Jane Helper", report.HouseHelpName);
        Assert.Equal(2, report.CompletedBookings);
        Assert.Equal(150m, report.GrossBookingValue);
        Assert.Equal(100m, report.PaidAmount);
        Assert.Equal(50m, report.OutstandingAmount);
        Assert.Equal(new[] { "BK-UNPAID", "BK-PAID" }, report.Bookings.Select(booking => booking.Reference));
    }

    [Fact]
    public async Task GetForUserAsync_ReturnsOnlyLinkedHouseHelpEarnings()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        SeedReportData(context, DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-10));
        await context.SaveChangesAsync();

        var service = new HouseHelpEarningsService(context);

        var report = await service.GetForUserAsync(10);
        var missing = await service.GetForUserAsync(999);

        Assert.NotNull(report);
        Assert.Equal(1, report!.HouseHelpId);
        Assert.Equal(3, report.CompletedBookings);
        Assert.Null(missing);
    }

    [Fact]
    public async Task GetSummariesAsync_ReturnsHouseHelpsWithCompletedBookingsOrderedByGrossValue()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        SeedReportData(context, DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-10));
        await context.SaveChangesAsync();

        var service = new HouseHelpEarningsService(context);

        var summaries = await service.GetSummariesAsync();

        Assert.Equal(new[] { 1, 2 }, summaries.Select(summary => summary.HouseHelpId));
        Assert.Equal(230m, summaries[0].GrossBookingValue);
        Assert.Equal(75m, summaries[1].GrossBookingValue);
    }

    private static void SeedReportData(HouseContext context, DateTimeOffset includedDate, DateTimeOffset excludedDate)
    {
        context.HouseHelps.AddRange(
            new HouseHelp
            {
                Id = 1,
                UserId = 10,
                FirstName = "Jane",
                LastName = "Helper",
                Phone = "+256700000001",
                City = "Kampala"
            },
            new HouseHelp
            {
                Id = 2,
                UserId = 20,
                FirstName = "Mary",
                LastName = "Cleaner",
                Phone = "+256700000002",
                City = "Entebbe"
            });
        context.Services.Add(new Service
        {
            Id = 1,
            Code = "CLEANING",
            Name = "Cleaning",
            IsActive = true
        });
        context.Bookings.AddRange(
            new Booking
            {
                Id = 1,
                Reference = "BK-PAID",
                ServiceId = 1,
                AssignedHouseHelpId = 1,
                Status = BookingStatus.Completed,
                ScheduledStart = includedDate,
                ScheduledEnd = includedDate.AddHours(2),
                TotalPrice = 100m,
                Payments =
                [
                    new Payment
                    {
                        Amount = 100m,
                        Currency = "UGX",
                        MethodType = PaymentMethodType.MobileMoney,
                        Status = PaymentStatus.Succeeded,
                        ProviderName = "test",
                        ProviderReference = "paid",
                        IdempotencyKey = "paid-key"
                    }
                ]
            },
            new Booking
            {
                Id = 2,
                Reference = "BK-UNPAID",
                ServiceId = 1,
                AssignedHouseHelpId = 1,
                Status = BookingStatus.Completed,
                ScheduledStart = includedDate.AddHours(1),
                ScheduledEnd = includedDate.AddHours(3),
                TotalPrice = 50m
            },
            new Booking
            {
                Id = 3,
                Reference = "BK-OLD",
                ServiceId = 1,
                AssignedHouseHelpId = 1,
                Status = BookingStatus.Completed,
                ScheduledStart = excludedDate,
                ScheduledEnd = excludedDate.AddHours(2),
                TotalPrice = 80m
            },
            new Booking
            {
                Id = 4,
                Reference = "BK-OTHER",
                ServiceId = 1,
                AssignedHouseHelpId = 2,
                Status = BookingStatus.Completed,
                ScheduledStart = includedDate,
                ScheduledEnd = includedDate.AddHours(2),
                TotalPrice = 75m
            },
            new Booking
            {
                Id = 5,
                Reference = "BK-NOT-COMPLETE",
                ServiceId = 1,
                AssignedHouseHelpId = 1,
                Status = BookingStatus.Assigned,
                ScheduledStart = includedDate,
                ScheduledEnd = includedDate.AddHours(2),
                TotalPrice = 999m
            });
    }
}
