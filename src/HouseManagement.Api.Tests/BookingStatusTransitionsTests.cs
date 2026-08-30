using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Xunit;

namespace HouseManagement.Api.Tests;

public class BookingStatusTransitionsTests
{
    private readonly BookingTransitionValidator _validator = new();

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

    [Fact]
    public void Validator_RejectsSameStatus()
    {
        var error = _validator.Validate(BookingStatus.Requested, BookingStatus.Requested);

        Assert.Equal("A booking cannot transition to its current status.", error);
    }

    [Fact]
    public void Validator_RejectsUndefinedStatus()
    {
        var error = _validator.Validate((BookingStatus)999, BookingStatus.Confirmed);

        Assert.Equal("The booking status is invalid.", error);
    }

    [Fact]
    public void Validator_AllowsMappedTransition()
    {
        var error = _validator.Validate(BookingStatus.Requested, BookingStatus.Confirmed);

        Assert.Null(error);
    }
}
