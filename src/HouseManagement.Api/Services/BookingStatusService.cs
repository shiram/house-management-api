using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Services;

public sealed class BookingStatusService : IBookingStatusService
{
    private readonly HouseContext _db;

    public BookingStatusService(HouseContext db)
    {
        _db = db;
    }

    public async Task<BookingStatusTransitionResult> TransitionAsync(int bookingId, BookingStatus nextStatus)
    {
        var booking = await _db.Bookings.SingleOrDefaultAsync(item => item.Id == bookingId);
        if (booking == null)
        {
            return new BookingStatusTransitionResult(null, "The requested booking was not found.");
        }

        if (!BookingStatusTransitions.IsAllowed(booking.Status, nextStatus))
        {
            return new BookingStatusTransitionResult(
                null,
                $"A booking with status '{booking.Status}' cannot transition to '{nextStatus}'.");
        }

        booking.Status = nextStatus;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return new BookingStatusTransitionResult(booking, null);
    }
}
