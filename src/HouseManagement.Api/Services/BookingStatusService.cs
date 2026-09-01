using HouseManagement.Api.Data;
using HouseManagement.Api.Common;
using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Services;

public sealed class BookingStatusService : IBookingStatusService
{
    private readonly HouseContext _db;
    private readonly IBookingTransitionValidator _validator;
    private readonly INotificationService _notifications;

    public BookingStatusService(
        HouseContext db,
        IBookingTransitionValidator validator,
        INotificationService notifications)
    {
        _db = db;
        _validator = validator;
        _notifications = notifications;
    }

    public async Task<BookingStatusTransitionResult> TransitionAsync(int bookingId, BookingStatus nextStatus)
    {
        var booking = await _db.Bookings
            .Include(item => item.Client)
            .SingleOrDefaultAsync(item => item.Id == bookingId);
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

        if (booking.Client?.UserId is int clientUserId)
        {
            if (nextStatus == BookingStatus.Confirmed)
            {
                await _notifications.CreateAsync(
                    clientUserId,
                    NotificationTypes.BookingConfirmed,
                    "Booking confirmed",
                    $"Your booking ({booking.Reference}) has been confirmed.",
                    "Booking",
                    booking.Id);
            }
            else
            {
                await _notifications.CreateAsync(
                    clientUserId,
                    NotificationTypes.BookingStatusChanged,
                    "Booking status updated",
                    $"Your booking ({booking.Reference}) status changed to {nextStatus}.",
                    "Booking",
                    booking.Id);
            }
        }
        else
        {
            await _db.SaveChangesAsync();
        }

        return new BookingStatusTransitionResult(booking, null);
    }

    public Task<BookingStatusTransitionResult> CancelAsync(int bookingId)
    {
        return TransitionAsync(bookingId, BookingStatus.Cancelled);
    }

    public Task<BookingStatusTransitionResult> RejectAsync(int bookingId)
    {
        return TransitionAsync(bookingId, BookingStatus.Rejected);
    }

    public Task<BookingStatusTransitionResult> ConfirmAsync(int bookingId)
    {
        return TransitionAsync(bookingId, BookingStatus.Confirmed);
    }

    public Task<BookingStatusTransitionResult> CompleteAsync(int bookingId)
    {
        return TransitionAsync(bookingId, BookingStatus.Completed);
    }
}
