using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Services.SubscriptionPlanService
{
    public interface ISubscriptionPlanService
    {
        Task<IReadOnlyList<SubscriptionPlanDto>> GetPublicPlansAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SubscriptionPlanDto>> GetAdminPlansAsync(CancellationToken cancellationToken = default);
        Task<(bool Success, string? Error, SubscriptionPlanDto? Plan)> CreatePlanAsync(CreateSubscriptionPlanRequestDto request, CancellationToken cancellationToken = default);
        Task<(bool Success, string? Error, SubscriptionPlanDto? Plan)> UpdatePlanAsync(int planId, UpdateSubscriptionPlanRequestDto request, CancellationToken cancellationToken = default);
        Task<(bool Success, string? Error)> SetPlanActiveAsync(int planId, bool isActive, CancellationToken cancellationToken = default);
        Task<(bool Success, string? Error, SubscriptionPriceDto? Price)> CreatePriceAsync(int planId, CreateSubscriptionPriceRequestDto request, CancellationToken cancellationToken = default);
        Task<(bool Success, string? Error, SubscriptionPriceDto? Price)> UpdatePriceAsync(int priceId, UpdateSubscriptionPriceRequestDto request, CancellationToken cancellationToken = default);
        Task<(bool Success, string? Error)> SetPriceActiveAsync(int priceId, bool isActive, CancellationToken cancellationToken = default);
    }
}
