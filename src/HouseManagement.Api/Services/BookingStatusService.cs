using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Services;

public sealed class BookingStatusService : IBookingStatusService
{
    private readonly HouseContext _db;
    private readonly IBookingTransitionValidator _validator;

    public BookingStatusService(HouseContext db, IBookingTransitionValidator validator)
    {
        _db = db;
        _validator = validator;
    }

    public async Task<BookingStatusTransitionResult> TransitionAsync(int bookingId, BookingStatus nextStatus)
    {
        var booking = await _db.Bookings.SingleOrDefaultAsync(item => item.Id == bookingId);
        if (booking == null)
        {
            return new BookingStatusTransitionResult(null, "The requested booking was not found.");
        }

        var validationError = _validator.Validate(booking.Status, nextStatus);
        if (validationError != null)
        {
            return new BookingStatusTransitionResult(null, validationError);
        }

        booking.Status = nextStatus;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return new BookingStatusTransitionResult(booking, null);
    }
}
