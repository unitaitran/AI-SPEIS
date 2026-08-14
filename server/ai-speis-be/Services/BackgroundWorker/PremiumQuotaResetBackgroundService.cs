using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ai_speis_be.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ai_speis_be.Services.SubscriptionService;

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
            var subscriptionService = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();

            var now = DateTime.UtcNow;
            var usersToSynchronize = await context.Users
                .Where(user => user.IsPremium || user.PremiumExpireAt != null)
                .ToListAsync(stoppingToken);

            if (usersToSynchronize.Any())
            {
                foreach (var user in usersToSynchronize)
                {
                    await subscriptionService.GetQuotaAsync(user, now, stoppingToken);
                }

                await context.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Synchronized subscription and fixed 30-day quota periods for {Count} users.", usersToSynchronize.Count);
            }
        }
    }
}
