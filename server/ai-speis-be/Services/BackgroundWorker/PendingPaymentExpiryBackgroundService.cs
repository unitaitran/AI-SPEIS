using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.RewardService;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Services.BackgroundWorker;

public sealed class PendingPaymentExpiryBackgroundService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan LegacyExpiryDuration = TimeSpan.FromMinutes(10);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PendingPaymentExpiryBackgroundService> _logger;

    public PendingPaymentExpiryBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<PendingPaymentExpiryBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Pending payment expiry service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpirePendingPaymentsAsync(stoppingToken);
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while expiring pending payments.");
                await Task.Delay(CheckInterval, stoppingToken);
            }
        }

        _logger.LogInformation("Pending payment expiry service is stopping.");
    }

    private async Task ExpirePendingPaymentsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rewardService = scope.ServiceProvider.GetRequiredService<IRewardService>();
        var now = DateTime.UtcNow;
        var legacyCutoff = now.Subtract(LegacyExpiryDuration);

        var expiredPayments = await context.Payments
            .Where(payment => payment.Status == PaymentStatus.Pending
                && ((payment.ExpiredAt != null && payment.ExpiredAt <= now)
                    || (payment.ExpiredAt == null && payment.CreatedAt <= legacyCutoff)))
            .ToListAsync(cancellationToken);

        foreach (var payment in expiredPayments)
        {
            payment.Status = PaymentStatus.Expired;
            payment.FailureReason = "Payment expired before completion.";
            await rewardService.ReleasePaymentReservationAsync(
                payment.UserId,
                payment.RewardPointsUsed,
                payment.OrderCode,
                cancellationToken);
        }

        if (expiredPayments.Count == 0)
        {
            return;
        }

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Expired {Count} pending payments and released their reserved reward points.",
            expiredPayments.Count);
    }
}
