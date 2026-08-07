using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.NotificationService;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Services.BackgroundWorker;

// Reconciles recent successful payments and sends one operational summary per week.
// The notification deduplication key makes this safe across restarts and retries.
public sealed class AdminOperationalNotificationBackgroundService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminOperationalNotificationBackgroundService> _logger;

    public AdminOperationalNotificationBackgroundService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<AdminOperationalNotificationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { _logger.LogError(exception, "Admin operational notification scheduler failed."); }
            try { await Task.Delay(CheckInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var adminPublisher = scope.ServiceProvider.GetRequiredService<IAdminNotificationPublisher>();
        var nowUtc = DateTime.UtcNow;

        // Reconciliation covers a missed in-request publish without replaying old payments.
        var recentPaidPayments = await context.Payments.AsNoTracking()
            .Where(item => (item.Status == PaymentStatus.Paid || item.Status == PaymentStatus.PaidByReward)
                && item.PaidAt != null && item.PaidAt >= nowUtc.AddHours(-2))
            .Select(item => new { item.PaymentId, item.UserId })
            .ToListAsync(cancellationToken);
        foreach (var payment in recentPaidPayments)
        {
            await adminPublisher.PublishAsync(new AdminNotificationEvent(
                payment.UserId, NotificationType.SUBSCRIPTION_PAYMENT_SUCCEEDED,
                NotificationCategory.SUBSCRIPTION, NotificationSeverity.SUCCESS,
                "Subscription payment received", "A subscription payment was completed successfully.",
                NotificationEntityType.PAYMENT, payment.PaymentId.ToString(), "/admin/payments",
                $"SUBSCRIPTION_PAYMENT_SUCCEEDED:{payment.PaymentId}",
                new Dictionary<string, object?> { ["transactionReference"] = payment.PaymentId }), cancellationToken);
        }

        var timeZone = ResolveTimeZone();
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
        var scheduledDay = Math.Clamp(_configuration.GetValue<int?>("Notification:WeeklyStatisticsDayOfWeek") ?? (int)DayOfWeek.Monday, 0, 6);
        var scheduledHour = Math.Clamp(_configuration.GetValue<int?>("Notification:WeeklyStatisticsHour") ?? 8, 0, 23);
        if ((int)localNow.DayOfWeek != scheduledDay || localNow.Hour != scheduledHour) return;

        var localPeriodEnd = localNow.Date;
        var localPeriodStart = localPeriodEnd.AddDays(-7);
        var utcPeriodStart = TimeZoneInfo.ConvertTimeToUtc(localPeriodStart, timeZone);
        var utcPeriodEnd = TimeZoneInfo.ConvertTimeToUtc(localPeriodEnd, timeZone);
        var newUsers = await context.Users.CountAsync(user => user.CreatedAt >= utcPeriodStart && user.CreatedAt < utcPeriodEnd, cancellationToken);
        var completedInterviews = await context.InterviewSessions.CountAsync(session => session.UpdatedAt >= utcPeriodStart && session.UpdatedAt < utcPeriodEnd && session.Status == InterviewSessionStatus.Completed, cancellationToken);
        var paidPayments = await context.Payments.Where(item => (item.Status == PaymentStatus.Paid || item.Status == PaymentStatus.PaidByReward)
                && item.PaidAt >= utcPeriodStart && item.PaidAt < utcPeriodEnd)
            .Select(item => item.Amount).ToListAsync(cancellationToken);
        var weekKey = $"{localPeriodStart:yyyyMMdd}-{localPeriodEnd:yyyyMMdd}";
        await adminPublisher.PublishAsync(new AdminNotificationEvent(
            0, NotificationType.WEEKLY_SYSTEM_STATISTICS, NotificationCategory.SYSTEM, NotificationSeverity.INFO,
            "Weekly system statistics", "Your weekly AI-SPEIS statistics summary is now available.",
            NotificationEntityType.SYSTEM_SERVICE, "WEEKLY_STATISTICS", "/admin/dashboard",
            $"WEEKLY_SYSTEM_STATISTICS:{weekKey}",
            new Dictionary<string, object?> { ["periodStart"] = localPeriodStart, ["periodEnd"] = localPeriodEnd, ["newUsers"] = newUsers, ["completedInterviews"] = completedInterviews, ["successfulTransactions"] = paidPayments.Count, ["revenue"] = paidPayments.Sum() }), cancellationToken);
    }

    private TimeZoneInfo ResolveTimeZone()
    {
        var configuredId = _configuration["Notification:TimeZoneId"];
        if (string.IsNullOrWhiteSpace(configuredId)) return TimeZoneInfo.Local;
        try { return TimeZoneInfo.FindSystemTimeZoneById(configuredId); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Local; }
    }
}
