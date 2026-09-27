namespace HouseManagement.Api.Models;

// A global public holiday date used to evaluate the Holiday surcharge trigger. Not tied to a
// specific service; the same calendar applies to every service's Holiday surcharges.
public class PublicHoliday
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
