namespace HouseManagement.Api.Models;

public static class BookingStatusTransitions
{
    private static readonly IReadOnlyDictionary<BookingStatus, BookingStatus[]> Allowed = new Dictionary<BookingStatus, BookingStatus[]>
    {
        [BookingStatus.Requested] = new[] { BookingStatus.Confirmed, BookingStatus.Rejected, BookingStatus.Cancelled },
        [BookingStatus.Confirmed] = new[] { BookingStatus.Assigned, BookingStatus.Cancelled },
        [BookingStatus.Assigned] = new[] { BookingStatus.InProgress, BookingStatus.Cancelled },
        [BookingStatus.InProgress] = new[] { BookingStatus.Completed },
        [BookingStatus.Rejected] = Array.Empty<BookingStatus>(),
        [BookingStatus.Cancelled] = Array.Empty<BookingStatus>(),
        [BookingStatus.Completed] = Array.Empty<BookingStatus>()
    };

    public static bool IsAllowed(BookingStatus current, BookingStatus next)
    {
        return Allowed.TryGetValue(current, out var transitions) && transitions.Contains(next);
    }
}
