using HouseManagement.Api.Common;

namespace HouseManagement.Api.Tests;

public sealed class PricingSettingsTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData("not-a-number", 0)]
    [InlineData("18", 18)]
    [InlineData("18.5", 18.5)]
    [InlineData("-5", 0)]
    [InlineData("150", 100)]
    public void ParseTaxRatePercentage_ClampsAndDefaultsToZeroForInvalidValues(string? rawValue, decimal expected)
    {
        var rate = PricingSettings.ParseTaxRatePercentage(rawValue);

        Assert.Equal(expected, rate);
    }

    [Theory]
    [InlineData(null, "UGX")]
    [InlineData("", "UGX")]
    [InlineData("   ", "UGX")]
    [InlineData("KE", "UGX")]
    [InlineData("KESH", "UGX")]
    [InlineData("US1", "UGX")]
    [InlineData("kes", "KES")]
    [InlineData("USD", "USD")]
    public void ParseCurrencyCode_NormalizesValidCodesAndDefaultsOtherwise(string? rawValue, string expected)
    {
        var currency = PricingSettings.ParseCurrencyCode(rawValue);

        Assert.Equal(expected, currency);
    }
}
