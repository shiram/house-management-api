using HouseManagement.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace HouseManagement.Api.DTOs;

public class CreateBookingRequest
{
    [Range(1, int.MaxValue)]
    public int ServiceId { get; set; }
    public DateTimeOffset ScheduledStart { get; set; }
    public DateTimeOffset ScheduledEnd { get; set; }
    [Required]
    public ServiceAddressRequest Address { get; set; } = new();
    public string? Notes { get; set; }
}

public sealed class CreateAnonymousBookingRequest : CreateBookingRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string ContactName { get; set; } = null!;
    [Required]
    [Phone]
    public string Phone { get; set; } = null!;
    [EmailAddress]
    public string? Email { get; set; }
}

public sealed class CreateAuthenticatedBookingRequest : CreateBookingRequest
{
}

public sealed class ServiceAddressRequest
{
    [Required]
    [StringLength(250)]
    public string Line1 { get; set; } = null!;
    public string? Line2 { get; set; }
    [Required]
    [StringLength(100)]
    public string City { get; set; } = null!;
    public string? Region { get; set; }
    public string? PostalCode { get; set; }
    [Required]
    [StringLength(100)]
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
