using ai_speis_be.Models;

namespace ai_speis_be.Repositories.PaymentRepo
{
    public interface IPaymentRepository
    {
        Task<Payment> CreateAsync(Payment payment, CancellationToken cancellationToken = default);
        Task<Payment?> GetByOrderCodeAsync(string orderCode, CancellationToken cancellationToken = default);
        Task<bool> ExistsByOrderCodeAsync(string orderCode, CancellationToken cancellationToken = default);
        Task<Payment> UpdateAsync(Payment payment, CancellationToken cancellationToken = default);
    }
}
