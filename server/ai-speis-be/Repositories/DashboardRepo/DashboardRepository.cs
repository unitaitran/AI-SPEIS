using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ai_speis_be.Repositories.DashboardRepo
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly ApplicationDbContext _context;

        public DashboardRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AdminDashboardDto> GetDashboardStatsAsync(
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            // Start from the beginning of the month 5 months ago → 6 months total
            var sixMonthsAgo = startOfMonth.AddMonths(-5);

            // ── User aggregates ──────────────────────────────────────────────
            var totalUsers = await _context.Users.CountAsync(cancellationToken);
            var premiumUsers = await _context.Users.CountAsync(u => u.IsPremium, cancellationToken);
            var lockedUsers = await _context.Users.CountAsync(u => u.IsLocked, cancellationToken);
            var activeUsers = await _context.Users.CountAsync(u => !u.IsLocked && u.Status, cancellationToken);

            // ── Question aggregate ───────────────────────────────────────────
            var totalQuestions = await _context.Questions
                .CountAsync(q => !q.IsDeleted, cancellationToken);

            // ── Interview campaign aggregates ────────────────────────────────
            var totalInterviews = await _context.InterviewCampaigns.CountAsync(cancellationToken);
            var monthlyInterviews = await _context.InterviewCampaigns
                .CountAsync(c => c.CreatedAt >= startOfMonth, cancellationToken);

            // ── Revenue aggregates (paid payments only) ──────────────────────
            var totalRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid)
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

            var monthlyRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt >= startOfMonth)
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

            // ── Time-series: user registrations per month ────────────────────
            var userGrowthRaw = await _context.Users
                .Where(u => u.CreatedAt >= sixMonthsAgo)
                .GroupBy(u => new { u.CreatedAt.Year, u.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync(cancellationToken);

            // ── Time-series: interview campaigns per month ───────────────────
            var interviewActivityRaw = await _context.InterviewCampaigns
                .Where(c => c.CreatedAt >= sixMonthsAgo)
                .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync(cancellationToken);

            // ── Time-series: paid revenue per month ──────────────────────────
            var revenueActivityRaw = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt >= sixMonthsAgo)
                .GroupBy(p => new { p.PaidAt!.Value.Year, p.PaidAt!.Value.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(p => p.Amount) })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync(cancellationToken);

            // ── Build full 6-month bucket list ────────────────────────────────
            var months = Enumerable.Range(0, 6)
                .Select(i => sixMonthsAgo.AddMonths(i))
                .Select(d => (d.Year, d.Month,
                    Label: d.ToString("MMM yyyy", CultureInfo.InvariantCulture)))
                .ToList();

            var userGrowth = months.Select(m => new MonthlyDataPointDto
            {
                Year = m.Year,
                Month = m.Month,
                Label = m.Label,
                Value = userGrowthRaw
                    .FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Count ?? 0
            }).ToList();

            var interviewActivity = months.Select(m => new MonthlyDataPointDto
            {
                Year = m.Year,
                Month = m.Month,
                Label = m.Label,
                Value = interviewActivityRaw
                    .FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Count ?? 0
            }).ToList();

            var revenueActivity = months.Select(m => new MonthlyDataPointDto
            {
                Year = m.Year,
                Month = m.Month,
                Label = m.Label,
                Value = revenueActivityRaw
                    .FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Amount ?? 0m
            }).ToList();

            return new AdminDashboardDto
            {
                TotalUsers = totalUsers,
                PremiumUsers = premiumUsers,
                FreeUsers = totalUsers - premiumUsers,
                ActiveUsers = activeUsers,
                LockedUsers = lockedUsers,
                TotalQuestions = totalQuestions,
                TotalInterviews = totalInterviews,
                MonthlyInterviews = monthlyInterviews,
                TotalRevenue = totalRevenue,
                MonthlyRevenue = monthlyRevenue,
                UserGrowth = userGrowth,
                InterviewActivity = interviewActivity,
                RevenueActivity = revenueActivity,
                GeneratedAt = now,
            };
        }
    }
}
