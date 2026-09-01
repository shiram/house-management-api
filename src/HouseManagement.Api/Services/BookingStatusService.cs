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
    private readonly IAuditLogService _auditLogs;

    public BookingStatusService(
        HouseContext db,
        IBookingTransitionValidator validator,
        INotificationService notifications,
        IAuditLogService auditLogs)
    {
        _db = db;
        _validator = validator;
        _notifications = notifications;
        _auditLogs = auditLogs;
    }

    public async Task<BookingStatusTransitionResult> TransitionAsync(int bookingId, BookingStatus nextStatus, int? changedByUserId = null)
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

        var previousStatus = booking.Status;
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

        await _auditLogs.LogAsync(
            AuditEventTypes.BookingStatusChanged,
            nameof(Booking),
            entityId: booking.Id,
            userId: changedByUserId,
            details: $"{previousStatus} -> {nextStatus}");

        return new BookingStatusTransitionResult(booking, null);
    }

    public Task<BookingStatusTransitionResult> CancelAsync(int bookingId, int? changedByUserId = null)
    {
        return TransitionAsync(bookingId, BookingStatus.Cancelled, changedByUserId);
    }

    public Task<BookingStatusTransitionResult> RejectAsync(int bookingId, int? changedByUserId = null)
    {
        return TransitionAsync(bookingId, BookingStatus.Rejected, changedByUserId);
    }

    public Task<BookingStatusTransitionResult> ConfirmAsync(int bookingId, int? changedByUserId = null)
    {
        return TransitionAsync(bookingId, BookingStatus.Confirmed, changedByUserId);
    }

    public Task<BookingStatusTransitionResult> CompleteAsync(int bookingId, int? changedByUserId = null)
    {
        return TransitionAsync(bookingId, BookingStatus.Completed, changedByUserId);
    }
}
