namespace ai_speis_be.Services.SubscriptionService;

public interface ISubscriptionMaintenanceService
{
    Task<int> ReconcileExpiredEntitlementsAsync(DateTime now, CancellationToken cancellationToken = default);
    Task<int> SynchronizeQuotaPeriodsAsync(DateTime now, CancellationToken cancellationToken = default);
}
