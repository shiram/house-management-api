namespace HouseManagement.Api.DTOs;

public sealed class HouseHelpEarningsReportDto
{
    public int HouseHelpId { get; set; }
    public string HouseHelpName { get; set; } = null!;
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public int CompletedBookings { get; set; }
    public decimal GrossBookingValue { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public IEnumerable<HouseHelpEarningsBookingDto> Bookings { get; set; } = [];
}

public sealed class HouseHelpEarningsSummaryDto
{
    public int HouseHelpId { get; set; }
    public string HouseHelpName { get; set; } = null!;
    public int CompletedBookings { get; set; }
    public decimal GrossBookingValue { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
}

public sealed class HouseHelpEarningsBookingDto
{
    public int BookingId { get; set; }
    public string Reference { get; set; } = null!;
    public string ServiceName { get; set; } = null!;
    public DateTimeOffset ScheduledStart { get; set; }
    public DateTimeOffset ScheduledEnd { get; set; }
    public decimal BookingTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
}
