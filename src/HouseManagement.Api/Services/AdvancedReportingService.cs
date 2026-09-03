using System.Globalization;
using System.Text;
using HouseManagement.Api.Data;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Services;

public sealed class AdvancedReportingService : IAdvancedReportingService
{
    private readonly HouseContext _db;

    public AdvancedReportingService(HouseContext db)
    {
        _db = db;
    }

    public async Task<AdvancedReportSummaryDto> GetSummaryAsync(DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        var bookings = await BookingQuery(from, to)
            .OrderBy(booking => booking.Service.Name)
            .ThenBy(booking => booking.Service.Code)
            .ThenBy(booking => booking.ScheduledStart)
            .ToListAsync();

        var gross = bookings.Sum(booking => booking.TotalPrice);
        var discount = bookings.Sum(booking => booking.DiscountAmount);
        var paid = bookings.Sum(GetSucceededPaymentTotal);

        return new AdvancedReportSummaryDto
        {
            From = from,
            To = to,
            TotalBookings = bookings.Count,
            CompletedBookings = bookings.Count(booking => booking.Status == BookingStatus.Completed),
            AssignedBookings = bookings.Count(booking => booking.Status == BookingStatus.Assigned),
            CancelledBookings = bookings.Count(booking => booking.Status == BookingStatus.Cancelled),
            RejectedBookings = bookings.Count(booking => booking.Status == BookingStatus.Rejected),
            GrossBookingValue = gross,
            DiscountAmount = discount,
            PaidAmount = paid,
            OutstandingAmount = gross - paid,
            AverageBookingValue = bookings.Count == 0 ? 0 : Math.Round(gross / bookings.Count, 2, MidpointRounding.AwayFromZero),
            ByStatus = BuildStatusReports(bookings),
            ByService = BuildServiceReports(bookings)
        };
    }

    public async Task<string> ExportBookingsCsvAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, BookingStatus? status = null)
    {
        var query = BookingQuery(from, to);
        if (status.HasValue)
        {
            query = query.Where(booking => booking.Status == status.Value);
        }

        var bookings = await query
            .OrderBy(booking => booking.ScheduledStart)
            .ThenBy(booking => booking.Id)
            .ToListAsync();

        var builder = new StringBuilder();
        builder.AppendLine("BookingId,Reference,ServiceCode,ServiceName,Status,ScheduledStart,ScheduledEnd,AssignedHouseHelpId,TotalPrice,DiscountAmount,PaidAmount,OutstandingAmount,AppliedPromotionCode");

        foreach (var booking in bookings)
        {
            var paid = GetSucceededPaymentTotal(booking);
            AppendCsvRow(builder,
            [
                booking.Id.ToString(CultureInfo.InvariantCulture),
                booking.Reference,
                booking.Service?.Code ?? string.Empty,
                booking.Service?.Name ?? string.Empty,
                booking.Status.ToString(),
                booking.ScheduledStart.ToString("O", CultureInfo.InvariantCulture),
                booking.ScheduledEnd.ToString("O", CultureInfo.InvariantCulture),
                booking.AssignedHouseHelpId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                booking.TotalPrice.ToString(CultureInfo.InvariantCulture),
                booking.DiscountAmount.ToString(CultureInfo.InvariantCulture),
                paid.ToString(CultureInfo.InvariantCulture),
                (booking.TotalPrice - paid).ToString(CultureInfo.InvariantCulture),
                booking.AppliedPromotionCode ?? string.Empty
            ]);
        }

        return builder.ToString();
    }

    private IQueryable<Booking> BookingQuery(DateTimeOffset? from, DateTimeOffset? to)
    {
        var query = _db.Bookings
            .AsNoTracking()
            .Include(booking => booking.Service)
            .Include(booking => booking.Payments)
            .AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(booking => booking.ScheduledStart >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(booking => booking.ScheduledStart <= to.Value);
        }

        return query;
    }

    private static IReadOnlyList<BookingStatusReportDto> BuildStatusReports(IReadOnlyList<Booking> bookings)
    {
        return bookings
            .GroupBy(booking => booking.Status)
            .Select(group => new BookingStatusReportDto
            {
                Status = group.Key,
                BookingCount = group.Count(),
                GrossBookingValue = group.Sum(booking => booking.TotalPrice),
                PaidAmount = group.Sum(GetSucceededPaymentTotal)
            })
            .OrderBy(report => report.Status)
            .ToList();
    }

    private static IReadOnlyList<ServiceReportDto> BuildServiceReports(IReadOnlyList<Booking> bookings)
    {
        return bookings
            .GroupBy(booking => new
            {
                booking.ServiceId,
                ServiceCode = booking.Service?.Code ?? string.Empty,
                ServiceName = booking.Service?.Name ?? string.Empty
            })
            .Select(group =>
            {
                var gross = group.Sum(booking => booking.TotalPrice);
                var paid = group.Sum(GetSucceededPaymentTotal);

                return new ServiceReportDto
                {
                    ServiceId = group.Key.ServiceId,
                    ServiceCode = group.Key.ServiceCode,
                    ServiceName = group.Key.ServiceName,
                    BookingCount = group.Count(),
                    CompletedBookings = group.Count(booking => booking.Status == BookingStatus.Completed),
                    GrossBookingValue = gross,
                    DiscountAmount = group.Sum(booking => booking.DiscountAmount),
                    PaidAmount = paid,
                    OutstandingAmount = gross - paid
                };
            })
            .OrderByDescending(report => report.GrossBookingValue)
            .ThenBy(report => report.ServiceName)
            .ToList();
    }

    private static decimal GetSucceededPaymentTotal(Booking booking)
    {
        return booking.Payments
            .Where(payment => payment.Status == PaymentStatus.Succeeded)
            .Sum(payment => payment.Amount);
    }

    private static void AppendCsvRow(StringBuilder builder, IReadOnlyList<string> values)
    {
        builder.AppendLine(string.Join(",", values.Select(EscapeCsv)));
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
