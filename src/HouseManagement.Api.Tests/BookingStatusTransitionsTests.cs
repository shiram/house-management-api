using HouseManagement.Api.Models;
using Xunit;

namespace HouseManagement.Api.Tests;

public class BookingStatusTransitionsTests
{
    [Theory]
    [InlineData(BookingStatus.Requested, BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Confirmed, BookingStatus.Assigned)]
    [InlineData(BookingStatus.Assigned, BookingStatus.InProgress)]
    [InlineData(BookingStatus.InProgress, BookingStatus.Completed)]
    public void IsAllowed_ReturnsTrueForValidTransitions(BookingStatus current, BookingStatus next)
    {
        Assert.True(BookingStatusTransitions.IsAllowed(current, next));
    }

    [Theory]
    [InlineData(BookingStatus.Completed, BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Cancelled, BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Requested, BookingStatus.Completed)]
    public void IsAllowed_ReturnsFalseForInvalidTransitions(BookingStatus current, BookingStatus next)
    {
        Assert.False(BookingStatusTransitions.IsAllowed(current, next));
    }
}
