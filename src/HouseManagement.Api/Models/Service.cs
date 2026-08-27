namespace HouseManagement.Api.Models;

public class Service
{
    public int Id { get; set; }

    // Stable business identifier used by clients and integrations.
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public List<Booking> Bookings { get; set; } = new();
}
