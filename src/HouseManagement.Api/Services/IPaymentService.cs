using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IPaymentService
{
    Task<PaymentInitiationResult> InitiateAsync(
        PaymentInitiationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentInitiationRequest(
    int BookingId,
    PaymentMethodType MethodType,
    string Currency,
    string IdempotencyKey);

public sealed record PaymentInitiationResult(Payment? Payment, string? Error);
