using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Services.RewardService;

public class RewardService : IRewardService
{
    private const string CampaignReference = "InterviewCampaign";
    private const string PaymentReference = "Payment";
    private readonly ApplicationDbContext _context;

    public RewardService(ApplicationDbContext context) => _context = context;

    public async Task<int> GetAvailablePointsAsync(int userId, CancellationToken cancellationToken = default)
    {
        var account = await EnsureAccountAsync(userId, cancellationToken);
        return account.AvailablePoints;
    }

    public async Task<int> AwardInterviewPointsAsync(
        int userId,
        int interviewCampaignId,
        decimal overallScore,
        CancellationToken cancellationToken = default)
    {
        var referenceId = interviewCampaignId.ToString();
        var existing = await _context.RewardTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(transaction =>
                transaction.UserId == userId
                && transaction.Type == RewardTransactionType.Earn
                && transaction.ReferenceType == CampaignReference
                && transaction.ReferenceId == referenceId,
                cancellationToken);
        if (existing != null) return existing.Delta;

        var points = CalculateInterviewPoints(overallScore);
        var account = await EnsureAccountAsync(userId, cancellationToken);
        account.AvailablePoints += points;
        account.LifetimeEarnedPoints += points;
        _context.RewardTransactions.Add(new RewardTransaction
        {
            UserId = userId,
            Type = RewardTransactionType.Earn,
            Delta = points,
            ReferenceType = CampaignReference,
            ReferenceId = referenceId,
            Reason = $"Interview completed with overall score {overallScore:0.00}.",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);
        return points;
    }

    public async Task<(bool Success, string? ErrorMessage, int ReservedPoints)> ReserveForPaymentAsync(
        int userId,
        int requestedPoints,
        string orderCode,
        decimal orderAmount,
        CancellationToken cancellationToken = default)
    {
        if (requestedPoints < 0) return (false, "Số điểm sử dụng không hợp lệ.", 0);
        if (requestedPoints == 0) return (true, null, 0);

        var maximumByAmount = decimal.ToInt32(decimal.Floor(orderAmount));
        if (requestedPoints > maximumByAmount)
            return (false, "Số điểm không được vượt quá giá trị đơn hàng (1 điểm = 1 VND).", 0);

        var account = await EnsureAccountAsync(userId, cancellationToken);
        if (requestedPoints > account.AvailablePoints)
            return (false, "Số dư điểm thưởng không đủ.", 0);

        if (await HasTransactionAsync(userId, RewardTransactionType.Reserve, orderCode, cancellationToken))
            return (true, null, requestedPoints);

        account.AvailablePoints -= requestedPoints;
        account.ReservedPoints += requestedPoints;
        AddTransaction(userId, RewardTransactionType.Reserve, -requestedPoints, orderCode, "Reserve points for checkout.");
        return (true, null, requestedPoints);
    }

    public async Task RedeemPaymentReservationAsync(
        int userId,
        int points,
        string orderCode,
        CancellationToken cancellationToken = default)
    {
        if (points <= 0 || await HasTransactionAsync(userId, RewardTransactionType.Redeem, orderCode, cancellationToken)) return;
        var account = await EnsureAccountAsync(userId, cancellationToken);
        if (account.ReservedPoints < points) throw new InvalidOperationException("Reserved reward balance is inconsistent.");
        account.ReservedPoints -= points;
        AddTransaction(userId, RewardTransactionType.Redeem, -points, orderCode, "Redeem points after successful payment.");
    }

    public async Task ReleasePaymentReservationAsync(
        int userId,
        int points,
        string orderCode,
        CancellationToken cancellationToken = default)
    {
        if (points <= 0 || await HasTransactionAsync(userId, RewardTransactionType.Release, orderCode, cancellationToken)) return;
        var account = await EnsureAccountAsync(userId, cancellationToken);
        var releasable = Math.Min(points, account.ReservedPoints);
        account.ReservedPoints -= releasable;
        account.AvailablePoints += releasable;
        AddTransaction(userId, RewardTransactionType.Release, releasable, orderCode, "Release points from an unsuccessful payment.");
    }

    private static int CalculateInterviewPoints(decimal score) => Math.Clamp(score, 0m, 10m) switch
    {
        >= 9m and <= 10m => 300,
        >= 8m => 200,
        >= 7m => 100,
        _ => 50
    };

    private async Task<RewardAccount> EnsureAccountAsync(int userId, CancellationToken cancellationToken)
    {
        var account = await _context.RewardAccounts.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (account != null) return account;
        account = new RewardAccount { UserId = userId };
        _context.RewardAccounts.Add(account);
        return account;
    }

    private Task<bool> HasTransactionAsync(int userId, RewardTransactionType type, string referenceId, CancellationToken cancellationToken) =>
        _context.RewardTransactions.AnyAsync(transaction =>
            transaction.UserId == userId
            && transaction.Type == type
            && transaction.ReferenceType == PaymentReference
            && transaction.ReferenceId == referenceId,
            cancellationToken);

    private void AddTransaction(int userId, RewardTransactionType type, int delta, string referenceId, string reason) =>
        _context.RewardTransactions.Add(new RewardTransaction
        {
            UserId = userId,
            Type = type,
            Delta = delta,
            ReferenceType = PaymentReference,
            ReferenceId = referenceId,
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        });
}
