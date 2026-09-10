namespace HouseManagement.Api.Models;

public sealed class ServiceTimePricingPolicy
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;
    public TimePricingUnit BillingUnit { get; set; } = TimePricingUnit.Hour;
    public decimal UnitPrice { get; set; }
    public int MinimumBillableDurationMinutes { get; set; }
    public int BillingIncrementMinutes { get; set; }
    public TimeRoundingPolicy RoundingPolicy { get; set; } = TimeRoundingPolicy.Up;
    public int? OvertimeThresholdMinutes { get; set; }
    public decimal? OvertimeUnitPrice { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
