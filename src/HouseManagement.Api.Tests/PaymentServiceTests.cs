using HouseManagement.Api.Data;
using HouseManagement.Api.Infrastructure.Payments;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HouseManagement.Api.Tests;

public class PaymentServiceTests
{
    [Fact]
    public async Task InitiateAsync_CreatesProviderBackedPaymentRecord()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Bookings.Add(new Booking
        {
            Id = 1,
            Reference = "BK-PAYMENT",
            TotalPrice = 50000m,
            Status = BookingStatus.Confirmed,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(2)
        });
        await context.SaveChangesAsync();

        var gateway = new FakePaymentGateway(PaymentMethodType.MobileMoney, "UGX");
        var service = new PaymentService(context, [gateway]);

        var result = await service.InitiateAsync(new PaymentInitiationRequest(
            1,
            PaymentMethodType.MobileMoney,
            "ugx",
            "booking-1-mobile-money"));

        Assert.NotNull(result.Payment);
        Assert.Null(result.Error);
        Assert.Equal(50000m, result.Payment!.Amount);
        Assert.Equal("UGX", result.Payment.Currency);
        Assert.Equal(PaymentMethodType.MobileMoney, result.Payment.MethodType);
        Assert.Equal(PaymentStatus.Processing, result.Payment.Status);
        Assert.Equal("fake-provider", result.Payment.ProviderName);
        Assert.Equal("fake-reference", result.Payment.ProviderReference);
        Assert.Equal("https://payments.example/checkout/fake-reference", result.Payment.ProviderCheckoutUrl);
        Assert.Equal(1, await context.Payments.CountAsync());
    }

    [Fact]
    public async Task InitiateAsync_ReturnsExistingPaymentForSameIdempotencyKey()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Bookings.Add(new Booking
        {
            Id = 1,
            Reference = "BK-IDEMPOTENT",
            TotalPrice = 75000m,
            Status = BookingStatus.Confirmed,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(2)
        });
        await context.SaveChangesAsync();

        var service = new PaymentService(context, [new FakePaymentGateway(PaymentMethodType.Card, "UGX")]);

        var first = await service.InitiateAsync(new PaymentInitiationRequest(1, PaymentMethodType.Card, "UGX", "same-payment-key"));
        var second = await service.InitiateAsync(new PaymentInitiationRequest(1, PaymentMethodType.Card, "UGX", "same-payment-key"));

        Assert.NotNull(first.Payment);
        Assert.NotNull(second.Payment);
        Assert.Equal(first.Payment!.Id, second.Payment!.Id);
        Assert.Equal(1, await context.Payments.CountAsync());
    }

    [Fact]
    public async Task InitiateAsync_RejectsWhenNoProviderSupportsMethodAndCurrency()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Bookings.Add(new Booking
        {
            Id = 1,
            Reference = "BK-NO-PROVIDER",
            TotalPrice = 10000m,
            Status = BookingStatus.Confirmed,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(2)
        });
        await context.SaveChangesAsync();

        var service = new PaymentService(context, []);

        var result = await service.InitiateAsync(new PaymentInitiationRequest(1, PaymentMethodType.Card, "UGX", "missing-provider-key"));

        Assert.Null(result.Payment);
        Assert.Contains("No payment provider is configured", result.Error);
        Assert.Empty(await context.Payments.ToListAsync());
    }

    [Fact]
    public async Task InitiateAsync_RejectsDuplicateActivePaymentForBooking()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.Bookings.Add(new Booking
        {
            Id = 1,
            Reference = "BK-DUPLICATE",
            TotalPrice = 10000m,
            Status = BookingStatus.Confirmed,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(2)
        });
        context.Payments.Add(new Payment
        {
            BookingId = 1,
            Amount = 10000m,
            Currency = "UGX",
            MethodType = PaymentMethodType.Card,
            Status = PaymentStatus.Processing,
            ProviderName = "existing",
            ProviderReference = "existing-reference",
            IdempotencyKey = "existing-payment"
        });
        await context.SaveChangesAsync();

        var service = new PaymentService(context, [new FakePaymentGateway(PaymentMethodType.Card, "UGX")]);

        var result = await service.InitiateAsync(new PaymentInitiationRequest(1, PaymentMethodType.Card, "UGX", "new-payment-key"));

        Assert.Null(result.Payment);
        Assert.Contains("already has an active payment", result.Error);
        Assert.Equal(1, await context.Payments.CountAsync());
    }

    private sealed class FakePaymentGateway : IPaymentGateway
    {
        private readonly PaymentMethodType _methodType;
        private readonly string _currency;

        public FakePaymentGateway(PaymentMethodType methodType, string currency)
        {
            _methodType = methodType;
            _currency = currency;
        }

        public string ProviderName => "fake-provider";

        public bool Supports(PaymentMethodType methodType, string currency)
        {
            return methodType == _methodType && string.Equals(currency, _currency, StringComparison.OrdinalIgnoreCase);
        }

        public Task<PaymentGatewayCreateResult> CreatePaymentAsync(
            PaymentGatewayCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PaymentGatewayCreateResult(
                PaymentStatus.Processing,
                "fake-reference",
                "https://payments.example/checkout/fake-reference",
                null));
        }
    }
}
