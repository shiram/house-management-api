using HouseManagement.Api.Data;
using HouseManagement.Api.Infrastructure.Payments;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HouseManagement.Api.Tests;

public class PaymentReconciliationServiceTests
{
    [Fact]
    public async Task ReconcileAsync_UpdatesPaymentWhenProviderReportsSucceeded()
    {
        await using var context = CreateContext();
        var booking = AddBooking(context, 1, "BK-RECONCILE-1");
        var payment = AddPayment(context, booking.Id, "fake-provider", "order-1", PaymentStatus.Pending);
        await context.SaveChangesAsync();

        var gateway = new FakeStatusQueryGateway("fake-provider", new PaymentGatewayStatusResult(PaymentStatus.Succeeded, null));
        var service = CreateService(context, gateway);

        var result = await service.ReconcileAsync("fake-provider", "order-1");

        Assert.Equal(PaymentReconciliationOutcome.Reconciled, result.Outcome);
        var updated = await context.Payments.SingleAsync(item => item.Id == payment.Id);
        Assert.Equal(PaymentStatus.Succeeded, updated.Status);
        Assert.NotNull(updated.CompletedAt);
        Assert.NotNull(updated.UpdatedAt);
        Assert.Equal(1, await context.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task ReconcileAsync_UpdatesPaymentWhenProviderReportsFailed()
    {
        await using var context = CreateContext();
        var booking = AddBooking(context, 1, "BK-RECONCILE-2");
        var payment = AddPayment(context, booking.Id, "fake-provider", "order-2", PaymentStatus.Processing);
        await context.SaveChangesAsync();

        var gateway = new FakeStatusQueryGateway(
            "fake-provider",
            new PaymentGatewayStatusResult(PaymentStatus.Failed, "Insufficient funds"));
        var service = CreateService(context, gateway);

        var result = await service.ReconcileAsync("fake-provider", "order-2");

        Assert.Equal(PaymentReconciliationOutcome.Reconciled, result.Outcome);
        var updated = await context.Payments.SingleAsync(item => item.Id == payment.Id);
        Assert.Equal(PaymentStatus.Failed, updated.Status);
        Assert.Equal("Insufficient funds", updated.FailureReason);
        Assert.Null(updated.CompletedAt);
    }

    [Fact]
    public async Task ReconcileAsync_IsIdempotentForAnAlreadyTerminalPayment()
    {
        await using var context = CreateContext();
        var booking = AddBooking(context, 1, "BK-RECONCILE-3");
        AddPayment(context, booking.Id, "fake-provider", "order-3", PaymentStatus.Succeeded);
        await context.SaveChangesAsync();

        var gateway = new FakeStatusQueryGateway("fake-provider", new PaymentGatewayStatusResult(PaymentStatus.Failed, "should not be applied"));
        var service = CreateService(context, gateway);

        var result = await service.ReconcileAsync("fake-provider", "order-3");

        Assert.Equal(PaymentReconciliationOutcome.AlreadySettled, result.Outcome);
        Assert.False(gateway.WasCalled);
        var unchanged = await context.Payments.SingleAsync(item => item.ProviderReference == "order-3");
        Assert.Equal(PaymentStatus.Succeeded, unchanged.Status);
        Assert.Equal(0, await context.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task ReconcileAsync_ReturnsPaymentNotFoundForAnUnknownReference()
    {
        await using var context = CreateContext();
        var gateway = new FakeStatusQueryGateway("fake-provider", new PaymentGatewayStatusResult(PaymentStatus.Succeeded, null));
        var service = CreateService(context, gateway);

        var result = await service.ReconcileAsync("fake-provider", "does-not-exist");

        Assert.Equal(PaymentReconciliationOutcome.PaymentNotFound, result.Outcome);
        Assert.False(gateway.WasCalled);
    }

    [Fact]
    public async Task ReconcileAsync_ReturnsProviderNotSupportedWhenNoGatewayMatches()
    {
        await using var context = CreateContext();
        var booking = AddBooking(context, 1, "BK-RECONCILE-4");
        AddPayment(context, booking.Id, "unregistered-provider", "order-4", PaymentStatus.Pending);
        await context.SaveChangesAsync();

        var gateway = new FakeStatusQueryGateway("fake-provider", new PaymentGatewayStatusResult(PaymentStatus.Succeeded, null));
        var service = CreateService(context, gateway);

        var result = await service.ReconcileAsync("unregistered-provider", "order-4");

        Assert.Equal(PaymentReconciliationOutcome.ProviderNotSupported, result.Outcome);
    }

    private static HouseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HouseContext(options);
    }

    private static Booking AddBooking(HouseContext context, int id, string reference)
    {
        var booking = new Booking
        {
            Id = id,
            Reference = reference,
            TotalPrice = 50000m,
            Status = BookingStatus.Confirmed,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(2)
        };
        context.Bookings.Add(booking);
        return booking;
    }

    private static Payment AddPayment(
        HouseContext context,
        int bookingId,
        string providerName,
        string providerReference,
        PaymentStatus status)
    {
        var payment = new Payment
        {
            BookingId = bookingId,
            Amount = 50000m,
            Currency = "UGX",
            MethodType = PaymentMethodType.MobileMoney,
            Status = status,
            ProviderName = providerName,
            ProviderReference = providerReference,
            IdempotencyKey = $"idempotency-{providerReference}"
        };
        context.Payments.Add(payment);
        return payment;
    }

    private static PaymentReconciliationService CreateService(HouseContext context, IPaymentGateway gateway)
    {
        return new PaymentReconciliationService(
            context,
            [gateway],
            new AuditLogService(context),
            NullLogger<PaymentReconciliationService>.Instance);
    }

    private sealed class FakeStatusQueryGateway : IPaymentGateway, IPaymentStatusQuery
    {
        private readonly PaymentGatewayStatusResult _result;

        public FakeStatusQueryGateway(string providerName, PaymentGatewayStatusResult result)
        {
            ProviderName = providerName;
            _result = result;
        }

        public bool WasCalled { get; private set; }

        public string ProviderName { get; }

        public bool Supports(PaymentMethodType methodType, string currency) => true;

        public Task<PaymentGatewayCreateResult> CreatePaymentAsync(
            PaymentGatewayCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<PaymentGatewayStatusResult> GetStatusAsync(
            string providerReference,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(_result);
        }
    }
}
