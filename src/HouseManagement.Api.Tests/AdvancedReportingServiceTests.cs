using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HouseManagement.Api.Tests;

public class AdvancedReportingServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_ReturnsOperationalRevenueAndServiceBreakdowns()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        SeedReportData(context);
        await context.SaveChangesAsync();

        var service = new AdvancedReportingService(context);
        var summary = await service.GetSummaryAsync(DateTimeOffset.UtcNow.AddDays(-5), DateTimeOffset.UtcNow);

        Assert.Equal(3, summary.TotalBookings);
        Assert.Equal(1, summary.CompletedBookings);
        Assert.Equal(1, summary.AssignedBookings);
        Assert.Equal(1, summary.CancelledBookings);
        Assert.Equal(350m, summary.GrossBookingValue);
        Assert.Equal(25m, summary.DiscountAmount);
        Assert.Equal(100m, summary.PaidAmount);
        Assert.Equal(250m, summary.OutstandingAmount);
        Assert.Equal(116.67m, summary.AverageBookingValue);

        var completed = Assert.Single(summary.ByStatus, item => item.Status == BookingStatus.Completed);
        Assert.Equal(1, completed.BookingCount);
        Assert.Equal(100m, completed.PaidAmount);

        var cleaning = Assert.Single(summary.ByService, item => item.ServiceCode == "CLEANING");
        Assert.Equal(2, cleaning.BookingCount);
        Assert.Equal(1, cleaning.CompletedBookings);
        Assert.Equal(150m, cleaning.GrossBookingValue);
        Assert.Equal(25m, cleaning.DiscountAmount);
    }

    [Fact]
    public async Task ExportBookingsCsvAsync_ReturnsFilteredEscapedRows()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        SeedReportData(context);
        await context.SaveChangesAsync();

        var service = new AdvancedReportingService(context);
        var csv = await service.ExportBookingsCsvAsync(
            DateTimeOffset.UtcNow.AddDays(-5),
            DateTimeOffset.UtcNow,
            BookingStatus.Completed);

        Assert.StartsWith("BookingId,Reference,ServiceCode,ServiceName,Status", csv);
        Assert.Contains("BK-COMPLETE", csv);
        Assert.Contains("\"Cleaning, Deep\"", csv);
        Assert.Contains(",Completed,", csv);
        Assert.Contains(",100,", csv);
        Assert.DoesNotContain("BK-ASSIGNED", csv);
        Assert.DoesNotContain("BK-OLD", csv);
    }

    private static void SeedReportData(HouseContext context)
    {
        var currentDate = DateTimeOffset.UtcNow.AddDays(-2);
        var oldDate = DateTimeOffset.UtcNow.AddDays(-10);

        context.Services.AddRange(
            new Service
            {
                Id = 1,
                Code = "CLEANING",
                Name = "Cleaning, Deep",
                IsActive = true
            },
            new Service
            {
                Id = 2,
                Code = "LAUNDRY",
                Name = "Laundry",
                IsActive = true
            });
        context.Bookings.AddRange(
            new Booking
            {
                Id = 1,
                Reference = "BK-COMPLETE",
                ServiceId = 1,
                AssignedHouseHelpId = 1,
                Status = BookingStatus.Completed,
                ScheduledStart = currentDate,
                ScheduledEnd = currentDate.AddHours(2),
                TotalPrice = 100m,
                DiscountAmount = 25m,
                AppliedPromotionCode = "SAVE25",
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
                        IdempotencyKey = "advanced-paid-key"
                    }
                ]
            },
            new Booking
            {
                Id = 2,
                Reference = "BK-ASSIGNED",
                ServiceId = 1,
                AssignedHouseHelpId = 1,
                Status = BookingStatus.Assigned,
                ScheduledStart = currentDate.AddHours(1),
                ScheduledEnd = currentDate.AddHours(3),
                TotalPrice = 50m
            },
            new Booking
            {
                Id = 3,
                Reference = "BK-CANCELLED",
                ServiceId = 2,
                Status = BookingStatus.Cancelled,
                ScheduledStart = currentDate.AddHours(2),
                ScheduledEnd = currentDate.AddHours(4),
                TotalPrice = 200m
            },
            new Booking
            {
                Id = 4,
                Reference = "BK-OLD",
                ServiceId = 1,
                Status = BookingStatus.Completed,
                ScheduledStart = oldDate,
                ScheduledEnd = oldDate.AddHours(2),
                TotalPrice = 999m
            });
    }
}
