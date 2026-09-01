using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IBookingStatusService
{
    Task<BookingStatusTransitionResult> TransitionAsync(int bookingId, BookingStatus nextStatus, int? changedByUserId = null);
    Task<BookingStatusTransitionResult> CancelAsync(int bookingId, int? changedByUserId = null);
    Task<BookingStatusTransitionResult> RejectAsync(int bookingId, int? changedByUserId = null);
    Task<BookingStatusTransitionResult> ConfirmAsync(int bookingId, int? changedByUserId = null);
    Task<BookingStatusTransitionResult> CompleteAsync(int bookingId, int? changedByUserId = null);
}

public sealed record BookingStatusTransitionResult(Booking? Booking, string? Error);
