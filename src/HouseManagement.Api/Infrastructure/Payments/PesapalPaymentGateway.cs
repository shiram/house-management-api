using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HouseManagement.Api.Models;
using Microsoft.Extensions.Options;

namespace HouseManagement.Api.Infrastructure.Payments;

// Pesapal API 3.0 adapter (T401 decision, T402 implementation). Implements the order-submission
// half of the flow documented in docs/PAYMENT-PROVIDER-DECISION.md: request a bearer token,
// submit an order for the booking, and return Pesapal's hosted checkout redirect URL. The
// resulting payment starts as Pending; IPN callback handling and the authoritative
// GetTransactionStatus reconciliation call are implemented separately (T403), since Pesapal's own
// integration guidance treats the callback as a prompt to check status rather than proof of
// payment on its own.
public sealed class PesapalPaymentGateway : IPaymentGateway
{
    private readonly HttpClient _httpClient;
    private readonly PesapalPaymentGatewayOptions _options;
    private readonly PesapalTokenCache _tokenCache;

    public PesapalPaymentGateway(
        HttpClient httpClient,
        IOptions<PesapalPaymentGatewayOptions> options,
        PesapalTokenCache tokenCache)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _tokenCache = tokenCache;
    }

    public string ProviderName => string.IsNullOrWhiteSpace(_options.ProviderName)
        ? "pesapal"
        : _options.ProviderName.Trim();

    public bool Supports(PaymentMethodType methodType, string currency)
    {
        return _options.Enabled &&
            IsBaseUrlConfigured() &&
            !string.IsNullOrWhiteSpace(_options.ConsumerKey) &&
            !string.IsNullOrWhiteSpace(_options.ConsumerSecret) &&
            !string.IsNullOrWhiteSpace(_options.CallbackUrl) &&
            !string.IsNullOrWhiteSpace(_options.IpnId) &&
            _options.SupportedMethods.Contains(methodType) &&
            _options.SupportedCurrencies.Any(item => string.Equals(item, currency, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<PaymentGatewayCreateResult> CreatePaymentAsync(
        PaymentGatewayCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(request.MethodType, request.Currency))
        {
            throw new InvalidOperationException("The Pesapal payment gateway is not configured for this payment request.");
        }

        var token = await _tokenCache.GetTokenAsync(RequestTokenAsync, cancellationToken);

        var baseUrl = _options.BaseUrl!.TrimEnd('/');
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/Transactions/SubmitOrderRequest")
        {
            Content = JsonContent.Create(new PesapalOrderRequest(
                // Pesapal requires a unique merchant reference per order attempt; the payment's
                // idempotency key already serves that purpose for this platform.
                request.IdempotencyKey,
                request.Currency,
                request.Amount,
                BuildDescription(request.BookingReference),
                _options.CallbackUrl!,
                _options.IpnId!,
                new PesapalBillingAddress(
                    string.IsNullOrWhiteSpace(request.CustomerEmail) ? null : request.CustomerEmail.Trim(),
                    string.IsNullOrWhiteSpace(request.CustomerPhone) ? null : request.CustomerPhone.Trim(),
                    SplitFirstName(request.CustomerName),
                    SplitLastName(request.CustomerName),
                    // This platform operates in Uganda only; Pesapal requires an ISO 3166-1
                    // alpha-2 country code on the billing address.
                    "UG")))
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"The Pesapal payment gateway returned HTTP {(int)response.StatusCode}.");
        }

        var orderResponse = await response.Content.ReadFromJsonAsync<PesapalOrderResponse>(
            cancellationToken: cancellationToken);
        if (orderResponse == null ||
            string.IsNullOrWhiteSpace(orderResponse.OrderTrackingId) ||
            string.IsNullOrWhiteSpace(orderResponse.RedirectUrl))
        {
            var reason = orderResponse?.Error?.Message;
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(reason)
                ? "The Pesapal payment gateway returned an invalid response."
                : $"The Pesapal payment gateway rejected the order: {reason}");
        }

        // Pesapal does not return a final payment status from order submission alone; the
        // transaction starts Pending until the IPN callback/status query (T403) resolves it.
        return new PaymentGatewayCreateResult(
            PaymentStatus.Pending,
            orderResponse.OrderTrackingId.Trim(),
            orderResponse.RedirectUrl.Trim(),
            null);
    }

    // Invoked by the shared PesapalTokenCache only when its cached token is missing or near
    // expiry, so a RequestToken HTTP call happens at most once per refresh across the whole
    // application rather than once per payment.
    private async Task<(string Token, DateTimeOffset? ExpiresAt)> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var baseUrl = _options.BaseUrl!.TrimEnd('/');
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/Auth/RequestToken")
        {
            Content = JsonContent.Create(new PesapalTokenRequest(_options.ConsumerKey!, _options.ConsumerSecret!))
        };
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"The Pesapal payment gateway returned HTTP {(int)response.StatusCode} while requesting a token.");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<PesapalTokenResponse>(
            cancellationToken: cancellationToken);
        if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.Token))
        {
            var reason = tokenResponse?.Message;
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(reason)
                ? "The Pesapal payment gateway did not return an authentication token."
                : $"The Pesapal payment gateway rejected authentication: {reason}");
        }

        return (tokenResponse.Token.Trim(), tokenResponse.ExpiryDate);
    }

    private bool IsBaseUrlConfigured()
    {
        return Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase));
    }

    // Pesapal limits the order description length; keep this well within its documented bound.
    private static string BuildDescription(string bookingReference)
    {
        var description = $"House Management booking {bookingReference}";
        return description.Length > 100 ? description[..100] : description;
    }

    private static string? SplitFirstName(string? customerName)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            return null;
        }

        var parts = customerName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : null;
    }

    private static string? SplitLastName(string? customerName)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            return null;
        }

        var parts = customerName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[1] : null;
    }

    private sealed record PesapalTokenRequest(
        [property: JsonPropertyName("consumer_key")] string ConsumerKey,
        [property: JsonPropertyName("consumer_secret")] string ConsumerSecret);

    private sealed record PesapalTokenResponse(
        [property: JsonPropertyName("token")] string? Token,
        [property: JsonPropertyName("expiryDate")] DateTimeOffset? ExpiryDate,
        [property: JsonPropertyName("message")] string? Message);

    private sealed record PesapalOrderRequest(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("callback_url")] string CallbackUrl,
        [property: JsonPropertyName("notification_id")] string NotificationId,
        [property: JsonPropertyName("billing_address")] PesapalBillingAddress BillingAddress);

    private sealed record PesapalBillingAddress(
        [property: JsonPropertyName("email_address")] string? EmailAddress,
        [property: JsonPropertyName("phone_number")] string? PhoneNumber,
        [property: JsonPropertyName("first_name")] string? FirstName,
        [property: JsonPropertyName("last_name")] string? LastName,
        [property: JsonPropertyName("country_code")] string CountryCode);

    private sealed record PesapalOrderResponse(
        [property: JsonPropertyName("order_tracking_id")] string? OrderTrackingId,
        [property: JsonPropertyName("merchant_reference")] string? MerchantReference,
        [property: JsonPropertyName("redirect_url")] string? RedirectUrl,
        [property: JsonPropertyName("error")] PesapalOrderError? Error);

    private sealed record PesapalOrderError(
        [property: JsonPropertyName("message")] string? Message);
}
