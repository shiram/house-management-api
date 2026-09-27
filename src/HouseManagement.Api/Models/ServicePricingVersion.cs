namespace HouseManagement.Api.Models;

// An effective-dated, immutable revision of a service's pricing definition. Booking creation and
// quote calculation (T384) resolve exactly one version using the requested service time, so
// future rate changes never alter the price snapshot copied onto an already-accepted booking.
//
// A version snapshots only the fields relevant to the pricing mode captured at draft time
// (fixed: BasePrice, per-unit: Units, time-based: the Time* fields). Fields for other modes
// remain null and are ignored by resolution/calculation.
public sealed class ServicePricingVersion
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;

    public PricingVersionStatus Status { get; set; } = PricingVersionStatus.Draft;
    public ServicePricingMode PricingMode { get; set; }

    // Inclusive start of the effective window, evaluated against the requested service time (UTC).
    public DateTimeOffset EffectiveFrom { get; set; }

    // Exclusive end of the effective window. Null means "still open" (no later published version yet).
    public DateTimeOffset? EffectiveTo { get; set; }

    // Fixed-mode snapshot.
    public decimal? BasePrice { get; set; }

    // Time-based-mode snapshot, mirroring ServiceTimePricingPolicy.
    public TimePricingUnit? TimeBillingUnit { get; set; }
    public decimal? TimeUnitPrice { get; set; }
    public int? MinimumBillableDurationMinutes { get; set; }
    public int? BillingIncrementMinutes { get; set; }
    public TimeRoundingPolicy? TimeRoundingPolicy { get; set; }
    public int? OvertimeThresholdMinutes { get; set; }
    public decimal? OvertimeUnitPrice { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }

    // Per-unit-mode snapshot.
    public List<ServicePricingVersionUnit> Units { get; set; } = new();
}
