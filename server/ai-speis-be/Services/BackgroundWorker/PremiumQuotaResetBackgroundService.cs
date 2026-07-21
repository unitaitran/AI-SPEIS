using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ai_speis_be.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ai_speis_be.Services.BackgroundWorker
{
    public class PremiumQuotaResetBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PremiumQuotaResetBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6); // Check every 6 hours

        public PremiumQuotaResetBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<PremiumQuotaResetBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Premium Quota Reset Background Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessQuotaResetsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown/cancellation, exit gracefully
                    break;
                }
                catch (Exception ex)
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    _logger.LogError(ex, "Error occurred executing PremiumQuotaResetBackgroundService.");
                }

                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("Premium Quota Reset Background Service is stopping.");
        }

        private async Task ProcessQuotaResetsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var now = DateTime.UtcNow;
            // 30 days is our "monthly" definition for resetting.
            var oneMonthAgo = now.AddDays(-30);

            // Find premium users whose LastQuotaResetAt is over 30 days ago
            var usersToReset = await context.Users
                .Where(u => u.IsPremium
                            && u.PremiumExpireAt != null 
                            && u.PremiumExpireAt > now
                            && u.LastQuotaResetAt != null 
                            && u.LastQuotaResetAt <= oneMonthAgo)
                .ToListAsync(stoppingToken);

            if (usersToReset.Any())
            {
                foreach (var user in usersToReset)
                {
                    // Update quota back to 15
                    user.RemainingInterviewQuota = 15;
                    // Reset the timer for next month
                    user.LastQuotaResetAt = now;
                    user.UpdatedAt = now;
                }

                await context.SaveChangesAsync(stoppingToken);
                _logger.LogInformation($"Successfully reset interview quota for {usersToReset.Count} Premium users.");
            }
        }
    }
}
