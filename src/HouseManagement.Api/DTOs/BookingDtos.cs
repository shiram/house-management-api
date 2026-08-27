using HouseManagement.Api.Models;

namespace HouseManagement.Api.DTOs;

public class CreateBookingRequest
{
    public int ServiceId { get; set; }
    public DateTimeOffset ScheduledStart { get; set; }
    public DateTimeOffset ScheduledEnd { get; set; }
    public ServiceAddressRequest Address { get; set; } = new();
    public string? Notes { get; set; }
}

public sealed class ServiceAddressRequest
{
    public string Line1 { get; set; } = null!;
    public string? Line2 { get; set; }
    public string City { get; set; } = null!;
    public string? Region { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = null!;
}

public sealed class BookingDto
{
    public int Id { get; set; }
    public string Reference { get; set; } = null!;
    public int ServiceId { get; set; }
    public string ServiceCode { get; set; } = null!;
    public string ServiceName { get; set; } = null!;
    public DateTimeOffset ScheduledStart { get; set; }
    public DateTimeOffset ScheduledEnd { get; set; }
    public BookingStatus Status { get; set; }
    public ServiceAddressRequest Address { get; set; } = new();
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
