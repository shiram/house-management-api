using HouseManagement.Api.Common;
using HouseManagement.Api.Data;
using HouseManagement.Api.Infrastructure.Payments;
using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Services;

// T403: reconciles a Payment's status against its provider. Designed to be invoked from a public
// webhook/callback endpoint that cannot be trusted on its own - see docs/PAYMENT-PROVIDER-DECISION.md
// for why Pesapal's IPN callback carries no verifiable signature. The callback is only ever used
// as a trigger; the actual status transition is always sourced from an authenticated,
// server-to-server status query made with this platform's own provider credentials.
public sealed class PaymentReconciliationService : IPaymentReconciliationService
{
    private static readonly PaymentStatus[] TerminalPaymentStatuses =
    [
        PaymentStatus.Succeeded,
        PaymentStatus.Failed,
        PaymentStatus.Cancelled,
        PaymentStatus.Refunded
    ];

    private readonly HouseContext _db;
    private readonly IEnumerable<IPaymentGateway> _gateways;
    private readonly IAuditLogService _auditLogs;
    private readonly ILogger<PaymentReconciliationService> _logger;

    public PaymentReconciliationService(
        HouseContext db,
        IEnumerable<IPaymentGateway> gateways,
        IAuditLogService auditLogs,
        ILogger<PaymentReconciliationService> logger)
    {
        _db = db;
        _gateways = gateways;
        _auditLogs = auditLogs;
        _logger = logger;
    }

    public async Task<PaymentReconciliationResult> ReconcileAsync(
        string providerName,
        string providerReference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(providerReference))
        {
            return new PaymentReconciliationResult(PaymentReconciliationOutcome.PaymentNotFound);
        }

        var normalizedProviderName = providerName.Trim();
        var normalizedProviderReference = providerReference.Trim();

        var payment = await _db.Payments.SingleOrDefaultAsync(
            item => item.ProviderName == normalizedProviderName &&
                item.ProviderReference == normalizedProviderReference,
            cancellationToken);
        if (payment == null)
        {
            _logger.LogWarning(
                "Payment reconciliation callback referenced an unknown payment for provider {ProviderName}.",
                normalizedProviderName);
            return new PaymentReconciliationResult(PaymentReconciliationOutcome.PaymentNotFound);
        }

        // Idempotency: a terminal payment is never revisited, so replayed or duplicate callbacks
        // (Pesapal explicitly documents it may send the same IPN more than once) are a safe no-op.
        if (TerminalPaymentStatuses.Contains(payment.Status))
        {
            return new PaymentReconciliationResult(PaymentReconciliationOutcome.AlreadySettled);
        }

        var gateway = _gateways.OfType<IPaymentStatusQuery>().FirstOrDefault(statusQuery =>
            statusQuery is IPaymentGateway typedGateway &&
            string.Equals(typedGateway.ProviderName, normalizedProviderName, StringComparison.OrdinalIgnoreCase));
        if (gateway == null)
        {
            _logger.LogWarning(
                "No payment gateway supporting status queries is registered for provider {ProviderName}.",
                normalizedProviderName);
            return new PaymentReconciliationResult(PaymentReconciliationOutcome.ProviderNotSupported);
        }

        var statusResult = await gateway.GetStatusAsync(normalizedProviderReference, cancellationToken);

        var previousStatus = payment.Status;
        payment.Status = statusResult.Status;
        payment.FailureReason = string.IsNullOrWhiteSpace(statusResult.FailureReason)
            ? payment.FailureReason
            : statusResult.FailureReason.Trim();
        payment.UpdatedAt = DateTimeOffset.UtcNow;
        if (statusResult.Status == PaymentStatus.Succeeded)
        {
            payment.CompletedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (previousStatus != payment.Status)
        {
            await _auditLogs.LogAsync(
                AuditEventTypes.PaymentStatusReconciled,
                nameof(Payment),
                entityId: payment.Id,
                userId: null,
                details: $"{previousStatus} -> {payment.Status} (provider: {normalizedProviderName})",
                cancellationToken: cancellationToken);
        }

        return new PaymentReconciliationResult(PaymentReconciliationOutcome.Reconciled);
    }
}
