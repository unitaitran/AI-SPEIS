using ai_speis_be.Models;

namespace ai_speis_be.Repositories.PaymentRepo
{
    public interface IPaymentRepository
    {
        Task<Payment> CreateAsync(Payment payment, CancellationToken cancellationToken = default);
        Task<Payment?> GetByOrderCodeAsync(string orderCode, CancellationToken cancellationToken = default);
        Task<bool> ExistsByOrderCodeAsync(string orderCode, CancellationToken cancellationToken = default);
        Task<Payment> UpdateAsync(Payment payment, CancellationToken cancellationToken = default);
        Task<(List<Payment> Items, int TotalCount)> GetAdminPaginatedAsync(
            int page,
            int pageSize,
            Models.Enums.PaymentStatus? status,
            int? planId,
            DateTime? dateFrom,
            DateTime? dateTo,
            string? search,
            string? sortBy,
            CancellationToken cancellationToken = default);
        Task<Payment?> GetAdminDetailByIdAsync(int paymentId, CancellationToken cancellationToken = default);
        Task<List<Payment>> GetAdminRecentAsync(int count = 5, CancellationToken cancellationToken = default);
    }
}
