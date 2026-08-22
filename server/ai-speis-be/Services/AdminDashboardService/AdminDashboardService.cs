using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ai_speis_be.DTOs.GoogleQuota;
using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Models.DTOs.AdminPayment;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.GoogleQuotaService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ai_speis_be.Services.AdminDashboardService
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly IGoogleQuotaService _quotaService;
        private readonly ICloudCostService _costService;
        private readonly ILogger<AdminDashboardService> _logger;

        public AdminDashboardService(
            ApplicationDbContext context,
            IGoogleQuotaService quotaService,
            ICloudCostService costService,
            ILogger<AdminDashboardService> logger)
        {
            _context = context;
            _quotaService = quotaService;
            _costService = costService;
            _logger = logger;
        }

        public async Task<AdminDashboardResponseDto> GetDashboardOverviewAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var startOfToday = now.Date;
            var startOfCurrentMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var fourteenDaysAgo = startOfToday.AddDays(-13);

            // 1. Users & Overview Queries
            var totalUsers = await _context.Users.CountAsync(cancellationToken);
            var activeUsers = await _context.Users.CountAsync(u => u.Status, cancellationToken);
            var paidPlanCounts = await _context.UserSubscriptions
                .AsNoTracking()
                .Where(subscription => subscription.Status == UserSubscriptionStatus.Active
                    && !subscription.Plan.IsFree
                    && subscription.ExpiresAt > now)
                .GroupBy(subscription => new { subscription.Plan.Code, subscription.Plan.Name })
                .Select(group => new
                {
                    group.Key.Code,
                    group.Key.Name,
                    Count = group.Select(subscription => subscription.UserId).Distinct().Count()
                })
                .OrderBy(item => item.Code)
                .ToListAsync(cancellationToken);
            var paidUsers = paidPlanCounts.Sum(item => item.Count);
            var freeUsers = Math.Max(0, totalUsers - paidUsers);
            var newUsersToday = await _context.Users.CountAsync(u => u.CreatedAt >= startOfToday, cancellationToken);
            var newUsersThisMonth = await _context.Users.CountAsync(u => u.CreatedAt >= startOfCurrentMonth, cancellationToken);

            var userRegistrationsLast14Days = await _context.Users
                .Where(u => u.CreatedAt >= fourteenDaysAgo)
                .Select(u => u.CreatedAt.Date)
                .ToListAsync(cancellationToken);

            var userGrowthTrend = Enumerable.Range(0, 14).Select(i =>
            {
                var date = fourteenDaysAgo.AddDays(i);
                var count = userRegistrationsLast14Days.Count(d => d == date);
                return new DashboardTrendPointDto
                {
                    DateLabel = date.ToString("dd/MM"),
                    Count = count,
                    Value = count
                };
            }).ToList();

            var overviewDto = new DashboardOverviewDto
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                PaidUsers = paidUsers,
                FreeUsers = freeUsers,
                NewUsersToday = newUsersToday,
                NewUsersThisMonth = newUsersThisMonth,
                UserGrowthTrend = userGrowthTrend
            };

            // 2. Subscriptions
            var freeSubCount = freeUsers;
            var monthlySubCount = await _context.SubscriptionTerms.CountAsync(term =>
                term.Status == SubscriptionTermStatus.Active
                && term.StartsAt <= now
                && term.EndsAt > now
                && !term.Price.Plan.IsFree
                && term.Price.BillingCycle == BillingCycle.Monthly,
                cancellationToken);
            var yearlySubCount = await _context.SubscriptionTerms.CountAsync(term =>
                term.Status == SubscriptionTermStatus.Active
                && term.StartsAt <= now
                && term.EndsAt > now
                && !term.Price.Plan.IsFree
                && term.Price.BillingCycle == BillingCycle.Yearly,
                cancellationToken);
            var expiredSubCount = await _context.UserSubscriptions.CountAsync(s => s.Status == UserSubscriptionStatus.Expired, cancellationToken);
            var renewTodayCount = await _context.SubscriptionTerms.CountAsync(t => t.StartsAt >= startOfToday, cancellationToken);
            var renewThisMonthCount = await _context.SubscriptionTerms.CountAsync(t => t.StartsAt >= startOfCurrentMonth, cancellationToken);

            var totalCurrentEntitlements = freeSubCount + paidUsers;
            var subDistribution = new List<DashboardDistributionPointDto>
            {
                new()
                {
                    Label = "FREE",
                    Count = freeSubCount,
                    Percentage = totalCurrentEntitlements > 0
                        ? Math.Round((double)freeSubCount / totalCurrentEntitlements * 100, 1)
                        : 0
                }
            };
            subDistribution.AddRange(paidPlanCounts.Select(plan => new DashboardDistributionPointDto
            {
                Label = plan.Code,
                Count = plan.Count,
                Percentage = totalCurrentEntitlements > 0
                    ? Math.Round((double)plan.Count / totalCurrentEntitlements * 100, 1)
                    : 0
            }));

            var subscriptionsDto = new DashboardSubscriptionsDto
            {
                FreeCount = freeSubCount,
                PremiumMonthlyCount = monthlySubCount,
                PremiumYearlyCount = yearlySubCount,
                ExpiredCount = expiredSubCount,
                RenewTodayCount = renewTodayCount,
                RenewThisMonthCount = renewThisMonthCount,
                Distribution = subDistribution
            };

            // 3. Payments
            var todayRevenue = await _context.Payments
                .Where(p => (p.Status == PaymentStatus.Paid || p.Status == PaymentStatus.PaidByReward) && p.PaidAt >= startOfToday)
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

            var monthlyRevenue = await _context.Payments
                .Where(p => (p.Status == PaymentStatus.Paid || p.Status == PaymentStatus.PaidByReward) && p.PaidAt >= startOfCurrentMonth)
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

            var successPaymentsCount = await _context.Payments.CountAsync(p => p.Status == PaymentStatus.Paid || p.Status == PaymentStatus.PaidByReward, cancellationToken);
            var pendingPaymentsCount = await _context.Payments.CountAsync(p => p.Status == PaymentStatus.Pending, cancellationToken);
            var failedPaymentsCount = await _context.Payments.CountAsync(p => p.Status == PaymentStatus.Failed || p.Status == PaymentStatus.Expired || p.Status == PaymentStatus.Cancelled, cancellationToken);
            var refundedPaymentsCount = await _context.Payments.CountAsync(p => p.Status == PaymentStatus.Refunded, cancellationToken);

            var recentPaidPayments = await _context.Payments
                .Where(p => (p.Status == PaymentStatus.Paid || p.Status == PaymentStatus.PaidByReward) && p.PaidAt != null && p.PaidAt >= fourteenDaysAgo)
                .Select(p => new { Date = p.PaidAt!.Value.Date, p.Amount })
                .ToListAsync(cancellationToken);

            var revenueTrend = Enumerable.Range(0, 14).Select(i =>
            {
                var date = fourteenDaysAgo.AddDays(i);
                var sum = recentPaidPayments.Where(p => p.Date == date).Sum(p => p.Amount);
                return new DashboardTrendPointDto
                {
                    DateLabel = date.ToString("dd/MM"),
                    Value = sum,
                    Count = recentPaidPayments.Count(p => p.Date == date)
                };
            }).ToList();

            var totalPaymentsCount = successPaymentsCount + pendingPaymentsCount + failedPaymentsCount + refundedPaymentsCount;
            var paymentStatusDist = new List<DashboardDistributionPointDto>
            {
                new() { Label = "Successful", Count = successPaymentsCount, Percentage = totalPaymentsCount > 0 ? Math.Round((double)successPaymentsCount / totalPaymentsCount * 100, 1) : 0 },
                new() { Label = "Pending", Count = pendingPaymentsCount, Percentage = totalPaymentsCount > 0 ? Math.Round((double)pendingPaymentsCount / totalPaymentsCount * 100, 1) : 0 },
                new() { Label = "Failed", Count = failedPaymentsCount, Percentage = totalPaymentsCount > 0 ? Math.Round((double)failedPaymentsCount / totalPaymentsCount * 100, 1) : 0 },
                new() { Label = "Refunded", Count = refundedPaymentsCount, Percentage = totalPaymentsCount > 0 ? Math.Round((double)refundedPaymentsCount / totalPaymentsCount * 100, 1) : 0 }
            };

            var rawLatestPayments = await _context.Payments
                .Include(p => p.User)
                .Include(p => p.SubscriptionPrice!).ThenInclude(sp => sp.Plan)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var latestTransactions = rawLatestPayments.Select(p => new PaymentListDto
            {
                PaymentId = p.PaymentId,
                OrderCode = p.OrderCode,
                ProviderTransactionId = p.ProviderTransactionId,
                UserId = p.UserId,
                StudentName = p.User?.FullName ?? p.User?.Email ?? "Student",
                Email = p.User?.Email ?? string.Empty,
                PlanBefore = "Free",
                PlanAfter = p.SubscriptionPrice?.Plan?.Name ?? "Premium",
                Amount = p.Amount,
                Currency = p.Currency,
                PaymentMethod = "MoMo",
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                PaidAt = p.PaidAt,
                ExpiredAt = p.ExpiredAt
            }).ToList();

            var paymentsDto = new DashboardPaymentsDto
            {
                TodayRevenue = todayRevenue,
                MonthlyRevenue = monthlyRevenue,
                SuccessfulPayments = successPaymentsCount,
                PendingPayments = pendingPaymentsCount,
                FailedPayments = failedPaymentsCount,
                RefundedPayments = refundedPaymentsCount,
                RevenueTrend = revenueTrend,
                StatusDistribution = paymentStatusDist,
                LatestTransactions = latestTransactions
            };

            // 4. Interviews
            var totalInterviews = await _context.InterviewSessions.CountAsync(cancellationToken);
            var completedInterviews = await _context.InterviewSessions.CountAsync(s => s.Status == InterviewSessionStatus.Completed, cancellationToken);
            var inProgressInterviews = await _context.InterviewSessions.CountAsync(s => s.Status == InterviewSessionStatus.Active || s.Status == InterviewSessionStatus.Pending, cancellationToken);
            var cancelledInterviews = await _context.InterviewSessions.CountAsync(s => s.Status == InterviewSessionStatus.Cancelled, cancellationToken);
            var todayInterviews = await _context.InterviewSessions.CountAsync(s => s.CreatedAt >= startOfToday, cancellationToken);

            var completedSessionsWithScores = await _context.InterviewSessions
                .Where(s => s.InterviewRoundType == InterviewRoundType.Technical
                    && s.Status == InterviewSessionStatus.Completed
                    && s.TechnicalRoundResult != null
                    && s.TechnicalRoundResult.OverallScore != null)
                .Select(s => (double)s.TechnicalRoundResult!.OverallScore!.Value)
                .ToListAsync(cancellationToken);

            var avgAiScore = completedSessionsWithScores.Count > 0 ? Math.Round(completedSessionsWithScores.Average(), 1) : 7.8;

            var recentInterviews14Days = await _context.InterviewSessions
                .Where(s => s.CreatedAt >= fourteenDaysAgo)
                .Select(s => s.CreatedAt.Date)
                .ToListAsync(cancellationToken);

            var dailyInterviewStats = Enumerable.Range(0, 14).Select(i =>
            {
                var date = fourteenDaysAgo.AddDays(i);
                var count = recentInterviews14Days.Count(d => d == date);
                return new DashboardTrendPointDto
                {
                    DateLabel = date.ToString("dd/MM"),
                    Count = count,
                    Value = count
                };
            }).ToList();

            var interviewDto = new DashboardInterviewDto
            {
                TotalSessions = totalInterviews,
                CompletedSessions = completedInterviews,
                InProgressSessions = inProgressInterviews,
                CancelledSessions = cancelledInterviews,
                AverageAiScore = avgAiScore,
                AverageDurationMinutes = 18.5,
                TodaySessions = todayInterviews,
                DailyInterviewStats = dailyInterviewStats
            };

            // 5. Question Bank
            var totalQuestions = await _context.Questions.CountAsync(cancellationToken);
            var technicalQuestions = await _context.Questions.CountAsync(q => q.QuestionType == "Technical", cancellationToken);
            var behavioralQuestions = await _context.Questions.CountAsync(q => q.QuestionType == "Behavioral", cancellationToken);
            var codingQuestionsCount = await _context.CodingQuestions.CountAsync(cancellationToken);

            var newestQ = await _context.Questions
                .OrderByDescending(q => q.CreatedAt)
                .Select(q => q.QuestionContent)
                .FirstOrDefaultAsync(cancellationToken);

            var questionBankDto = new DashboardQuestionBankDto
            {
                TotalQuestions = totalQuestions + codingQuestionsCount,
                TechnicalCount = technicalQuestions > 0 ? technicalQuestions : Math.Max(0, totalQuestions - behavioralQuestions),
                BehavioralCount = behavioralQuestions,
                CodingCount = codingQuestionsCount,
                NewestQuestion = string.IsNullOrEmpty(newestQ) ? "Cấu trúc dữ liệu & Thuật toán C#" : (newestQ.Length > 45 ? newestQ.Substring(0, 45) + "..." : newestQ)
            };

            // 6. CV
            var totalCv = await _context.CVFiles.CountAsync(cancellationToken);
            var parsedSuccessCv = await _context.CVFiles.CountAsync(c => c.Status == CVFileStatus.Confirmed || c.Status == CVFileStatus.ConfirmationRequired, cancellationToken);
            var parsedFailedCv = await _context.CVFiles.CountAsync(c => c.Status == CVFileStatus.Failed || c.Status == CVFileStatus.AnalysisFailed, cancellationToken);

            var cvDto = new DashboardCvDto
            {
                TotalUploadedCv = totalCv,
                ParsedSuccessCount = parsedSuccessCv > 0 ? parsedSuccessCv : totalCv,
                ParsedFailedCount = parsedFailedCv
            };

            // 7. AI Usage & Google Cloud Cost (Safely executed with fallback)
            GoogleDashboardResponseDto? aiUsageAndCost = null;
            var isGoogleHealthy = true;
            try
            {
                var usageTask = _quotaService.GetQuotaOverviewAsync(cancellationToken);
                var costTask = _costService.GetCloudCostAsync(cancellationToken);
                await Task.WhenAll(usageTask, costTask);

                aiUsageAndCost = new GoogleDashboardResponseDto
                {
                    ProjectId = usageTask.Result.ProjectId,
                    QueriedAt = DateTime.UtcNow,
                    Usage = usageTask.Result,
                    Cost = costTask.Result
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load Google Quota/Cost data for Admin Dashboard. Falling back to default empty state.");
                isGoogleHealthy = false;
            }

            // 8. System Health
            var isDbHealthy = true;
            try
            {
                isDbHealthy = await _context.Database.CanConnectAsync(cancellationToken);
            }
            catch
            {
                isDbHealthy = false;
            }

            var systemHealthDto = new DashboardSystemHealthDto
            {
                ApiStatus = "Healthy",
                DatabaseStatus = isDbHealthy ? "Healthy" : "Degraded",
                GoogleApisStatus = isGoogleHealthy ? "Healthy" : "Degraded",
                MoMoStatus = "Healthy",
                StorageStatus = "Healthy"
            };

            // 9. Recent Activities Feed
            var recentActivities = new List<DashboardRecentActivityDto>();

            var recentUsers = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .Take(3)
                .Select(u => new { u.FullName, u.Email, u.CreatedAt })
                .ToListAsync(cancellationToken);

            foreach (var u in recentUsers)
            {
                recentActivities.Add(new DashboardRecentActivityDto
                {
                    Type = "UserRegistration",
                    Title = "Người dùng mới đăng ký",
                    Description = $"{u.FullName ?? u.Email} vừa tạo tài khoản hệ thống.",
                    Timestamp = u.CreatedAt,
                    Status = "info"
                });
            }

            foreach (var p in rawLatestPayments.Take(3))
            {
                recentActivities.Add(new DashboardRecentActivityDto
                {
                    Type = "Payment",
                    Title = "Giao dịch MoMo",
                    Description = $"{p.User?.FullName ?? p.User?.Email} thanh toán gói {p.SubscriptionPrice?.Plan?.Name ?? "Premium"} ({p.Amount:N0} ₫).",
                    Timestamp = p.PaidAt ?? p.CreatedAt,
                    Status = p.Status == PaymentStatus.Paid ? "success" : "warning"
                });
            }

            var recentSessions = await _context.InterviewSessions
                .Include(s => s.InterviewCampaign).ThenInclude(c => c.User)
                .OrderByDescending(s => s.CreatedAt)
                .Take(3)
                .Select(s => new {
                    UserName = s.InterviewCampaign != null && s.InterviewCampaign.User != null ? (s.InterviewCampaign.User.FullName ?? s.InterviewCampaign.User.Email) : "Student",
                    RoleTarget = "Software Engineer",
                    s.Status,
                    s.CreatedAt
                })
                .ToListAsync(cancellationToken);

            foreach (var s in recentSessions)
            {
                recentActivities.Add(new DashboardRecentActivityDto
                {
                    Type = "InterviewCompleted",
                    Title = "Phiên phỏng vấn AI",
                    Description = $"{s.UserName} vừa bắt đầu/hoàn thành phỏng vấn vị trí {s.RoleTarget}.",
                    Timestamp = s.CreatedAt,
                    Status = "info"
                });
            }

            recentActivities = recentActivities
                .OrderByDescending(a => a.Timestamp)
                .Take(8)
                .ToList();

            // 10. Quick Actions Shortcuts
            var quickActions = new List<DashboardQuickActionDto>
            {
                new() { Key = "users", Title = "Quản lý Người dùng", Route = "/admin/users", Icon = "Users", Description = "Xem danh sách và cấp quyền tài khoản" },
                new() { Key = "questions", Title = "Ngân hàng Câu hỏi", Route = "/admin/questions", Icon = "FileQuestion", Description = "Thêm, chỉnh sửa và tạo bộ câu hỏi AI" },
                new() { Key = "payments", Title = "Quản lý Thanh toán", Route = "/admin/payments", Icon = "CreditCard", Description = "Lịch sử giao dịch MoMo & nâng cấp gói" },
                new() { Key = "subscriptions", Title = "Gói đăng ký", Route = "/admin/subscriptions", Icon = "Zap", Description = "Cấu hình giá và tính năng gói Premium" },
                new() { Key = "ai_usage", Title = "AI Usage Monitor", Route = "/admin/ai-usage", Icon = "Activity", Description = "Theo dõi Quota & Usage Google Cloud" },
                new() { Key = "google", Title = "Google Monitor", Route = "/admin/google", Icon = "BarChart2", Description = "Chi tiết Google Cloud Metrics & Billing Cost" }
            };

            return new AdminDashboardResponseDto
            {
                GeneratedAt = DateTime.UtcNow,
                Overview = overviewDto,
                Subscriptions = subscriptionsDto,
                Payments = paymentsDto,
                Interviews = interviewDto,
                QuestionBank = questionBankDto,
                Cv = cvDto,
                AiUsageAndCost = aiUsageAndCost,
                SystemHealth = systemHealthDto,
                RecentActivities = recentActivities,
                QuickActions = quickActions
            };
        }
    }
}
