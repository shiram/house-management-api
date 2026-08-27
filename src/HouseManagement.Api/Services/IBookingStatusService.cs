using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IBookingStatusService
{
    Task<BookingStatusTransitionResult> TransitionAsync(int bookingId, BookingStatus nextStatus);
    Task<BookingStatusTransitionResult> CancelAsync(int bookingId);
    Task<BookingStatusTransitionResult> RejectAsync(int bookingId);
}

public sealed record BookingStatusTransitionResult(Booking? Booking, string? Error);
