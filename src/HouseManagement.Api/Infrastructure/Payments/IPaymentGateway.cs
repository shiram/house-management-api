using HouseManagement.Api.Models;

namespace HouseManagement.Api.Infrastructure.Payments;

public interface IPaymentGateway
{
    string ProviderName { get; }
    bool Supports(PaymentMethodType methodType, string currency);
    Task<PaymentGatewayCreateResult> CreatePaymentAsync(
        PaymentGatewayCreateRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentGatewayCreateRequest(
    int BookingId,
    string BookingReference,
    decimal Amount,
    string Currency,
    PaymentMethodType MethodType,
    string IdempotencyKey,
    // Optional customer contact details. The generic sandbox adapter ignores these, but
    // provider-specific adapters (e.g. Pesapal) require at least one contact field on the
    // billing/order payload. Sourced from the booking's Client record by PaymentService.
    string? CustomerName = null,
    string? CustomerEmail = null,
    string? CustomerPhone = null);

public sealed record PaymentGatewayCreateResult(
    PaymentStatus Status,
    string ProviderReference,
    string? CheckoutUrl,
    string? FailureReason);
