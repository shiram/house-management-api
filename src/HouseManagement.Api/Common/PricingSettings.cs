using System.Globalization;
using System.Linq;

namespace HouseManagement.Api.Common;

// Shared tax/VAT configuration lookup used by both booking creation (BookingService) and the
// public quote endpoint (ServicesController via IServiceCatalogService). The rate is stored as a
// generic SystemSetting so an Admin can change it through the existing settings endpoints without
// a dedicated pricing-tax administration surface; no payment provider is consulted or involved.
public static class PricingSettings
{
    // SystemSetting.Key holding the platform-wide tax/VAT rate as a percentage (e.g. "18" for 18%).
    public const string TaxRatePercentageKey = "Pricing.TaxRatePercentage";

    // SystemSetting.Key holding the platform-wide default currency code used for receipt totals.
    public const string CurrencyCodeKey = "Pricing.CurrencyCode";

    // The currency used when no valid Pricing.CurrencyCode setting is configured.
    public const string DefaultCurrencyCode = "UGX";

    // Parses the stored setting value into a valid percentage, clamped to [0, 100].
    // Returns 0 (no tax) when the setting is missing, blank, or not a valid number.
    public static decimal ParseTaxRatePercentage(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue) ||
            !decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate))
        {
            return 0m;
        }

        return Math.Clamp(rate, 0m, 100m);
    }

    // Parses the stored setting value into a valid three-letter currency code, falling back to
    // DefaultCurrencyCode when the setting is missing, blank, or not a plausible currency code.
    public static string ParseCurrencyCode(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return DefaultCurrencyCode;
        }

        var normalized = rawValue.Trim().ToUpperInvariant();
        return normalized.Length == 3 && normalized.All(char.IsLetter) ? normalized : DefaultCurrencyCode;
    }
}
