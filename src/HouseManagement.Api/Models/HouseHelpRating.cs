namespace HouseManagement.Api.Models;

// A client-submitted rating/review for a completed booking. Exactly one rating
// may exist per booking (enforced by a unique index and by service-level checks),
// and it may only be created once the booking has reached BookingStatus.Completed
// with a househelp assigned.
public class HouseHelpRating
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public int HouseHelpId { get; set; }
    public HouseHelp HouseHelp { get; set; } = null!;

    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;

    // 1 (lowest) to 5 (highest).
    public int Score { get; set; }

    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
