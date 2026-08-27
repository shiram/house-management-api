using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IBookingStatusService
{
    Task<BookingStatusTransitionResult> TransitionAsync(int bookingId, BookingStatus nextStatus);
}

public sealed record BookingStatusTransitionResult(Booking? Booking, string? Error);
