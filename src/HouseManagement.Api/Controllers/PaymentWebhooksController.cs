using HouseManagement.Api.Common;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HouseManagement.Api.Controllers;

// T403: receives provider payment callbacks/IPNs. Anonymous by design - providers call this
// directly from their own servers with no user session - but every callback is treated purely as
// a prompt to re-check status via an authenticated, server-to-server call to the provider; the
// callback's own query parameters are never trusted as proof of a payment outcome. Pesapal's
// IPN protocol carries no cryptographic signature, so this reconciliation-first design is the
// equivalent control: see docs/PAYMENT-PROVIDER-DECISION.md.
[ApiController]
[Route("api/payments/webhooks")]
[AllowAnonymous]
public sealed class PaymentWebhooksController : ControllerBase
{
    private readonly IPaymentReconciliationService _reconciliation;
    private readonly ILogger<PaymentWebhooksController> _logger;

    public PaymentWebhooksController(
        IPaymentReconciliationService reconciliation,
        ILogger<PaymentWebhooksController> logger)
    {
        _reconciliation = reconciliation;
        _logger = logger;
    }

    // Pesapal's IPN v3 contract calls back with GET and these query parameters, and expects a
    // small JSON acknowledgement echoing them back alongside a status code.
    [EnableRateLimiting(RateLimitPolicyNames.PaymentWebhook)]
    [HttpGet("pesapal")]
    public async Task<IActionResult> PesapalCallback(
        [FromQuery(Name = "OrderTrackingId")] string? orderTrackingId,
        [FromQuery(Name = "OrderMerchantReference")] string? orderMerchantReference,
        [FromQuery(Name = "OrderNotificationType")] string? orderNotificationType)
    {
        if (string.IsNullOrWhiteSpace(orderTrackingId))
        {
            return BadRequest();
        }

        try
        {
            await _reconciliation.ReconcileAsync("pesapal", orderTrackingId, HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            // Never leak provider/internal error details to a webhook caller; log for operators
            // and still acknowledge so Pesapal does not endlessly retry a request we cannot
            // service right now (operational reconciliation can be retried out-of-band).
            _logger.LogError(ex, "Payment reconciliation failed for Pesapal order {OrderTrackingId}.", orderTrackingId);
        }

        return Ok(new
        {
            orderNotificationType,
            orderTrackingId,
            orderMerchantReference,
            status = 200
        });
    }
}
