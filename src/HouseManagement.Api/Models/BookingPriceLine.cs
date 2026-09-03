namespace HouseManagement.Api.Models;

// A booking-time price snapshot. It deliberately has no foreign key to a
// mutable service price rule so a historical quote remains self-contained.
public class BookingPriceLine
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public string Description { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
