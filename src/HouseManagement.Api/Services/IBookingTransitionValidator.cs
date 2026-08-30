using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IBookingTransitionValidator
{
    string? Validate(BookingStatus currentStatus, BookingStatus nextStatus);
}
