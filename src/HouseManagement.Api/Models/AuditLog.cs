namespace HouseManagement.Api.Models;

public class AuditLog
{
    public int Id { get; set; }

    // Short machine-readable action name (e.g. "user.role_changed", "booking.assigned").
    public string Action { get; set; } = null!;

    // Name of the entity type affected (e.g. "User", "Booking"), for grouping/filtering.
    public string EntityType { get; set; } = null!;
    public int? EntityId { get; set; }

    // The authenticated user who performed the action, when known.
    public int? UserId { get; set; }

    // Optional free-form context (e.g. old/new values), kept small and non-sensitive.
    public string? Details { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
