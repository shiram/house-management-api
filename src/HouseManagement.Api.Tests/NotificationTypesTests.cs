using HouseManagement.Api.Common;
using Xunit;

namespace HouseManagement.Api.Tests;

public class NotificationTypesTests
{
    [Fact]
    public void All_ContainsEveryDefinedConstant_WithNoDuplicates()
    {
        var expected = new[]
        {
            NotificationTypes.BookingCreated,
            NotificationTypes.BookingConfirmed,
            NotificationTypes.BookingAssigned,
            NotificationTypes.BookingStatusChanged
        };

        Assert.Equal(expected.Length, NotificationTypes.All.Count);
        foreach (var value in expected)
        {
            Assert.Contains(value, NotificationTypes.All);
        }
    }

    [Fact]
    public void All_Values_AreNonEmptyAndDistinct()
    {
        Assert.All(NotificationTypes.All, value => Assert.False(string.IsNullOrWhiteSpace(value)));
        Assert.Equal(NotificationTypes.All.Count, new HashSet<string>(NotificationTypes.All).Count);
    }
}
