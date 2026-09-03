using HouseManagement.Api.Models;

namespace HouseManagement.Api.DTOs;

public sealed class AdvancedReportSummaryDto
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public int TotalBookings { get; set; }
    public int CompletedBookings { get; set; }
    public int AssignedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public int RejectedBookings { get; set; }
    public decimal GrossBookingValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public decimal AverageBookingValue { get; set; }
    public IEnumerable<BookingStatusReportDto> ByStatus { get; set; } = [];
    public IEnumerable<ServiceReportDto> ByService { get; set; } = [];
}

public sealed class BookingStatusReportDto
{
    public BookingStatus Status { get; set; }
    public int BookingCount { get; set; }
    public decimal GrossBookingValue { get; set; }
    public decimal PaidAmount { get; set; }
}

public sealed class ServiceReportDto
{
    public int ServiceId { get; set; }
    public string ServiceCode { get; set; } = null!;
    public string ServiceName { get; set; } = null!;
    public int BookingCount { get; set; }
    public int CompletedBookings { get; set; }
    public decimal GrossBookingValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
}
