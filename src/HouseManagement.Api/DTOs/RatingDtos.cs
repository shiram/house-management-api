using System.ComponentModel.DataAnnotations;

namespace HouseManagement.Api.DTOs;

public class CreateHouseHelpRatingRequest
{
    [Range(1, 5)]
    public int Score { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }
}

public class HouseHelpRatingDto
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public int HouseHelpId { get; set; }
    public int Score { get; set; }
    public string? Comment { get; set; }

    // First name only, to avoid exposing full client identity on a public listing.
    public string? ClientDisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class HouseHelpRatingSummaryDto
{
    public int HouseHelpId { get; set; }
    public double? AverageScore { get; set; }
    public int RatingCount { get; set; }
}
