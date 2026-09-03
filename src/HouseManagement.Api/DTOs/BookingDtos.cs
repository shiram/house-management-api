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
    public IEnumerable<BookingPriceItemRequest> PricingItems { get; set; } = [];
    public string? PromotionCode { get; set; }
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

public sealed class RepeatBookingRequest
{
    public DateTimeOffset ScheduledStart { get; set; }
    public DateTimeOffset ScheduledEnd { get; set; }
    public IEnumerable<BookingPriceItemRequest> PricingItems { get; set; } = [];
    public string? PromotionCode { get; set; }
}

public sealed class BookingPriceItemRequest
{
    [Range(1, int.MaxValue)]
    public int PriceRuleId { get; set; }

    [Range(1, 100000)]
    public int Quantity { get; set; }
}

public sealed class AssignHouseHelpRequest
{
    [Range(1, int.MaxValue)]
    public int HouseHelpId { get; set; }
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
    public int? AssignedHouseHelpId { get; set; }
    public int? AssignedByUserId { get; set; }
    public DateTimeOffset? AssignedAt { get; set; }
    public ServiceAddressRequest Address { get; set; } = new();
    public string? Notes { get; set; }
    public string? AppliedPromotionCode { get; set; }
    public string? AppliedPromotionName { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalPrice { get; set; }
    public IEnumerable<BookingPriceLineDto> PriceLines { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class BookingPriceLineDto
{
    public string Description { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class BookingTrackingDto
{
    public int Id { get; set; }
    public string Reference { get; set; } = null!;
    public string ServiceName { get; set; } = null!;
    public DateTimeOffset ScheduledStart { get; set; }
    public DateTimeOffset ScheduledEnd { get; set; }
    public BookingStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
