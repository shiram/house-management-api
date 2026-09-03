using HouseManagement.Api.Data;
using HouseManagement.Api.Infrastructure.Payments;
using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Services;

public sealed class PaymentService : IPaymentService
{
    private static readonly PaymentStatus[] ActivePaymentStatuses =
    [
        PaymentStatus.Pending,
        PaymentStatus.Processing,
        PaymentStatus.Succeeded
    ];

    private readonly HouseContext _db;
    private readonly IEnumerable<IPaymentGateway> _gateways;

    public PaymentService(HouseContext db, IEnumerable<IPaymentGateway> gateways)
    {
        _db = db;
        _gateways = gateways;
    }

    public async Task<PaymentInitiationResult> InitiateAsync(
        PaymentInitiationRequest request,
        CancellationToken cancellationToken = default)
    {
        var currency = NormalizeCurrency(request.Currency);
        if (currency == null)
        {
            return new PaymentInitiationResult(null, "A valid three-letter currency code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return new PaymentInitiationResult(null, "A valid idempotency key is required.");
        }

        var idempotencyKey = request.IdempotencyKey.Trim();
        if (idempotencyKey.Length is < 8 or > 128)
        {
            return new PaymentInitiationResult(null, "A valid idempotency key is required.");
        }

        var existingPayment = await _db.Payments
            .AsNoTracking()
            .SingleOrDefaultAsync(payment => payment.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existingPayment != null)
        {
            return new PaymentInitiationResult(existingPayment, null);
        }

        var booking = await _db.Bookings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.BookingId, cancellationToken);
        if (booking == null)
        {
            return new PaymentInitiationResult(null, "The requested booking was not found.");
        }

        if (booking.TotalPrice <= 0)
        {
            return new PaymentInitiationResult(null, "The booking does not require payment.");
        }

        var hasActivePayment = await _db.Payments.AnyAsync(payment =>
            payment.BookingId == request.BookingId &&
            ActivePaymentStatuses.Contains(payment.Status),
            cancellationToken);
        if (hasActivePayment)
        {
            return new PaymentInitiationResult(null, "The booking already has an active payment.");
        }

        var gateway = _gateways.FirstOrDefault(item => item.Supports(request.MethodType, currency));
        if (gateway == null)
        {
            return new PaymentInitiationResult(null, "No payment provider is configured for this payment method and currency.");
        }

        var providerResult = await gateway.CreatePaymentAsync(
            new PaymentGatewayCreateRequest(
                booking.Id,
                booking.Reference,
                booking.TotalPrice,
                currency,
                request.MethodType,
                idempotencyKey),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(providerResult.ProviderReference))
        {
            return new PaymentInitiationResult(null, "The payment provider did not return a payment reference.");
        }

        var payment = new Payment
        {
            BookingId = booking.Id,
            Amount = booking.TotalPrice,
            Currency = currency,
            MethodType = request.MethodType,
            Status = providerResult.Status,
            ProviderName = gateway.ProviderName.Trim(),
            ProviderReference = providerResult.ProviderReference.Trim(),
            ProviderCheckoutUrl = string.IsNullOrWhiteSpace(providerResult.CheckoutUrl)
                ? null
                : providerResult.CheckoutUrl.Trim(),
            FailureReason = string.IsNullOrWhiteSpace(providerResult.FailureReason)
                ? null
                : providerResult.FailureReason.Trim(),
            IdempotencyKey = idempotencyKey,
            CompletedAt = providerResult.Status == PaymentStatus.Succeeded ? DateTimeOffset.UtcNow : null,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);
        return new PaymentInitiationResult(payment, null);
    }

    private static string? NormalizeCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return null;
        }

        var normalized = currency.Trim().ToUpperInvariant();
        return normalized.Length == 3 && normalized.All(char.IsLetter) ? normalized : null;
    }
}
