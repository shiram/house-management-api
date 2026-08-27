namespace HouseManagement.Api.Models;

public enum BookingStatus
{
    Requested = 0,
    Rejected = 1,
    Cancelled = 2,
    Confirmed = 3,
    Assigned = 4,
    InProgress = 5,
    Completed = 6
}
