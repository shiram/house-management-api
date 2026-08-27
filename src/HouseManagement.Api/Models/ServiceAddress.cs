namespace HouseManagement.Api.Models;

public class ServiceAddress
{
    public int Id { get; set; }
    public string Line1 { get; set; } = null!;
    public string? Line2 { get; set; }
    public string City { get; set; } = null!;
    public string? Region { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = null!;
    public List<Booking> Bookings { get; set; } = new();
}
