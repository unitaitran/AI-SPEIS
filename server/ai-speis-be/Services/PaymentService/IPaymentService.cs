using ai_speis_be.Models.DTOs.Payment;

namespace ai_speis_be.Services.PaymentService
{
    public interface IPaymentService
    {
        Task<(bool Success, string? ErrorMessage, PaymentResponseDto? Payment)> CreatePaymentAsync(
            int userId,
            int priceId,
            bool useRewardPoints,
            CancellationToken cancellationToken = default);

        Task<(bool Success, string? ErrorMessage, PaymentCheckResponseDto? Payment)> CheckPaymentAsync(
            int userId,
            string orderCode,
            CancellationToken cancellationToken = default);

        Task<(bool Success, string? ErrorMessage)> HandleWebhookAsync(
            PaymentWebhookRequestDto webhook,
            CancellationToken cancellationToken = default);

        Task<(bool Success, string? ErrorMessage)> QueryTransactionStatusAsync(
            string orderCode,
            int? resultCode = null,
            CancellationToken cancellationToken = default);
    }
}
