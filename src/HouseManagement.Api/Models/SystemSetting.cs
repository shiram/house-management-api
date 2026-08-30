namespace HouseManagement.Api.Models;

public class SystemSetting
{
    public int Id { get; set; }

    // Stable, unique key used to look up a setting (e.g. "Booking.MaxAdvanceDays").
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
