using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HouseManagement.Api.Infrastructure.Payments;
using HouseManagement.Api.Models;
using Microsoft.Extensions.Options;
using Xunit;

namespace HouseManagement.Api.Tests;

public class PesapalPaymentGatewayTests
{
    private static readonly PesapalPaymentGatewayOptions ValidOptions = new()
    {
        Enabled = true,
        BaseUrl = "https://cybqa.pesapal.com/pesapalv3",
        ConsumerKey = "sandbox-consumer-key",
        ConsumerSecret = "sandbox-consumer-secret",
        CallbackUrl = "https://api.example/payments/pesapal/callback",
        IpnId = "ipn-123",
        SupportedCurrencies = ["UGX"],
        SupportedMethods = [PaymentMethodType.Card, PaymentMethodType.MobileMoney]
    };

    [Fact]
    public void Supports_RequiresEnabledConfiguredCredentialsMethodAndCurrency()
    {
        var gateway = CreateGateway(ValidOptions);
        Assert.True(gateway.Supports(PaymentMethodType.Card, "ugx"));
        Assert.True(gateway.Supports(PaymentMethodType.MobileMoney, "UGX"));
        Assert.False(gateway.Supports(PaymentMethodType.Card, "KES"));

        Assert.False(CreateGateway(Clone(ValidOptions, o => o.Enabled = false)).Supports(PaymentMethodType.Card, "UGX"));
        Assert.False(CreateGateway(Clone(ValidOptions, o => o.BaseUrl = "not-a-url")).Supports(PaymentMethodType.Card, "UGX"));
        Assert.False(CreateGateway(Clone(ValidOptions, o => o.ConsumerKey = null)).Supports(PaymentMethodType.Card, "UGX"));
        Assert.False(CreateGateway(Clone(ValidOptions, o => o.ConsumerSecret = null)).Supports(PaymentMethodType.Card, "UGX"));
        Assert.False(CreateGateway(Clone(ValidOptions, o => o.CallbackUrl = null)).Supports(PaymentMethodType.Card, "UGX"));
        Assert.False(CreateGateway(Clone(ValidOptions, o => o.IpnId = null)).Supports(PaymentMethodType.Card, "UGX"));
    }

    [Fact]
    public async Task CreatePaymentAsync_RequestsTokenThenSubmitsOrderAndMapsResponse()
    {
        var capturedRequests = new List<(string Path, string? Body, HttpRequestMessage Request)>();
        var handler = new RoutingStubHandler(async request =>
        {
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync();
            capturedRequests.Add((request.RequestUri!.AbsolutePath, body, request));

            if (request.RequestUri!.AbsolutePath.EndsWith("RequestToken", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        token = "sandbox-token",
                        expiryDate = DateTimeOffset.UtcNow.AddMinutes(5),
                        status = "200",
                        message = "Request processed successfully"
                    })
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    order_tracking_id = "order-tracking-abc",
                    merchant_reference = "payment-key-123",
                    redirect_url = "https://cybqa.pesapal.com/pesapalv3/checkout/order-tracking-abc",
                    error = (object?)null,
                    status = "200"
                })
            };
        });

        var gateway = CreateGateway(ValidOptions, handler);
        var result = await gateway.CreatePaymentAsync(new PaymentGatewayCreateRequest(
            10,
            "BK-SANDBOX",
            25000m,
            "UGX",
            PaymentMethodType.MobileMoney,
            "payment-key-123",
            "Jane Doe",
            "jane@example.com",
            "+256700000000"));

        Assert.Equal(PaymentStatus.Pending, result.Status);
        Assert.Equal("order-tracking-abc", result.ProviderReference);
        Assert.Equal("https://cybqa.pesapal.com/pesapalv3/checkout/order-tracking-abc", result.CheckoutUrl);
        Assert.Null(result.FailureReason);
        Assert.Equal("pesapal", gateway.ProviderName);

        Assert.Equal(2, capturedRequests.Count);
        var tokenCall = capturedRequests[0];
        Assert.EndsWith("RequestToken", tokenCall.Path);
        using (var tokenBody = JsonDocument.Parse(tokenCall.Body!))
        {
            Assert.Equal("sandbox-consumer-key", tokenBody.RootElement.GetProperty("consumer_key").GetString());
            Assert.Equal("sandbox-consumer-secret", tokenBody.RootElement.GetProperty("consumer_secret").GetString());
        }

        var orderCall = capturedRequests[1];
        Assert.EndsWith("SubmitOrderRequest", orderCall.Path);
        Assert.Equal("Bearer", orderCall.Request.Headers.Authorization!.Scheme);
        Assert.Equal("sandbox-token", orderCall.Request.Headers.Authorization.Parameter);
        using var orderBody = JsonDocument.Parse(orderCall.Body!);
        var root = orderBody.RootElement;
        Assert.Equal("payment-key-123", root.GetProperty("id").GetString());
        Assert.Equal("UGX", root.GetProperty("currency").GetString());
        Assert.Equal(25000m, root.GetProperty("amount").GetDecimal());
        Assert.Equal("https://api.example/payments/pesapal/callback", root.GetProperty("callback_url").GetString());
        Assert.Equal("ipn-123", root.GetProperty("notification_id").GetString());
        var billing = root.GetProperty("billing_address");
        Assert.Equal("jane@example.com", billing.GetProperty("email_address").GetString());
        Assert.Equal("+256700000000", billing.GetProperty("phone_number").GetString());
        Assert.Equal("Jane", billing.GetProperty("first_name").GetString());
        Assert.Equal("Doe", billing.GetProperty("last_name").GetString());
        Assert.Equal("UG", billing.GetProperty("country_code").GetString());
    }

    [Fact]
    public async Task CreatePaymentAsync_ReusesTheCachedTokenAcrossMultipleCalls()
    {
        var tokenRequests = 0;
        var handler = new RoutingStubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("RequestToken", StringComparison.Ordinal))
            {
                tokenRequests++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        token = "sandbox-token",
                        expiryDate = DateTimeOffset.UtcNow.AddMinutes(5)
                    })
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    order_tracking_id = "order-tracking-abc",
                    redirect_url = "https://cybqa.pesapal.com/pesapalv3/checkout/order-tracking-abc"
                })
            });
        });

        var tokenCache = new PesapalTokenCache();
        var gateway = CreateGateway(ValidOptions, handler, tokenCache);
        var request = new PaymentGatewayCreateRequest(10, "BK-SANDBOX", 25000m, "UGX", PaymentMethodType.Card, "payment-key-1");
        var secondRequest = request with { IdempotencyKey = "payment-key-2" };

        await gateway.CreatePaymentAsync(request);
        await gateway.CreatePaymentAsync(secondRequest);

        Assert.Equal(1, tokenRequests);
    }

    [Fact]
    public async Task CreatePaymentAsync_RejectsAnIncompleteOrderResponse()
    {
        var handler = CreateTokenThenHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                error = new { message = "Invalid currency" },
                status = "500"
            })
        }));

        var gateway = CreateGateway(ValidOptions, handler);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.CreatePaymentAsync(
            new PaymentGatewayCreateRequest(10, "BK-SANDBOX", 25000m, "UGX", PaymentMethodType.Card, "payment-key-123")));

        Assert.Contains("Invalid currency", exception.Message);
    }

    [Fact]
    public async Task CreatePaymentAsync_RejectsWhenTokenRequestFails()
    {
        var handler = new RoutingStubHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        var gateway = CreateGateway(ValidOptions, handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.CreatePaymentAsync(
            new PaymentGatewayCreateRequest(10, "BK-SANDBOX", 25000m, "UGX", PaymentMethodType.Card, "payment-key-123")));
    }

    [Fact]
    public async Task GetStatusAsync_RequestsTokenThenMapsCompletedStatus()
    {
        var handler = CreateTokenThenHandler(request =>
        {
            Assert.EndsWith("GetTransactionStatus", request.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            Assert.Contains("orderTrackingId=order-tracking-abc", request.RequestUri!.Query, StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    payment_status_description = "COMPLETED",
                    message = "Request processed successfully",
                    merchant_reference = "payment-key-123",
                    error = (object?)null
                })
            });
        });

        var gateway = CreateGateway(ValidOptions, handler);
        var result = await gateway.GetStatusAsync("order-tracking-abc");

        Assert.Equal(PaymentStatus.Succeeded, result.Status);
        Assert.Null(result.FailureReason);
    }

    [Theory]
    [InlineData("FAILED", PaymentStatus.Failed)]
    [InlineData("INVALID", PaymentStatus.Failed)]
    [InlineData("REVERSED", PaymentStatus.Refunded)]
    [InlineData("PENDING", PaymentStatus.Processing)]
    [InlineData(null, PaymentStatus.Processing)]
    public async Task GetStatusAsync_MapsAllDocumentedStatusDescriptions(string? description, PaymentStatus expected)
    {
        var handler = CreateTokenThenHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                payment_status_description = description,
                message = "Some message",
                error = (object?)null
            })
        }));

        var gateway = CreateGateway(ValidOptions, handler);
        var result = await gateway.GetStatusAsync("order-tracking-abc");

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsFailedWhenProviderReportsAnError()
    {
        var handler = CreateTokenThenHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                error = new { message = "Transaction not found" }
            })
        }));

        var gateway = CreateGateway(ValidOptions, handler);
        var result = await gateway.GetStatusAsync("unknown-tracking-id");

        Assert.Equal(PaymentStatus.Failed, result.Status);
        Assert.Equal("Transaction not found", result.FailureReason);
    }

    [Fact]
    public async Task GetStatusAsync_RejectsWhenHttpCallFails()
    {
        var handler = CreateTokenThenHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var gateway = CreateGateway(ValidOptions, handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.GetStatusAsync("order-tracking-abc"));
    }

    private static RoutingStubHandler CreateTokenThenHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> orderResponder)
    {
        return new RoutingStubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("RequestToken", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { token = "sandbox-token", expiryDate = DateTimeOffset.UtcNow.AddMinutes(5) })
                });
            }

            return orderResponder(request);
        });
    }

    private static PesapalPaymentGatewayOptions Clone(
        PesapalPaymentGatewayOptions source,
        Action<PesapalPaymentGatewayOptions> mutate)
    {
        var clone = new PesapalPaymentGatewayOptions
        {
            Enabled = source.Enabled,
            ProviderName = source.ProviderName,
            BaseUrl = source.BaseUrl,
            ConsumerKey = source.ConsumerKey,
            ConsumerSecret = source.ConsumerSecret,
            CallbackUrl = source.CallbackUrl,
            IpnId = source.IpnId,
            TimeoutSeconds = source.TimeoutSeconds,
            SupportedCurrencies = source.SupportedCurrencies,
            SupportedMethods = source.SupportedMethods
        };
        mutate(clone);
        return clone;
    }

    private static PesapalPaymentGateway CreateGateway(
        PesapalPaymentGatewayOptions options,
        HttpMessageHandler? handler = null,
        PesapalTokenCache? tokenCache = null)
    {
        return new PesapalPaymentGateway(
            new HttpClient(handler ?? new RoutingStubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))))
            {
                Timeout = TimeSpan.FromSeconds(30)
            },
            Options.Create(options),
            tokenCache ?? new PesapalTokenCache());
    }

    private sealed class RoutingStubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _sendAsync;

        public RoutingStubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync)
        {
            _sendAsync = sendAsync;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _sendAsync(request);
        }
    }
}
