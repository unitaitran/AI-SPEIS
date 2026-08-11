using ai_speis_be.Models.DTOs.AdminPayment;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.Services.AdminPaymentService
{
    public interface IAdminPaymentService
    {
        Task<PaginatedResultDto<PaymentListDto>> GetPaymentsAsync(
            int page,
            int pageSize,
            PaymentStatus? status,
            int? planId,
            DateTime? dateFrom,
            DateTime? dateTo,
            string? search,
            string? sortBy,
            CancellationToken cancellationToken = default);

        Task<PaymentDetailDto?> GetPaymentDetailAsync(int paymentId, CancellationToken cancellationToken = default);

        Task<PaymentStatisticsDto> GetStatisticsAsync(CancellationToken cancellationToken = default);

        Task<List<PaymentListDto>> GetRecentPaymentsAsync(int count = 5, CancellationToken cancellationToken = default);

        Task<(bool Success, string Message, PaymentDetailDto? Detail)> VerifyPaymentWithMoMoAsync(int paymentId, CancellationToken cancellationToken = default);

        Task<byte[]> ExportPaymentsExcelAsync(
            PaymentStatus? status,
            int? planId,
            DateTime? dateFrom,
            DateTime? dateTo,
            string? search,
            string? sortBy,
            CancellationToken cancellationToken = default);
    }
}
