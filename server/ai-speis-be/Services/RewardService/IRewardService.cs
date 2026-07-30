namespace ai_speis_be.Services.RewardService;

public interface IRewardService
{
    Task<int> GetAvailablePointsAsync(int userId, CancellationToken cancellationToken = default);
    Task<int> AwardInterviewPointsAsync(int userId, int interviewCampaignId, decimal overallScore, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage, int ReservedPoints)> ReserveForPaymentAsync(int userId, int requestedPoints, string orderCode, decimal orderAmount, CancellationToken cancellationToken = default);
    Task RedeemPaymentReservationAsync(int userId, int points, string orderCode, CancellationToken cancellationToken = default);
    Task ReleasePaymentReservationAsync(int userId, int points, string orderCode, CancellationToken cancellationToken = default);
}
