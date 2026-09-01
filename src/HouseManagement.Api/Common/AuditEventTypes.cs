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

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        AuthenticationLoginSucceeded,
        AuthenticationLoginFailed,
        BookingStatusChanged,
        BookingAssigned,
        UserRoleChanged,
        UserActivated,
        SystemSettingUpdated
    };
}
