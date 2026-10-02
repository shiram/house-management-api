namespace HouseManagement.Api.Services;

public interface IPaymentReconciliationService
{
    // Re-checks a payment's authoritative status with its provider and updates the stored
    // Payment row accordingly. Must never trust caller-supplied status claims (e.g. webhook/IPN
    // query parameters) - only the gateway's own status-query response is applied.
    Task<PaymentReconciliationResult> ReconcileAsync(
        string providerName,
        string providerReference,
        CancellationToken cancellationToken = default);
}

public enum PaymentReconciliationOutcome
{
    // The payment's status was (re-)confirmed/updated from the provider.
    Reconciled,

    // No Payment row matches the given provider/reference; nothing to do.
    PaymentNotFound,

    // The payment was already in a terminal status; the provider was not re-queried.
    AlreadySettled,

    // No registered gateway for this provider name supports status queries.
    ProviderNotSupported
}

public sealed record PaymentReconciliationResult(PaymentReconciliationOutcome Outcome);
