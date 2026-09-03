namespace HouseManagement.Api.Models;

public class Payment
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "UGX";
    public PaymentMethodType MethodType { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string ProviderName { get; set; } = null!;
    public string ProviderReference { get; set; } = null!;
    public string? ProviderCheckoutUrl { get; set; }
    public string? FailureReason { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
