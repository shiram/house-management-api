namespace HouseManagement.Api.Models;

// A generally applicable additional charge for a service, such as a service or platform fee.
// Unlike a surcharge, a fee is not conditional on booking context and applies whenever active.
public class ServiceFee
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;
    public string Name { get; set; } = null!;
    public PricingAdjustmentType AdjustmentType { get; set; } = PricingAdjustmentType.FixedAmount;

    // A fixed currency amount, or a percentage (0-100) of the base charge subtotal.
    public decimal Amount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
