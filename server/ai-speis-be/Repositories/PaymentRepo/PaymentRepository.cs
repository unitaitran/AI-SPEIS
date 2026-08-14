using ai_speis_be.Models;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Repositories.PaymentRepo
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly ApplicationDbContext _context;

        public PaymentRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Payment> CreateAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            await _context.Payments.AddAsync(payment, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return payment;
        }

        public Task<Payment?> GetByOrderCodeAsync(string orderCode, CancellationToken cancellationToken = default)
        {
            return _context.Payments.FirstOrDefaultAsync(p => p.OrderCode == orderCode, cancellationToken);
        }

        public Task<bool> ExistsByOrderCodeAsync(string orderCode, CancellationToken cancellationToken = default)
        {
            return _context.Payments.AnyAsync(p => p.OrderCode == orderCode, cancellationToken);
        }

        public async Task<Payment> UpdateAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            _context.Payments.Update(payment);
            await _context.SaveChangesAsync(cancellationToken);
            return payment;
        }

        public async Task<(List<Payment> Items, int TotalCount)> GetAdminPaginatedAsync(
            int page,
            int pageSize,
            Models.Enums.PaymentStatus? status,
            int? planId,
            DateTime? dateFrom,
            DateTime? dateTo,
            string? search,
            string? sortBy,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Payments
                .Include(p => p.User)
                .Include(p => p.SubscriptionPrice!)
                    .ThenInclude(sp => sp.Plan)
                .AsNoTracking()
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(p => p.Status == status.Value);
            }

            if (planId.HasValue)
            {
                query = query.Where(p => p.SubscriptionPrice != null && p.SubscriptionPrice.PlanId == planId.Value);
            }

            if (dateFrom.HasValue)
            {
                query = query.Where(p => p.CreatedAt >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                query = query.Where(p => p.CreatedAt <= dateTo.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(p =>
                    p.OrderCode.ToLower().Contains(s) ||
                    (p.ProviderTransactionId != null && p.ProviderTransactionId.ToLower().Contains(s)) ||
                    p.User.Email.ToLower().Contains(s) ||
                    (p.User.FullName != null && p.User.FullName.ToLower().Contains(s)));
            }

            query = sortBy?.ToLower() switch
            {
                "oldest" => query.OrderBy(p => p.CreatedAt),
                "highestamount" => query.OrderByDescending(p => p.Amount),
                "lowestamount" => query.OrderBy(p => p.Amount),
                _ => query.OrderByDescending(p => p.CreatedAt) // "newest" default
            };

            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((Math.Max(1, page) - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public Task<Payment?> GetAdminDetailByIdAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            return _context.Payments
                .Include(p => p.User)
                .Include(p => p.SubscriptionPrice!)
                    .ThenInclude(sp => sp.Plan)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId, cancellationToken);
        }

        public Task<List<Payment>> GetAdminRecentAsync(int count = 5, CancellationToken cancellationToken = default)
        {
            return _context.Payments
                .Include(p => p.User)
                .Include(p => p.SubscriptionPrice!)
                    .ThenInclude(sp => sp.Plan)
                .AsNoTracking()
                .OrderByDescending(p => p.CreatedAt)
                .Take(count)
                .ToListAsync(cancellationToken);
        }
    }
}
