namespace HouseManagement.Api.Models;

// Foundation model for in-app notifications delivered to authenticated users
// (Manager/Admin/HouseHelp/registered clients). Notification.Type is a plain
// string placeholder here; T261 introduces the concrete set of supported
// notification type values used to populate it.
public class Notification
{
    public int Id { get; set; }

    // The recipient of the notification.
    public int UserId { get; set; }

    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;

    // Optional link back to the entity that triggered this notification (e.g. a booking).
    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }

    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
