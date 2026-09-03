using HouseManagement.Api.Data;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Services;

public sealed class HouseHelpEarningsService : IHouseHelpEarningsService
{
    private readonly HouseContext _db;

    public HouseHelpEarningsService(HouseContext db)
    {
        _db = db;
    }

    public async Task<HouseHelpEarningsReportDto?> GetForHouseHelpAsync(
        int houseHelpId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
    {
        var houseHelp = await _db.HouseHelps
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == houseHelpId);
        if (houseHelp == null)
        {
            return null;
        }

        var bookings = await CompletedBookingQuery(from, to)
            .Where(booking => booking.AssignedHouseHelpId == houseHelpId)
            .ToListAsync();

        return BuildReport(houseHelp, bookings, from, to);
    }

    public async Task<HouseHelpEarningsReportDto?> GetForUserAsync(
        int userId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
    {
        var houseHelpId = await _db.HouseHelps
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => (int?)item.Id)
            .SingleOrDefaultAsync();

        return houseHelpId.HasValue
            ? await GetForHouseHelpAsync(houseHelpId.Value, from, to)
            : null;
    }

    public async Task<IReadOnlyList<HouseHelpEarningsSummaryDto>> GetSummariesAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
    {
        var houseHelps = await _db.HouseHelps.AsNoTracking().ToListAsync();
        var bookings = await CompletedBookingQuery(from, to).ToListAsync();

        return houseHelps
            .Select(houseHelp =>
            {
                var houseHelpBookings = bookings
                    .Where(booking => booking.AssignedHouseHelpId == houseHelp.Id)
                    .ToList();
                var gross = houseHelpBookings.Sum(booking => booking.TotalPrice);
                var paid = houseHelpBookings.Sum(GetSucceededPaymentTotal);

                return new HouseHelpEarningsSummaryDto
                {
                    HouseHelpId = houseHelp.Id,
                    HouseHelpName = GetHouseHelpName(houseHelp),
                    CompletedBookings = houseHelpBookings.Count,
                    GrossBookingValue = gross,
                    PaidAmount = paid,
                    OutstandingAmount = gross - paid
                };
            })
            .Where(summary => summary.CompletedBookings > 0)
            .OrderByDescending(summary => summary.GrossBookingValue)
            .ThenBy(summary => summary.HouseHelpName)
            .ToList();
    }

    private IQueryable<Booking> CompletedBookingQuery(DateTimeOffset? from, DateTimeOffset? to)
    {
        var query = _db.Bookings
            .AsNoTracking()
            .Include(booking => booking.Service)
            .Include(booking => booking.Payments)
            .Where(booking =>
                booking.Status == BookingStatus.Completed &&
                booking.AssignedHouseHelpId.HasValue);

        if (from.HasValue)
        {
            query = query.Where(booking => booking.ScheduledStart >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(booking => booking.ScheduledStart <= to.Value);
        }

        return query
            .OrderByDescending(booking => booking.ScheduledStart)
            .ThenByDescending(booking => booking.Id);
    }

    private static HouseHelpEarningsReportDto BuildReport(
        HouseHelp houseHelp,
        IReadOnlyList<Booking> bookings,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        var bookingDtos = bookings
            .Select(booking =>
            {
                var paid = GetSucceededPaymentTotal(booking);
                return new HouseHelpEarningsBookingDto
                {
                    BookingId = booking.Id,
                    Reference = booking.Reference,
                    ServiceName = booking.Service?.Name ?? string.Empty,
                    ScheduledStart = booking.ScheduledStart,
                    ScheduledEnd = booking.ScheduledEnd,
                    BookingTotal = booking.TotalPrice,
                    PaidAmount = paid,
                    OutstandingAmount = booking.TotalPrice - paid
                };
            })
            .ToList();
        var gross = bookingDtos.Sum(booking => booking.BookingTotal);
        var paidAmount = bookingDtos.Sum(booking => booking.PaidAmount);

        return new HouseHelpEarningsReportDto
        {
            HouseHelpId = houseHelp.Id,
            HouseHelpName = GetHouseHelpName(houseHelp),
            From = from,
            To = to,
            CompletedBookings = bookingDtos.Count,
            GrossBookingValue = gross,
            PaidAmount = paidAmount,
            OutstandingAmount = gross - paidAmount,
            Bookings = bookingDtos
        };
    }

    private static decimal GetSucceededPaymentTotal(Booking booking)
    {
        return booking.Payments
            .Where(payment => payment.Status == PaymentStatus.Succeeded)
            .Sum(payment => payment.Amount);
    }

    private static string GetHouseHelpName(HouseHelp houseHelp)
    {
        return $"{houseHelp.FirstName} {houseHelp.LastName}".Trim();
    }
}
