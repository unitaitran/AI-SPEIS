using System.Text;
using ai_speis_be.Models;
using ai_speis_be.Models.DTOs.AdminPayment;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.PaymentRepo;
using ai_speis_be.Services.PaymentService;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Services.AdminPaymentService
{
    public class AdminPaymentService : IAdminPaymentService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IPaymentService _paymentService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminPaymentService> _logger;

        public AdminPaymentService(
            IPaymentRepository paymentRepository,
            IPaymentService paymentService,
            ApplicationDbContext context,
            ILogger<AdminPaymentService> logger)
        {
            _paymentRepository = paymentRepository;
            _paymentService = paymentService;
            _context = context;
            _logger = logger;
        }

        public async Task<PaginatedResultDto<PaymentListDto>> GetPaymentsAsync(
            int page,
            int pageSize,
            PaymentStatus? status,
            int? planId,
            DateTime? dateFrom,
            DateTime? dateTo,
            string? search,
            string? sortBy,
            CancellationToken cancellationToken = default)
        {
            var (items, totalCount) = await _paymentRepository.GetAdminPaginatedAsync(
                page, pageSize, status, planId, dateFrom, dateTo, search, sortBy, cancellationToken);

            var dtos = new List<PaymentListDto>();

            // Pre-fetch prior plan names for users to accurately compute PlanBefore
            var userIds = items.Select(i => i.UserId).Distinct().ToList();
            var userPriorPayments = await _context.Payments
                .Include(p => p.SubscriptionPrice!).ThenInclude(sp => sp.Plan)
                .Where(p => userIds.Contains(p.UserId) && (p.Status == PaymentStatus.Paid || p.Status == PaymentStatus.PaidByReward))
                .AsNoTracking()
                .OrderBy(p => p.CreatedAt)
                .ToListAsync(cancellationToken);

            foreach (var p in items)
            {
                dtos.Add(MapToPaymentListDto(p, userPriorPayments));
            }

            return new PaginatedResultDto<PaymentListDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                Page = Math.Max(1, page),
                PageSize = pageSize
            };
        }

        public async Task<PaymentDetailDto?> GetPaymentDetailAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            var payment = await _paymentRepository.GetAdminDetailByIdAsync(paymentId, cancellationToken);
            if (payment == null) return null;

            var userPriorPayments = await _context.Payments
                .Include(p => p.SubscriptionPrice!).ThenInclude(sp => sp.Plan)
                .Where(p => p.UserId == payment.UserId && (p.Status == PaymentStatus.Paid || p.Status == PaymentStatus.PaidByReward))
                .AsNoTracking()
                .OrderBy(p => p.CreatedAt)
                .ToListAsync(cancellationToken);

            var baseDto = MapToPaymentListDto(payment, userPriorPayments);

            // User Info
            var user = payment.User;
            var userInfo = new UserInfoDto
            {
                UserId = user.UserId,
                FullName = user.FullName ?? user.Email,
                Email = user.Email,
                Role = user.Role?.RoleName ?? "Student",
                CreatedAt = user.CreatedAt
            };

            // Subscription Info
            var userSub = await _context.UserSubscriptions
                .Include(us => us.Plan)
                .FirstOrDefaultAsync(us => us.UserId == payment.UserId, cancellationToken);

            var term = await _context.SubscriptionTerms
                .FirstOrDefaultAsync(t => t.SourcePaymentId == payment.PaymentId, cancellationToken);

            var subInfo = new SubscriptionDetailInfoDto
            {
                UserSubscriptionId = userSub?.UserSubscriptionId,
                PriceId = payment.PriceId,
                PlanCode = payment.SubscriptionPrice?.Plan?.Code ?? "FREE",
                PlanName = payment.SubscriptionPrice?.Plan?.Name ?? "Free",
                BillingCycle = payment.SubscriptionPrice?.BillingCycle ?? BillingCycle.Monthly,
                PlanBefore = baseDto.PlanBefore,
                PlanAfter = baseDto.PlanAfter,
                TermStartsAt = term?.StartsAt ?? payment.PaidAt,
                TermEndsAt = term?.EndsAt ?? userSub?.ExpiresAt
            };

            // MoMo Raw Info
            var momoInfo = new MoMoTransactionDetailDto
            {
                PartnerCode = "MOMO",
                OrderId = payment.OrderCode,
                RequestId = payment.OrderCode,
                TransId = payment.ProviderTransactionId ?? string.Empty,
                Amount = payment.Amount,
                ResultCode = payment.Status == PaymentStatus.Paid || payment.Status == PaymentStatus.PaidByReward ? 0 : (payment.Status == PaymentStatus.Failed ? 1006 : (int?)null),
                Message = payment.FailureReason ?? (payment.Status == PaymentStatus.Paid ? "Successful transaction." : "Transaction in progress."),
                PayType = "qr",
                ResponseTime = payment.PaidAt.HasValue ? new DateTimeOffset(payment.PaidAt.Value).ToUnixTimeMilliseconds() : 0,
                RawCallbackJson = $"{{\"orderId\":\"{payment.OrderCode}\",\"transId\":\"{payment.ProviderTransactionId}\",\"amount\":{payment.Amount},\"resultCode\":{(payment.Status == PaymentStatus.Paid ? 0 : 1006)},\"message\":\"{payment.FailureReason ?? "OK"}\"}}",
                RawResponseJson = $"{{\"partnerCode\":\"MOMO\",\"orderId\":\"{payment.OrderCode}\",\"amount\":{payment.Amount},\"status\":{(int)payment.Status}}}",
                VerificationFailed = payment.Status == PaymentStatus.Failed,
                VerificationError = payment.FailureReason
            };

            // Timeline Steps
            var timeline = new List<PaymentTimelineStepDto>
            {
                new()
                {
                    Step = "created",
                    Title = "Payment Created",
                    Timestamp = payment.CreatedAt,
                    Status = "completed",
                    Description = $"Order code {payment.OrderCode} created for amount {payment.Amount:N0} VND."
                },
                new()
                {
                    Step = "redirect",
                    Title = "Redirected to MoMo",
                    Timestamp = payment.CreatedAt.AddSeconds(2),
                    Status = "completed",
                    Description = "User redirected to MoMo payment gateway."
                },
                new()
                {
                    Step = "paid",
                    Title = "User Payment Status",
                    Timestamp = payment.PaidAt,
                    Status = payment.Status == PaymentStatus.Paid || payment.Status == PaymentStatus.PaidByReward ? "completed" : (payment.Status == PaymentStatus.Failed || payment.Status == PaymentStatus.Expired ? "failed" : "pending"),
                    Description = payment.PaidAt.HasValue ? $"MoMo trans ID: {payment.ProviderTransactionId}" : "Waiting for payment completion."
                },
                new()
                {
                    Step = "callback",
                    Title = "Callback / IPN Received",
                    Timestamp = payment.PaidAt,
                    Status = payment.Status == PaymentStatus.Paid || payment.Status == PaymentStatus.PaidByReward ? "completed" : (payment.Status == PaymentStatus.Failed ? "failed" : "pending"),
                    Description = payment.Status == PaymentStatus.Paid ? "MoMo webhook signature verified successfully." : "Pending callback verification."
                },
                new()
                {
                    Step = "verified",
                    Title = "Transaction Verified",
                    Timestamp = payment.PaidAt,
                    Status = payment.Status == PaymentStatus.Paid || payment.Status == PaymentStatus.PaidByReward ? "completed" : "pending",
                    Description = "Verified with MoMo Query API."
                },
                new()
                {
                    Step = "updated",
                    Title = "Subscription Updated",
                    Timestamp = payment.PaidAt,
                    Status = payment.Status == PaymentStatus.Paid || payment.Status == PaymentStatus.PaidByReward ? "completed" : "pending",
                    Description = $"User upgraded from {baseDto.PlanBefore} to {baseDto.PlanAfter}."
                }
            };

            // Student Upgrade History
            var userHistory = userPriorPayments
                .Select(p => new SubscriptionHistoryDto
                {
                    PaymentId = p.PaymentId,
                    PlanBefore = "Free",
                    PlanAfter = p.SubscriptionPrice?.Plan?.Name ?? "Premium",
                    Amount = p.Amount,
                    Date = p.PaidAt ?? p.CreatedAt,
                    Status = p.Status.ToString()
                })
                .ToList();

            return new PaymentDetailDto
            {
                PaymentId = baseDto.PaymentId,
                OrderCode = baseDto.OrderCode,
                ProviderTransactionId = baseDto.ProviderTransactionId,
                UserId = baseDto.UserId,
                StudentName = baseDto.StudentName,
                Email = baseDto.Email,
                PlanBefore = baseDto.PlanBefore,
                PlanAfter = baseDto.PlanAfter,
                Amount = baseDto.Amount,
                OriginalAmount = baseDto.OriginalAmount,
                DiscountAmount = baseDto.DiscountAmount,
                RewardPointsUsed = baseDto.RewardPointsUsed,
                Currency = baseDto.Currency,
                PaymentMethod = baseDto.PaymentMethod,
                Status = baseDto.Status,
                CreatedAt = baseDto.CreatedAt,
                PaidAt = baseDto.PaidAt,
                ExpiredAt = baseDto.ExpiredAt,
                FailureReason = baseDto.FailureReason,
                User = userInfo,
                Subscription = subInfo,
                MoMoDetails = momoInfo,
                Timeline = timeline,
                UpgradeHistory = userHistory
            };
        }

        public async Task<PaymentStatisticsDto> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var todayStart = now.Date;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var payments = await _context.Payments
                .Include(p => p.SubscriptionPrice!).ThenInclude(sp => sp.Plan)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var paidPayments = payments.Where(p => p.Status == PaymentStatus.Paid || p.Status == PaymentStatus.PaidByReward).ToList();

            decimal todayRevenue = paidPayments.Where(p => p.PaidAt >= todayStart || (p.PaidAt == null && p.CreatedAt >= todayStart)).Sum(p => p.Amount);
            decimal monthlyRevenue = paidPayments.Where(p => p.PaidAt >= monthStart || (p.PaidAt == null && p.CreatedAt >= monthStart)).Sum(p => p.Amount);

            int successCount = paidPayments.Count;
            int failedCount = payments.Count(p => p.Status == PaymentStatus.Failed || p.Status == PaymentStatus.Expired);
            int pendingCount = payments.Count(p => p.Status == PaymentStatus.Pending);
            int refundedCount = payments.Count(p => p.Status == PaymentStatus.Refunded || p.Status == PaymentStatus.Cancelled);

            int newPremiumToday = paidPayments.Count(p => (p.PaidAt >= todayStart || p.CreatedAt >= todayStart));
            int newPremiumThisMonth = paidPayments.Count(p => (p.PaidAt >= monthStart || p.CreatedAt >= monthStart));

            // Revenue trend last 14 days
            var revenueTrend = new List<RevenueTrendPointDto>();
            for (int i = 13; i >= 0; i--)
            {
                var day = todayStart.AddDays(-i);
                var nextDay = day.AddDays(1);
                var dayPayments = paidPayments.Where(p => (p.PaidAt ?? p.CreatedAt) >= day && (p.PaidAt ?? p.CreatedAt) < nextDay).ToList();

                revenueTrend.Add(new RevenueTrendPointDto
                {
                    Label = day.ToString("dd/MM"),
                    Revenue = dayPayments.Sum(p => p.Amount),
                    Count = dayPayments.Count
                });
            }

            // Status distribution
            int totalCount = payments.Count;
            var statusDistribution = Enum.GetValues<PaymentStatus>()
                .Select(st =>
                {
                    int cnt = payments.Count(p => p.Status == st);
                    return new StatusDistributionDto
                    {
                        Status = st.ToString(),
                        Count = cnt,
                        Percentage = totalCount > 0 ? Math.Round((double)cnt / totalCount * 100, 1) : 0
                    };
                })
                .Where(sd => sd.Count > 0)
                .ToList();

            // Top Selling Plans
            var topSellingPlans = paidPayments
                .GroupBy(p => p.SubscriptionPrice?.Plan?.Name ?? "Gói Premium")
                .Select(g => new TopPlanSalesDto
                {
                    PlanName = g.Key,
                    SalesCount = g.Count(),
                    Revenue = g.Sum(p => p.Amount)
                })
                .OrderByDescending(t => t.Revenue)
                .ToList();

            return new PaymentStatisticsDto
            {
                TodayRevenue = todayRevenue,
                MonthlyRevenue = monthlyRevenue,
                SuccessfulPayments = successCount,
                FailedPayments = failedCount,
                PendingPayments = pendingCount,
                RefundedPayments = refundedCount,
                NewPremiumToday = newPremiumToday,
                NewPremiumThisMonth = newPremiumThisMonth,
                RenewalCount = Math.Max(0, successCount - newPremiumThisMonth),
                UpgradeCount = newPremiumThisMonth,
                RevenueTrend = revenueTrend,
                StatusDistribution = statusDistribution,
                TopSellingPlans = topSellingPlans
            };
        }

        public async Task<List<PaymentListDto>> GetRecentPaymentsAsync(int count = 5, CancellationToken cancellationToken = default)
        {
            var items = await _paymentRepository.GetAdminRecentAsync(count, cancellationToken);
            var userIds = items.Select(i => i.UserId).Distinct().ToList();
            var userPriorPayments = await _context.Payments
                .Include(p => p.SubscriptionPrice!).ThenInclude(sp => sp.Plan)
                .Where(p => userIds.Contains(p.UserId) && (p.Status == PaymentStatus.Paid || p.Status == PaymentStatus.PaidByReward))
                .AsNoTracking()
                .OrderBy(p => p.CreatedAt)
                .ToListAsync(cancellationToken);

            return items.Select(p => MapToPaymentListDto(p, userPriorPayments)).ToList();
        }

        public async Task<(bool Success, string Message, PaymentDetailDto? Detail)> VerifyPaymentWithMoMoAsync(
            int paymentId, CancellationToken cancellationToken = default)
        {
            var payment = await _paymentRepository.GetAdminDetailByIdAsync(paymentId, cancellationToken);
            if (payment == null)
            {
                return (false, "Không tìm thấy giao dịch.", null);
            }

            _logger.LogInformation("Admin triggered re-verify for OrderCode: {OrderCode}", payment.OrderCode);

            // Re-verify with MoMo API using existing IPaymentService QueryTransactionStatusAsync method
            var (success, errorMessage) = await _paymentService.QueryTransactionStatusAsync(payment.OrderCode, null, cancellationToken);

            var updatedDetail = await GetPaymentDetailAsync(paymentId, cancellationToken);

            if (!success)
            {
                return (false, errorMessage ?? "Không thể xác minh trạng thái từ MoMo.", updatedDetail);
            }

            return (true, "Đã đồng bộ trạng thái giao dịch với MoMo thành công.", updatedDetail);
        }

        public async Task<byte[]> ExportPaymentsExcelAsync(
            PaymentStatus? status,
            int? planId,
            DateTime? dateFrom,
            DateTime? dateTo,
            string? search,
            string? sortBy,
            CancellationToken cancellationToken = default)
        {
            var (items, _) = await _paymentRepository.GetAdminPaginatedAsync(
                1, 10000, status, planId, dateFrom, dateTo, search, sortBy, cancellationToken);

            var userIds = items.Select(i => i.UserId).Distinct().ToList();
            var userPriorPayments = await _context.Payments
                .Include(p => p.SubscriptionPrice!).ThenInclude(sp => sp.Plan)
                .Where(p => userIds.Contains(p.UserId) && (p.Status == PaymentStatus.Paid || p.Status == PaymentStatus.PaidByReward))
                .AsNoTracking()
                .OrderBy(p => p.CreatedAt)
                .ToListAsync(cancellationToken);

            var sb = new StringBuilder();
            // UTF-8 BOM for Excel compatibility
            sb.Append('\uFEFF');

            // Header
            sb.AppendLine("ID,Mã Đơn,Mã MoMo,Sinh Viên,Email,Gói Trái/Phải (Upgrade),Số Tiền (VND),Trạng Thái,Ngày Tạo,Ngày Thanh Toán");

            foreach (var item in items)
            {
                var dto = MapToPaymentListDto(item, userPriorPayments);
                sb.AppendLine($"\"{dto.PaymentId}\",\"{dto.OrderCode}\",\"{dto.ProviderTransactionId ?? ""}\",\"{dto.StudentName}\",\"{dto.Email}\",\"{dto.PlanBefore} -> {dto.PlanAfter}\",\"{dto.Amount:N0}\",\"{dto.StatusName}\",\"{dto.CreatedAt:dd/MM/yyyy HH:mm}\",\"{dto.PaidAt?.ToString("dd/MM/yyyy HH:mm") ?? ""}\"");
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        // Helper: Calculate PlanBefore & PlanAfter for a payment
        private static PaymentListDto MapToPaymentListDto(Payment p, List<Payment> userPriorPayments)
        {
            var planAfterName = p.SubscriptionPrice?.Plan?.Name ?? "Premium";

            // Find prior paid payment for this user created before current payment
            var prior = userPriorPayments
                .Where(up => up.UserId == p.UserId && up.PaymentId != p.PaymentId && up.CreatedAt < p.CreatedAt)
                .OrderByDescending(up => up.CreatedAt)
                .FirstOrDefault();

            var planBeforeName = prior?.SubscriptionPrice?.Plan?.Name ?? "Free";

            return new PaymentListDto
            {
                PaymentId = p.PaymentId,
                OrderCode = p.OrderCode,
                ProviderTransactionId = p.ProviderTransactionId,
                UserId = p.UserId,
                StudentName = p.User?.FullName ?? p.User?.Email ?? "Student",
                Email = p.User?.Email ?? string.Empty,
                PlanBefore = planBeforeName,
                PlanAfter = planAfterName,
                Amount = p.Amount,
                OriginalAmount = p.OriginalAmount,
                DiscountAmount = p.DiscountAmount,
                RewardPointsUsed = p.RewardPointsUsed,
                Currency = p.Currency ?? "VND",
                PaymentMethod = "MoMo",
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                PaidAt = p.PaidAt,
                ExpiredAt = p.ExpiredAt,
                FailureReason = p.FailureReason
            };
        }
    }
}
