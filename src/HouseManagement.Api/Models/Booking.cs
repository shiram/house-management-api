namespace HouseManagement.Api.Models;

public class Booking
{
    public int Id { get; set; }
    public string Reference { get; set; } = null!;
    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;
    public int? ClientId { get; set; }
    public Client? Client { get; set; }
    public int? AssignedHouseHelpId { get; set; }
    public HouseHelp? AssignedHouseHelp { get; set; }
    public int ServiceAddressId { get; set; }
    public ServiceAddress ServiceAddress { get; set; } = null!;
    public DateTimeOffset ScheduledStart { get; set; }
    public DateTimeOffset ScheduledEnd { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Requested;
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
