using HouseManagement.Api.Common;
using Xunit;

namespace HouseManagement.Api.Tests;

public class AuditEventTypesTests
{
    [Fact]
    public void All_ContainsEachUniqueNonEmptyAuditEventType()
    {
        var eventTypes = new[]
        {
            AuditEventTypes.AuthenticationLoginSucceeded,
            AuditEventTypes.AuthenticationLoginFailed,
            AuditEventTypes.BookingStatusChanged,
            AuditEventTypes.BookingAssigned,
            AuditEventTypes.UserRoleChanged,
            AuditEventTypes.UserActivated,
            AuditEventTypes.SystemSettingUpdated
        };

        Assert.Equal(eventTypes.Length, eventTypes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(eventTypes, eventType => Assert.False(string.IsNullOrWhiteSpace(eventType)));
        Assert.Equal(eventTypes.OrderBy(eventType => eventType), AuditEventTypes.All.OrderBy(eventType => eventType));
    }
}
