using HouseManagement.Api.Models;

namespace HouseManagement.Api.Infrastructure.Payments;

// Options for the Pesapal API 3.0 adapter (T401/T402). BaseUrl should point at the Pesapal
// sandbox host during development/testing and the production host once go-live is approved.
// ConsumerKey/ConsumerSecret are merchant secrets and must only ever be supplied through
// environment variables (see README.local.md); they are never committed to appsettings.json.
public sealed class PesapalPaymentGatewayOptions
{
    public bool Enabled { get; set; }
    public string ProviderName { get; set; } = "pesapal";

    // e.g. "https://cybqa.pesapal.com/pesapalv3" (sandbox) or "https://pay.pesapal.com/v3" (production).
    public string? BaseUrl { get; set; }

    public string? ConsumerKey { get; set; }
    public string? ConsumerSecret { get; set; }

    // This API's public endpoint that Pesapal redirects the customer back to and calls as an IPN
    // callback once a transaction reaches a terminal or interim state (wired up in T403).
    public string? CallbackUrl { get; set; }

    // The notification_id returned by a one-time POST /api/URLSetup/RegisterIPN call against
    // CallbackUrl. Registration is an operational/setup step, not performed per-request, so the
    // resulting id is configured here rather than requested on every payment.
    public string? IpnId { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
    public string[] SupportedCurrencies { get; set; } = ["UGX"];
    public PaymentMethodType[] SupportedMethods { get; set; } =
    [
        PaymentMethodType.Card,
        PaymentMethodType.MobileMoney
    ];
}
