using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public sealed class BookingTransitionValidator : IBookingTransitionValidator
{
    public string? Validate(BookingStatus currentStatus, BookingStatus nextStatus)
    {
        if (!Enum.IsDefined(currentStatus) || !Enum.IsDefined(nextStatus))
        {
            return "The booking status is invalid.";
        }

        if (currentStatus == nextStatus)
        {
            return "A booking cannot transition to its current status.";
        }

        if (!BookingStatusTransitions.IsAllowed(currentStatus, nextStatus))
        {
            return $"A booking with status '{currentStatus}' cannot transition to '{nextStatus}'.";
        }

        return null;
    }
}
