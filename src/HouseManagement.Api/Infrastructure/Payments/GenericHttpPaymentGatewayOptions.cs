using HouseManagement.Api.Models;

namespace HouseManagement.Api.Infrastructure.Payments;

public sealed class GenericHttpPaymentGatewayOptions
{
    public bool Enabled { get; set; }
    public string ProviderName { get; set; } = "generic-http";
    public string? EndpointUrl { get; set; }
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public string[] SupportedCurrencies { get; set; } = ["UGX"];
    public PaymentMethodType[] SupportedMethods { get; set; } =
    [
        PaymentMethodType.Card,
        PaymentMethodType.MobileMoney
    ];
}
