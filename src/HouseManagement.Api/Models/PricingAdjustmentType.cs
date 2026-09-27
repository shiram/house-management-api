namespace HouseManagement.Api.Models;

// Shared by fees and surcharges: whether the configured amount is a percentage of the base
// subtotal or a fixed currency amount. Mirrors PromotionDiscountType's shape for consistency.
public enum PricingAdjustmentType
{
    Percentage = 0,
    FixedAmount = 1
}
