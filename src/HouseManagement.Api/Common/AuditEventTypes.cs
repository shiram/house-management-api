namespace HouseManagement.Api.Common;

// Canonical action values persisted in AuditLog.Action. Keep these stable once
// audit records have been written so filters and operational reports remain valid.
public static class AuditEventTypes
{
    public const string AuthenticationLoginSucceeded = "authentication.login_succeeded";
    public const string AuthenticationLoginFailed = "authentication.login_failed";
    public const string BookingStatusChanged = "booking.status_changed";
    public const string BookingAssigned = "booking.assigned";
    public const string UserRoleChanged = "user.role_changed";
    public const string UserActivated = "user.activated";
    public const string SystemSettingUpdated = "system_setting.updated";
    public const string HouseHelpRatingSubmitted = "househelp_rating.submitted";
    public const string HouseHelpProfileUpdated = "househelp.profile_updated";
    public const string HouseHelpActivationChanged = "househelp.activation_changed";
    public const string HouseHelpProfileImageUpdated = "househelp.profile_image_updated";

    // T391: pricing administration events (T388 endpoints). Each is scoped to the pricing
    // building block it affects; EntityId/Details identify the affected service or record.
    // These never retroactively change an already-accepted booking's immutable price snapshot
    // (see T390); they only audit changes to the live catalog used for future quotes/bookings.
    public const string ServicePriceRuleCreated = "service_price_rule.created";
    public const string ServicePriceRuleUpdated = "service_price_rule.updated";
    public const string ServicePriceRuleActivationChanged = "service_price_rule.activation_changed";
    public const string ServiceTimePricingPolicyUpdated = "service_time_pricing_policy.updated";
    public const string ServiceFeeCreated = "service_fee.created";
    public const string ServiceFeeUpdated = "service_fee.updated";
    public const string ServiceFeeActivationChanged = "service_fee.activation_changed";
    public const string ServiceSurchargeCreated = "service_surcharge.created";
    public const string ServiceSurchargeUpdated = "service_surcharge.updated";
    public const string ServiceSurchargeActivationChanged = "service_surcharge.activation_changed";
    public const string ServicePricingVersionCreated = "service_pricing_version.created";
    public const string ServicePricingVersionPublished = "service_pricing_version.published";
    public const string PublicHolidayCreated = "public_holiday.created";
    public const string PublicHolidayDeleted = "public_holiday.deleted";

    // T403: emitted when a payment's status is changed as a result of provider reconciliation
    // (webhook/IPN-triggered status re-check). EntityId is the Payment id.
    public const string PaymentStatusReconciled = "payment.status_reconciled";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        AuthenticationLoginSucceeded,
        AuthenticationLoginFailed,
        BookingStatusChanged,
        BookingAssigned,
        UserRoleChanged,
        UserActivated,
        SystemSettingUpdated,
        HouseHelpRatingSubmitted,
        HouseHelpProfileUpdated,
        HouseHelpActivationChanged,
        HouseHelpProfileImageUpdated,
        ServicePriceRuleCreated,
        ServicePriceRuleUpdated,
        ServicePriceRuleActivationChanged,
        ServiceTimePricingPolicyUpdated,
        ServiceFeeCreated,
        ServiceFeeUpdated,
        ServiceFeeActivationChanged,
        ServiceSurchargeCreated,
        ServiceSurchargeUpdated,
        ServiceSurchargeActivationChanged,
        ServicePricingVersionCreated,
        ServicePricingVersionPublished,
        PublicHolidayCreated,
        PublicHolidayDeleted,
        PaymentStatusReconciled
    };
}
