using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HouseManagement.Api.Data;
using HouseManagement.Api.Infrastructure.Payments;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseManagement.Api.Tests;

// T404: sandbox-flavored integration coverage for the Pesapal adapter end-to-end through the real
// HTTP pipeline (DI, options binding, rate limiting, controller routing) rather than calling
// services directly. The Pesapal sandbox itself is stood in for by a stub HTTP handler bound to
// the application's PesapalPaymentGateway HttpClient -- no real Pesapal sandbox credentials are
// used or required; the "ConsumerKey"/"ConsumerSecret" below are clearly-fake placeholders, never
// real secrets, matching the no-credentials-committed constraint for this task.
public class PesapalSandboxIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly StubPesapalHandler _pesapalHandler = new();

    public PesapalSandboxIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var dbDescriptor = services.SingleOrDefault(item => item.ServiceType == typeof(DbContextOptions<HouseContext>));
                if (dbDescriptor != null) services.Remove(dbDescriptor);
                services.AddDbContext<HouseContext>(options =>
                    options.UseInMemoryDatabase("pesapal_sandbox_integration_db"));

                services.PostConfigure<PesapalPaymentGatewayOptions>(options =>
                {
                    options.Enabled = true;
                    options.BaseUrl = "https://sandbox.pesapal.invalid/pesapalv3";
                    options.ConsumerKey = "sandbox-placeholder-consumer-key";
                    options.ConsumerSecret = "sandbox-placeholder-consumer-secret";
                    options.CallbackUrl = "https://api.example/api/payments/webhooks/pesapal";
                    options.IpnId = "ipn-sandbox-placeholder";
                });

                // Replaces the real outbound HttpClient with a stub simulating the Pesapal
                // sandbox for RequestToken/SubmitOrderRequest/GetTransactionStatus.
                services.AddHttpClient<PesapalPaymentGateway>()
                    .ConfigurePrimaryHttpMessageHandler(() => _pesapalHandler);
            });
        });
    }

    [Fact]
    public async Task PesapalCallback_ReconcilesAnInitiatedPaymentToSucceeded()
    {
        var client = _factory.CreateClient();
        var payment = await InitiatePaymentAsync(bookingReference: "BK-SANDBOX-1");

        _pesapalHandler.TransactionStatusDescription = "COMPLETED";
        var response = await client.GetAsync(
            $"/api/payments/webhooks/pesapal?OrderTrackingId={payment.ProviderReference}&OrderMerchantReference={payment.IdempotencyKey}&OrderNotificationType=IPNCHANGE");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var acknowledgementJson = await response.Content.ReadAsStringAsync();
        using var acknowledgementDocument = JsonDocument.Parse(acknowledgementJson);
        var acknowledgementRoot = acknowledgementDocument.RootElement;
        Assert.Equal(payment.ProviderReference, acknowledgementRoot.GetProperty("orderTrackingId").GetString());
        Assert.Equal(200, acknowledgementRoot.GetProperty("status").GetInt32());

        var updated = await GetPaymentAsync(payment.Id);
        Assert.Equal(PaymentStatus.Succeeded, updated.Status);
        Assert.NotNull(updated.CompletedAt);
    }

    [Fact]
    public async Task PesapalCallback_ReconcilesAnInitiatedPaymentToFailed()
    {
        var client = _factory.CreateClient();
        var payment = await InitiatePaymentAsync(bookingReference: "BK-SANDBOX-2");

        _pesapalHandler.TransactionStatusDescription = "FAILED";
        _pesapalHandler.TransactionStatusMessage = "Card declined";
        var response = await client.GetAsync(
            $"/api/payments/webhooks/pesapal?OrderTrackingId={payment.ProviderReference}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await GetPaymentAsync(payment.Id);
        Assert.Equal(PaymentStatus.Failed, updated.Status);
        Assert.Equal("Card declined", updated.FailureReason);
    }

    [Fact]
    public async Task PesapalCallback_IsIdempotentAndNeverRequeriesAnAlreadySettledPayment()
    {
        var client = _factory.CreateClient();
        var payment = await InitiatePaymentAsync(bookingReference: "BK-SANDBOX-3");

        _pesapalHandler.TransactionStatusDescription = "COMPLETED";
        var first = await client.GetAsync($"/api/payments/webhooks/pesapal?OrderTrackingId={payment.ProviderReference}");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var statusCallsAfterFirst = _pesapalHandler.TransactionStatusCalls;

        // Pesapal explicitly documents it may deliver the same IPN more than once; a replayed
        // callback against an already-terminal payment must not re-query the provider.
        var second = await client.GetAsync($"/api/payments/webhooks/pesapal?OrderTrackingId={payment.ProviderReference}");
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(statusCallsAfterFirst, _pesapalHandler.TransactionStatusCalls);
    }

    [Fact]
    public async Task PesapalCallback_AcknowledgesAnUnknownOrderTrackingIdWithoutError()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/payments/webhooks/pesapal?OrderTrackingId=unknown-tracking-id");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, _pesapalHandler.TransactionStatusCalls);
    }

    [Fact]
    public async Task PesapalCallback_RejectsAMissingOrderTrackingId()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/payments/webhooks/pesapal");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<Payment> InitiatePaymentAsync(string bookingReference)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HouseContext>();

        var booking = new Booking
        {
            Reference = bookingReference,
            TotalPrice = 42000m,
            Status = BookingStatus.Confirmed,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            Client = new Client { Name = "Sandbox Client", Phone = "+256700000000", Email = "sandbox@example.com" }
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var result = await paymentService.InitiateAsync(new PaymentInitiationRequest(
            booking.Id,
            PaymentMethodType.Card,
            "UGX",
            $"idempotency-{bookingReference}"));

        Assert.Null(result.Error);
        Assert.NotNull(result.Payment);
        Assert.Equal("pesapal", result.Payment!.ProviderName);
        return result.Payment;
    }

    private async Task<Payment> GetPaymentAsync(int paymentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
        return await db.Payments.AsNoTracking().SingleAsync(item => item.Id == paymentId);
    }

    // Stands in for the real Pesapal sandbox across all three endpoints this platform calls.
    private sealed class StubPesapalHandler : HttpMessageHandler
    {
        public string TransactionStatusDescription { get; set; } = "PENDING";
        public string? TransactionStatusMessage { get; set; }
        public int TransactionStatusCalls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("RequestToken", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        token = "sandbox-stub-token",
                        expiryDate = DateTimeOffset.UtcNow.AddMinutes(5)
                    })
                });
            }

            if (path.EndsWith("SubmitOrderRequest", StringComparison.Ordinal))
            {
                // A GUID (rather than an incrementing counter) keeps the tracking id unique across
                // every test in this suite even though they share one WebApplicationFactory-backed
                // in-memory database and ProviderReference is uniquely constrained.
                var orderTrackingId = $"sandbox-order-{Guid.NewGuid():N}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        order_tracking_id = orderTrackingId,
                        redirect_url = $"https://sandbox.pesapal.invalid/checkout/{orderTrackingId}",
                        error = (object?)null
                    })
                });
            }

            if (path.EndsWith("GetTransactionStatus", StringComparison.Ordinal))
            {
                TransactionStatusCalls++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        payment_status_description = TransactionStatusDescription,
                        message = TransactionStatusMessage,
                        error = (object?)null
                    })
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
