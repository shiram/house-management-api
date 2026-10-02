using HouseManagement.Api.Controllers;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HouseManagement.Api.Tests;

public class PaymentWebhooksControllerTests
{
    [Fact]
    public async Task PesapalCallback_ReturnsBadRequestWhenOrderTrackingIdIsMissing()
    {
        var reconciliation = new Mock<IPaymentReconciliationService>();
        var controller = CreateController(reconciliation);

        var result = await controller.PesapalCallback(null, "merchant-ref", "IPNCHANGE");

        Assert.IsType<BadRequestResult>(result);
        reconciliation.Verify(
            item => item.ReconcileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PesapalCallback_ReconcilesAndAcknowledgesTheCallback()
    {
        var reconciliation = new Mock<IPaymentReconciliationService>();
        reconciliation
            .Setup(item => item.ReconcileAsync("pesapal", "order-tracking-abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentReconciliationResult(PaymentReconciliationOutcome.Reconciled));
        var controller = CreateController(reconciliation);

        var result = await controller.PesapalCallback("order-tracking-abc", "merchant-ref", "IPNCHANGE");

        var ok = Assert.IsType<JsonResult>(result);
        reconciliation.Verify(
            item => item.ReconcileAsync("pesapal", "order-tracking-abc", It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task PesapalCallback_StillAcknowledgesWhenReconciliationThrows()
    {
        var reconciliation = new Mock<IPaymentReconciliationService>();
        reconciliation
            .Setup(item => item.ReconcileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider unreachable"));
        var controller = CreateController(reconciliation);

        var result = await controller.PesapalCallback("order-tracking-abc", "merchant-ref", "IPNCHANGE");

        Assert.IsType<JsonResult>(result);
    }

    private static PaymentWebhooksController CreateController(Mock<IPaymentReconciliationService> reconciliation)
    {
        return new PaymentWebhooksController(
            reconciliation.Object,
            NullLogger<PaymentWebhooksController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            }
        };
    }
}
