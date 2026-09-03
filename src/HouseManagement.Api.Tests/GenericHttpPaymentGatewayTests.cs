using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HouseManagement.Api.Infrastructure.Payments;
using HouseManagement.Api.Models;
using Microsoft.Extensions.Options;
using Xunit;

namespace HouseManagement.Api.Tests;

public class GenericHttpPaymentGatewayTests
{
    [Fact]
    public void Supports_RequiresEnabledAbsoluteEndpointMethodAndCurrency()
    {
        var gateway = CreateGateway(new GenericHttpPaymentGatewayOptions
        {
            Enabled = true,
            EndpointUrl = "https://payments.example/initiate",
            SupportedCurrencies = ["UGX"],
            SupportedMethods = [PaymentMethodType.Card]
        });

        Assert.True(gateway.Supports(PaymentMethodType.Card, "ugx"));
        Assert.False(gateway.Supports(PaymentMethodType.MobileMoney, "UGX"));
        Assert.False(gateway.Supports(PaymentMethodType.Card, "KES"));

        var disabled = CreateGateway(new GenericHttpPaymentGatewayOptions
        {
            Enabled = false,
            EndpointUrl = "https://payments.example/initiate"
        });
        Assert.False(disabled.Supports(PaymentMethodType.Card, "UGX"));
    }

    [Fact]
    public async Task CreatePaymentAsync_PostsSandboxPaymentRequestAndMapsResponse()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    status = "Processing",
                    providerReference = "sandbox-reference",
                    checkoutUrl = "https://payments.example/checkout/sandbox-reference",
                    failureReason = (string?)null
                })
            };
        });
        var gateway = CreateGateway(
            new GenericHttpPaymentGatewayOptions
            {
                Enabled = true,
                ProviderName = "sandbox-gateway",
                EndpointUrl = "https://payments.example/initiate",
                ApiKey = "sandbox-key",
                SupportedCurrencies = ["UGX"],
                SupportedMethods = [PaymentMethodType.Card, PaymentMethodType.MobileMoney]
            },
            handler);

        var result = await gateway.CreatePaymentAsync(new PaymentGatewayCreateRequest(
            10,
            "BK-SANDBOX",
            25000m,
            "UGX",
            PaymentMethodType.MobileMoney,
            "payment-key-123"));

        Assert.Equal(PaymentStatus.Processing, result.Status);
        Assert.Equal("sandbox-reference", result.ProviderReference);
        Assert.Equal("https://payments.example/checkout/sandbox-reference", result.CheckoutUrl);
        Assert.Equal("sandbox-gateway", gateway.ProviderName);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://payments.example/initiate", capturedRequest.RequestUri!.ToString());
        Assert.Equal("payment-key-123", capturedRequest.Headers.GetValues("X-Idempotency-Key").Single());
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization!.Scheme);
        Assert.Equal("sandbox-key", capturedRequest.Headers.Authorization.Parameter);

        using var document = JsonDocument.Parse(capturedBody!);
        var root = document.RootElement;
        Assert.Equal(10, root.GetProperty("bookingId").GetInt32());
        Assert.Equal("BK-SANDBOX", root.GetProperty("bookingReference").GetString());
        Assert.Equal(25000m, root.GetProperty("amount").GetDecimal());
        Assert.Equal("UGX", root.GetProperty("currency").GetString());
        Assert.Equal("MobileMoney", root.GetProperty("methodType").GetString());
        Assert.Equal("payment-key-123", root.GetProperty("idempotencyKey").GetString());
    }

    [Fact]
    public async Task CreatePaymentAsync_RejectsInvalidProviderResponse()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                status = "Unknown",
                providerReference = "sandbox-reference"
            })
        }));
        var gateway = CreateGateway(
            new GenericHttpPaymentGatewayOptions
            {
                Enabled = true,
                EndpointUrl = "https://payments.example/initiate",
                SupportedCurrencies = ["UGX"],
                SupportedMethods = [PaymentMethodType.Card]
            },
            handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.CreatePaymentAsync(new PaymentGatewayCreateRequest(
            10,
            "BK-SANDBOX",
            25000m,
            "UGX",
            PaymentMethodType.Card,
            "payment-key-123")));
    }

    private static GenericHttpPaymentGateway CreateGateway(
        GenericHttpPaymentGatewayOptions options,
        HttpMessageHandler? handler = null)
    {
        return new GenericHttpPaymentGateway(
            new HttpClient(handler ?? new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))))
            {
                Timeout = TimeSpan.FromSeconds(30)
            },
            Options.Create(options));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _sendAsync;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync)
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
