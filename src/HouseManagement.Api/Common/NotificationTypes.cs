namespace HouseManagement.Api.Common;

// Canonical set of supported values for Notification.Type. Keep these stable once
// persisted, since existing Notification rows will reference them by string value.
public static class NotificationTypes
{
    // A new booking request was created and needs Manager/Admin attention (T263).
    public const string BookingCreated = "booking.created";

    // A booking moved to Confirmed and the client should be informed (T264).
    public const string BookingConfirmed = "booking.confirmed";

    // A HouseHelp was assigned to a booking (T265).
    public const string BookingAssigned = "booking.assigned";

    // A booking's status changed (cancelled/rejected/completed/etc.) and the client
    // should be informed (T266). Distinct from BookingConfirmed/BookingAssigned so a
    // recipient can filter for/mute general status-change noise separately.
    public const string BookingStatusChanged = "booking.status_changed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        BookingCreated,
        BookingConfirmed,
        BookingAssigned,
        BookingStatusChanged
    };
}
