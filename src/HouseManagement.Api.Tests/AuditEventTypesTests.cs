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
            AuditEventTypes.SystemSettingUpdated,
            AuditEventTypes.HouseHelpRatingSubmitted,
            AuditEventTypes.HouseHelpProfileUpdated,
            AuditEventTypes.HouseHelpActivationChanged,
            AuditEventTypes.HouseHelpProfileImageUpdated,
            AuditEventTypes.ServicePriceRuleCreated,
            AuditEventTypes.ServicePriceRuleUpdated,
            AuditEventTypes.ServicePriceRuleActivationChanged,
            AuditEventTypes.ServiceTimePricingPolicyUpdated,
            AuditEventTypes.ServiceFeeCreated,
            AuditEventTypes.ServiceFeeUpdated,
            AuditEventTypes.ServiceFeeActivationChanged,
            AuditEventTypes.ServiceSurchargeCreated,
            AuditEventTypes.ServiceSurchargeUpdated,
            AuditEventTypes.ServiceSurchargeActivationChanged,
            AuditEventTypes.ServicePricingVersionCreated,
            AuditEventTypes.ServicePricingVersionPublished,
            AuditEventTypes.PublicHolidayCreated,
            AuditEventTypes.PublicHolidayDeleted,
            AuditEventTypes.PaymentStatusReconciled
        };

        Assert.Equal(eventTypes.Length, eventTypes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(eventTypes, eventType => Assert.False(string.IsNullOrWhiteSpace(eventType)));
        Assert.Equal(eventTypes.OrderBy(eventType => eventType), AuditEventTypes.All.OrderBy(eventType => eventType));
    }
}
