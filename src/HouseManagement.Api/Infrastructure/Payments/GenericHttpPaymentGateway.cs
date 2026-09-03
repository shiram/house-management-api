using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HouseManagement.Api.Models;
using Microsoft.Extensions.Options;

namespace HouseManagement.Api.Infrastructure.Payments;

public sealed class GenericHttpPaymentGateway : IPaymentGateway
{
    private readonly HttpClient _httpClient;
    private readonly GenericHttpPaymentGatewayOptions _options;

    public GenericHttpPaymentGateway(
        HttpClient httpClient,
        IOptions<GenericHttpPaymentGatewayOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string ProviderName => string.IsNullOrWhiteSpace(_options.ProviderName)
        ? "generic-http"
        : _options.ProviderName.Trim();

    public bool Supports(PaymentMethodType methodType, string currency)
    {
        return _options.Enabled &&
            IsEndpointConfigured() &&
            _options.SupportedMethods.Contains(methodType) &&
            _options.SupportedCurrencies.Any(item => string.Equals(item, currency, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<PaymentGatewayCreateResult> CreatePaymentAsync(
        PaymentGatewayCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(request.MethodType, request.Currency))
        {
            throw new InvalidOperationException("The generic HTTP payment gateway is not configured for this payment request.");
        }

        var endpoint = new Uri(_options.EndpointUrl!, UriKind.Absolute);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new GenericHttpPaymentRequest(
                request.BookingId,
                request.BookingReference,
                request.Amount,
                request.Currency,
                request.MethodType.ToString(),
                request.IdempotencyKey))
        };

        httpRequest.Headers.Add("X-Idempotency-Key", request.IdempotencyKey);
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey.Trim());
        }

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"The generic HTTP payment gateway returned HTTP {(int)response.StatusCode}.");
        }

        var gatewayResponse = await response.Content.ReadFromJsonAsync<GenericHttpPaymentResponse>(
            cancellationToken: cancellationToken);
        if (gatewayResponse == null ||
            string.IsNullOrWhiteSpace(gatewayResponse.Status) ||
            string.IsNullOrWhiteSpace(gatewayResponse.ProviderReference))
        {
            throw new InvalidOperationException("The generic HTTP payment gateway returned an invalid response.");
        }

        if (!Enum.TryParse<PaymentStatus>(gatewayResponse.Status, ignoreCase: true, out var status))
        {
            throw new InvalidOperationException("The generic HTTP payment gateway returned an unsupported payment status.");
        }

        return new PaymentGatewayCreateResult(
            status,
            gatewayResponse.ProviderReference.Trim(),
            string.IsNullOrWhiteSpace(gatewayResponse.CheckoutUrl) ? null : gatewayResponse.CheckoutUrl.Trim(),
            string.IsNullOrWhiteSpace(gatewayResponse.FailureReason) ? null : gatewayResponse.FailureReason.Trim());
    }

    private bool IsEndpointConfigured()
    {
        return Uri.TryCreate(_options.EndpointUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record GenericHttpPaymentRequest(
        int BookingId,
        string BookingReference,
        decimal Amount,
        string Currency,
        string MethodType,
        string IdempotencyKey);

    private sealed record GenericHttpPaymentResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("providerReference")] string ProviderReference,
        [property: JsonPropertyName("checkoutUrl")] string? CheckoutUrl,
        [property: JsonPropertyName("failureReason")] string? FailureReason);
}
