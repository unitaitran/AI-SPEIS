using ai_speis_be.BackgroundJobs;
using ai_speis_be.Models.DTOs.Payment;
using ai_speis_be.Services.NotificationService;
using ai_speis_be.Services.PaymentService;
using ai_speis_be.Services.SubscriptionService;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AiSpeis.BackgroundJobs.Tests;

public sealed class BackgroundJobTests
{
    [Fact]
    public async Task Payment_job_delegates_callback_to_authoritative_service()
    {
        var payment = new Mock<IPaymentService>();
        payment.Setup(service => service.HandleWebhookAsync(It.IsAny<PaymentWebhookRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, (string?)null));
        var job = new PaymentStatusCheckJob(payment.Object, NullLogger<PaymentStatusCheckJob>.Instance);

        await job.ExecuteAsync(new PaymentWebhookRequestDto { OrderId = "test-order", ResultCode = 0 });

        payment.Verify(service => service.HandleWebhookAsync(It.IsAny<PaymentWebhookRequestDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Payment_job_rejects_callback_without_order_before_service_call()
    {
        var payment = new Mock<IPaymentService>();
        var job = new PaymentStatusCheckJob(payment.Object, NullLogger<PaymentStatusCheckJob>.Instance);

        await job.ExecuteAsync(new PaymentWebhookRequestDto());

        payment.Verify(service => service.HandleWebhookAsync(It.IsAny<PaymentWebhookRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Subscription_expiry_job_delegates_to_maintenance_service()
    {
        var maintenance = new Mock<ISubscriptionMaintenanceService>();
        maintenance.Setup(service => service.ReconcileExpiredEntitlementsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(2);
        var job = new SubscriptionExpiryJob(maintenance.Object, NullLogger<SubscriptionExpiryJob>.Instance);

        await job.ExecuteAsync();

        maintenance.Verify(service => service.ReconcileExpiredEntitlementsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Quota_reset_job_delegates_to_maintenance_service()
    {
        var maintenance = new Mock<ISubscriptionMaintenanceService>();
        maintenance.Setup(service => service.SynchronizeQuotaPeriodsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(2);
        var job = new QuotaResetJob(maintenance.Object, NullLogger<QuotaResetJob>.Instance);

        await job.ExecuteAsync();

        maintenance.Verify(service => service.SynchronizeQuotaPeriodsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Notification_retry_job_delegates_to_email_delivery_service()
    {
        var delivery = new Mock<INotificationEmailDeliveryService>();
        delivery.Setup(service => service.RetryFailedAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationEmailRetryResult(3, 2, 1));
        var job = new NotificationRetryJob(delivery.Object, NullLogger<NotificationRetryJob>.Instance);

        await job.ExecuteAsync();

        delivery.Verify(service => service.RetryFailedAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
