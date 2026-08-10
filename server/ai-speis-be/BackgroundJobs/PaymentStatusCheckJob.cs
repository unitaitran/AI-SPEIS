using ai_speis_be.Models.DTOs.Payment;
using ai_speis_be.Services.PaymentService;
using Hangfire;

namespace ai_speis_be.BackgroundJobs;

// Payment callbacks are serialized across workers to prevent two valid callbacks from
// attempting to activate the same subscription at the same time. The service retains
// the authoritative signature verification and transaction boundaries.
[Queue("payments")]
[DisableConcurrentExecution(timeoutInSeconds: 300)]
[AutomaticRetry(Attempts = 5, DelaysInSeconds = new[] { 60, 300, 900, 1800, 3600 }, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
public sealed class PaymentStatusCheckJob
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentStatusCheckJob> _logger;

    public PaymentStatusCheckJob(IPaymentService paymentService, ILogger<PaymentStatusCheckJob> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task ExecuteAsync(PaymentWebhookRequestDto callback)
    {
        if (string.IsNullOrWhiteSpace(callback.OrderId))
        {
            _logger.LogWarning("Ignored payment callback without an order identifier.");
            return;
        }

        try
        {
            var result = await _paymentService.HandleWebhookAsync(callback, CancellationToken.None);
            if (!result.Success)
            {
                // Invalid, unsuccessful and unverified callbacks are terminal business
                // outcomes. The payment service never activates a subscription for them.
                _logger.LogWarning("Payment callback was rejected or did not complete successfully. ResultCode: {ResultCode}", callback.ResultCode);
                return;
            }

            _logger.LogInformation("Payment callback was processed successfully.");
        }
        catch (Exception exception)
        {
            // Do not log callback payload, order code, transaction id, or customer data.
            // Throwing lets Hangfire retry only operational/transient failures.
            _logger.LogError(exception, "Payment callback processing failed and will be retried by Hangfire.");
            throw;
        }
    }
}
