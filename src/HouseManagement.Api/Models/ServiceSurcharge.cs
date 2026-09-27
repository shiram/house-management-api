namespace HouseManagement.Api.Models;

// A conditional additional charge for a service, applied only when its trigger context matches
// the requested booking (weekend, holiday, after-hours, urgent lead time, or location).
public class ServiceSurcharge
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;
    public string Name { get; set; } = null!;
    public SurchargeTriggerType TriggerType { get; set; }
    public PricingAdjustmentType AdjustmentType { get; set; } = PricingAdjustmentType.FixedAmount;

    // A fixed currency amount, or a percentage (0-100) of the base charge subtotal.
    public decimal Amount { get; set; }
    public bool IsActive { get; set; } = true;

    // AfterHours trigger only: the after-hours window as minutes since midnight (0-1439), using
    // the scheduled start's own time-of-day. The window wraps past midnight when Start > End
    // (e.g. 18:00-08:00 is stored as 1080-480).
    public int? AfterHoursStartMinutes { get; set; }
    public int? AfterHoursEndMinutes { get; set; }

    // Urgent trigger only: the surcharge applies when the lead time between now and the
    // scheduled start is below this many minutes.
    public int? UrgentLeadTimeMinutes { get; set; }

    // Location trigger only: the surcharge applies when the booking address's city or region
    // matches this value, case-insensitively.
    public string? LocationMatch { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
