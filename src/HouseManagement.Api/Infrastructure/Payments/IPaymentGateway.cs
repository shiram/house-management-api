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
    string IdempotencyKey);

public sealed record PaymentGatewayCreateResult(
    PaymentStatus Status,
    string ProviderReference,
    string? CheckoutUrl,
    string? FailureReason);
