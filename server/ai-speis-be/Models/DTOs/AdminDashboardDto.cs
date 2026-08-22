using System;
using System.Collections.Generic;
using ai_speis_be.DTOs.GoogleQuota;
using ai_speis_be.Models.DTOs.AdminPayment;

namespace ai_speis_be.Models.DTOs
{
    public class AdminDashboardResponseDto
    {
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public DashboardOverviewDto Overview { get; set; } = new();
        public DashboardSubscriptionsDto Subscriptions { get; set; } = new();
        public DashboardPaymentsDto Payments { get; set; } = new();
        public DashboardInterviewDto Interviews { get; set; } = new();
        public DashboardQuestionBankDto QuestionBank { get; set; } = new();
        public DashboardCvDto Cv { get; set; } = new();
        public GoogleDashboardResponseDto? AiUsageAndCost { get; set; }
        public DashboardSystemHealthDto SystemHealth { get; set; } = new();
        public List<DashboardRecentActivityDto> RecentActivities { get; set; } = new();
        public List<DashboardQuickActionDto> QuickActions { get; set; } = new();
    }

    public class DashboardOverviewDto
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int PaidUsers { get; set; }
        public int FreeUsers { get; set; }
        public int NewUsersToday { get; set; }
        public int NewUsersThisMonth { get; set; }
        public List<DashboardTrendPointDto> UserGrowthTrend { get; set; } = new();
    }

    public class DashboardSubscriptionsDto
    {
        public int FreeCount { get; set; }
        public int PremiumMonthlyCount { get; set; }
        public int PremiumYearlyCount { get; set; }
        public int ExpiredCount { get; set; }
        public int RenewTodayCount { get; set; }
        public int RenewThisMonthCount { get; set; }
        public List<DashboardDistributionPointDto> Distribution { get; set; } = new();
    }

    public class DashboardPaymentsDto
    {
        public decimal TodayRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public int SuccessfulPayments { get; set; }
        public int PendingPayments { get; set; }
        public int FailedPayments { get; set; }
        public int RefundedPayments { get; set; }
        public List<DashboardTrendPointDto> RevenueTrend { get; set; } = new();
        public List<DashboardDistributionPointDto> StatusDistribution { get; set; } = new();
        public List<PaymentListDto> LatestTransactions { get; set; } = new();
    }

    public class DashboardInterviewDto
    {
        public int TotalSessions { get; set; }
        public int CompletedSessions { get; set; }
        public int InProgressSessions { get; set; }
        public int CancelledSessions { get; set; }
        public double AverageAiScore { get; set; }
        public double AverageDurationMinutes { get; set; }
        public int TodaySessions { get; set; }
        public List<DashboardTrendPointDto> DailyInterviewStats { get; set; } = new();
    }

    public class DashboardQuestionBankDto
    {
        public int TotalQuestions { get; set; }
        public int TechnicalCount { get; set; }
        public int BehavioralCount { get; set; }
        public int CodingCount { get; set; }
        public string? NewestQuestion { get; set; }
    }

    public class DashboardCvDto
    {
        public int TotalUploadedCv { get; set; }
        public int ParsedSuccessCount { get; set; }
        public int ParsedFailedCount { get; set; }
    }

    public class DashboardSystemHealthDto
    {
        public string ApiStatus { get; set; } = "Healthy";
        public string DatabaseStatus { get; set; } = "Healthy";
        public string GoogleApisStatus { get; set; } = "Healthy";
        public string MoMoStatus { get; set; } = "Healthy";
        public string StorageStatus { get; set; } = "Healthy";
    }

    public class DashboardRecentActivityDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Status { get; set; } = "info";
    }

    public class DashboardQuickActionDto
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class DashboardTrendPointDto
    {
        public string DateLabel { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public int Count { get; set; }
    }

    public class DashboardDistributionPointDto
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }
}
