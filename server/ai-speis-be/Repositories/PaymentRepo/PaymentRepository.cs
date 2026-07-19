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
    }
}
