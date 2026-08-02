using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Services.SubscriptionPlanService
{
    public sealed class SubscriptionPlanService : ISubscriptionPlanService
    {
        private readonly ApplicationDbContext _context;

        public SubscriptionPlanService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<SubscriptionPlanDto>> GetPublicPlansAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var plans = await _context.SubscriptionPlans
                .AsNoTracking()
                .Where(plan => plan.IsActive)
                .Include(plan => plan.Features.Where(feature => feature.IsEnabled))
                .Include(plan => plan.Prices.Where(price => price.IsActive
                    && price.EffectiveFrom <= now
                    && (!price.EffectiveTo.HasValue || price.EffectiveTo.Value > now)))
                .OrderBy(plan => plan.DisplayOrder)
                .ThenBy(plan => plan.PlanId)
                .ToListAsync(cancellationToken);

            return plans.Select(MapPlan).ToList();
        }

        public async Task<IReadOnlyList<SubscriptionPlanDto>> GetAdminPlansAsync(CancellationToken cancellationToken = default)
        {
            var plans = await _context.SubscriptionPlans
                .AsNoTracking()
                .Include(plan => plan.Features)
                .Include(plan => plan.Prices)
                .OrderBy(plan => plan.DisplayOrder)
                .ThenBy(plan => plan.PlanId)
                .ToListAsync(cancellationToken);

            return plans.Select(MapPlan).ToList();
        }

        public async Task<(bool Success, string? Error, SubscriptionPlanDto? Plan)> CreatePlanAsync(
            CreateSubscriptionPlanRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var code = NormalizeCode(request.Code);
            if (await _context.SubscriptionPlans.AnyAsync(plan => plan.Code == code, cancellationToken))
                return (false, "Mã gói đã tồn tại.", null);
            if (request.IsFree && request.QuotaResetDays.HasValue)
                return (false, "Gói Free không được cấu hình chu kỳ reset quota.", null);

            var plan = new SubscriptionPlan
            {
                Code = code,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                InterviewQuota = request.InterviewQuota,
                QuotaResetDays = request.QuotaResetDays,
                IsFree = request.IsFree,
                DisplayOrder = request.DisplayOrder,
                IsActive = request.IsActive ?? true,
                AiTier = NormalizeAiTier(request.AiTier),
                AdvancedAnalyticsEnabled = request.AdvancedAnalyticsEnabled ?? false,
                IsPopular = request.IsPopular ?? false,
                CreatedAt = DateTime.UtcNow
            };
            _context.SubscriptionPlans.Add(plan);
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null, MapPlan(plan));
        }

        public async Task<(bool Success, string? Error, SubscriptionPlanDto? Plan)> UpdatePlanAsync(
            int planId,
            UpdateSubscriptionPlanRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var plan = await _context.SubscriptionPlans
                .Include(item => item.Prices)
                .Include(item => item.Features)
                .FirstOrDefaultAsync(item => item.PlanId == planId, cancellationToken);
            if (plan is null) return (false, "Không tìm thấy gói.", null);

            var code = NormalizeCode(request.Code);
            if (await _context.SubscriptionPlans.AnyAsync(item => item.PlanId != planId && item.Code == code, cancellationToken))
                return (false, "Mã gói đã tồn tại.", null);
            if (request.IsFree && request.QuotaResetDays.HasValue)
                return (false, "Gói Free không được cấu hình chu kỳ reset quota.", null);

            plan.Code = code;
            plan.Name = request.Name.Trim();
            plan.Description = request.Description?.Trim();
            plan.InterviewQuota = request.InterviewQuota;
            plan.QuotaResetDays = request.QuotaResetDays;
            plan.IsFree = request.IsFree;
            plan.DisplayOrder = request.DisplayOrder;
            plan.AiTier = NormalizeAiTier(request.AiTier);
            plan.AdvancedAnalyticsEnabled = request.AdvancedAnalyticsEnabled ?? plan.AdvancedAnalyticsEnabled;
            plan.IsPopular = request.IsPopular ?? plan.IsPopular;
            if (request.IsActive.HasValue)
            {
                plan.IsActive = request.IsActive.Value;
            }
            plan.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null, MapPlan(plan));
        }

        public async Task<(bool Success, string? Error)> DeletePlanAsync(int planId, CancellationToken cancellationToken = default)
        {
            var plan = await _context.SubscriptionPlans
                .Include(item => item.Prices)
                .Include(item => item.Features)
                .FirstOrDefaultAsync(item => item.PlanId == planId, cancellationToken);
            if (plan is null) return (false, "Không tìm thấy gói.");
            if (plan.IsFree) return (false, "Không thể xóa gói Free mặc định.");

            var planPriceIds = plan.Prices.Select(price => price.PriceId).ToList();
            var hasPaymentHistory = planPriceIds.Count > 0 && await _context.Payments.AnyAsync(payment =>
                payment.PriceId.HasValue && planPriceIds.Contains(payment.PriceId.Value), cancellationToken);
            if (hasPaymentHistory)
                return (false, "This subscription plan has payment history and cannot be deleted.");

            var hasSubscriptionHistory = await _context.UserSubscriptions.AnyAsync(subscription => subscription.PlanId == planId, cancellationToken)
                || (planPriceIds.Count > 0 && await _context.SubscriptionTerms.AnyAsync(term => planPriceIds.Contains(term.PriceId), cancellationToken));
            if (hasSubscriptionHistory)
                return (false, "This subscription plan has subscription history and cannot be deleted.");

            await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            _context.PlanFeatures.RemoveRange(plan.Features);
            _context.SubscriptionPrices.RemoveRange(plan.Prices);
            _context.SubscriptionPlans.Remove(plan);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> SetPlanActiveAsync(int planId, bool isActive, CancellationToken cancellationToken = default)
        {
            var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(item => item.PlanId == planId, cancellationToken);
            if (plan is null) return (false, "Không tìm thấy gói.");
            if (plan.IsFree && !isActive)
                return (false, "Không thể vô hiệu hóa gói Free mặc định.");

            plan.IsActive = isActive;
            plan.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null);
        }

        public async Task<(bool Success, SubscriptionPriceValidationErrorDto? Error, SubscriptionPriceDto? Price)> CreatePriceAsync(
            int planId,
            CreateSubscriptionPriceRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (!await _context.SubscriptionPlans.AnyAsync(plan => plan.PlanId == planId, cancellationToken))
                return (false, new SubscriptionPriceValidationErrorDto { Message = "Không tìm thấy gói." }, null);
            var validationError = await ValidatePriceAsync(planId, null, request, cancellationToken);
            if (validationError is not null) return (false, validationError, null);

            var price = new SubscriptionPrice
            {
                PlanId = planId,
                BillingCycle = request.BillingCycle,
                BillingCycleCount = request.BillingCycleCount,
                Amount = request.Amount,
                Currency = request.Currency.Trim().ToUpperInvariant(),
                EffectiveFrom = AsUtc(request.EffectiveFrom),
                EffectiveTo = request.EffectiveTo.HasValue ? AsUtc(request.EffectiveTo.Value) : null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.SubscriptionPrices.Add(price);
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null, MapPrice(price));
        }

        public async Task<(bool Success, SubscriptionPriceValidationErrorDto? Error, SubscriptionPriceDto? Price)> UpdatePriceAsync(
            int priceId,
            UpdateSubscriptionPriceRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var price = await _context.SubscriptionPrices.FirstOrDefaultAsync(item => item.PriceId == priceId, cancellationToken);
            if (price is null) return (false, new SubscriptionPriceValidationErrorDto { Message = "Không tìm thấy giá gói." }, null);
            var validationError = await ValidatePriceAsync(price.PlanId, priceId, request, cancellationToken);
            if (validationError is not null) return (false, validationError, null);

            price.BillingCycle = request.BillingCycle;
            price.BillingCycleCount = request.BillingCycleCount;
            price.Amount = request.Amount;
            price.Currency = request.Currency.Trim().ToUpperInvariant();
            price.EffectiveFrom = AsUtc(request.EffectiveFrom);
            price.EffectiveTo = request.EffectiveTo.HasValue ? AsUtc(request.EffectiveTo.Value) : null;
            price.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null, MapPrice(price));
        }

        public async Task<(bool Success, string? Error)> SetPriceActiveAsync(int priceId, bool isActive, CancellationToken cancellationToken = default)
        {
            var price = await _context.SubscriptionPrices.FirstOrDefaultAsync(item => item.PriceId == priceId, cancellationToken);
            if (price is null) return (false, "Không tìm thấy giá gói.");
            price.IsActive = isActive;
            price.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null);
        }

        private async Task<SubscriptionPriceValidationErrorDto?> ValidatePriceAsync(
            int planId,
            int? priceId,
            CreateSubscriptionPriceRequestDto request,
            CancellationToken cancellationToken)
        {
            if (!Enum.IsDefined(request.BillingCycle))
            {
                return new SubscriptionPriceValidationErrorDto
                {
                    Field = nameof(CreateSubscriptionPriceRequestDto.BillingCycle),
                    Message = "Chu kỳ thanh toán không hợp lệ.",
                };
            }

            var start = AsUtc(request.EffectiveFrom);
            var end = request.EffectiveTo.HasValue ? AsUtc(request.EffectiveTo.Value) : (DateTime?)null;
            if (end.HasValue && end.Value <= start)
            {
                return new SubscriptionPriceValidationErrorDto
                {
                    Field = nameof(CreateSubscriptionPriceRequestDto.EffectiveTo),
                    Message = "Ngày kết thúc hiệu lực phải sau ngày bắt đầu.",
                };
            }

            var candidatePrices = _context.SubscriptionPrices.Where(price =>
                price.PlanId == planId
                && price.BillingCycle == request.BillingCycle
                && price.IsActive);

            if (priceId.HasValue)
            {
                candidatePrices = candidatePrices.Where(price => price.PriceId != priceId.Value);
            }

            var conflict = await candidatePrices
                .OrderBy(price => price.EffectiveFrom)
                .FirstOrDefaultAsync(price =>
                    (!price.EffectiveTo.HasValue || price.EffectiveTo.Value > start)
                    && (!end.HasValue || price.EffectiveFrom < end.Value), cancellationToken);

            return conflict is null
                ? null
                : new SubscriptionPriceValidationErrorDto
                {
                    Field = nameof(CreateSubscriptionPriceRequestDto.EffectiveFrom),
                    Message = "Khoảng hiệu lực giá bị trùng với một giá đang hoạt động.",
                    ConflictPriceId = conflict.PriceId,
                    ConflictBillingCycle = conflict.BillingCycle,
                    ConflictEffectiveFrom = conflict.EffectiveFrom,
                    ConflictEffectiveTo = conflict.EffectiveTo,
                };
        }

        private static SubscriptionPlanDto MapPlan(SubscriptionPlan plan) => new()
        {
            PlanId = plan.PlanId,
            Code = plan.Code,
            Name = plan.Name,
            Description = plan.Description,
            InterviewQuota = plan.InterviewQuota,
            QuotaResetDays = plan.QuotaResetDays,
            IsFree = plan.IsFree,
            DisplayOrder = plan.DisplayOrder,
            IsActive = plan.IsActive,
            AiTier = plan.AiTier,
            AdvancedAnalyticsEnabled = plan.AdvancedAnalyticsEnabled,
            IsPopular = plan.IsPopular,
            Prices = plan.Prices.OrderBy(price => price.BillingCycle).ThenBy(price => price.EffectiveFrom).Select(MapPrice).ToList(),
            Features = plan.Features.OrderBy(feature => feature.DisplayOrder).Select(feature => new PlanFeatureDto
            {
                PlanFeatureId = feature.PlanFeatureId,
                FeatureCode = feature.FeatureCode,
                LimitValue = feature.LimitValue,
                DisplayOrder = feature.DisplayOrder,
                IsEnabled = feature.IsEnabled
            }).ToList()
        };

        private static SubscriptionPriceDto MapPrice(SubscriptionPrice price) => new()
        {
            PriceId = price.PriceId,
            BillingCycle = price.BillingCycle,
            BillingCycleCount = price.BillingCycleCount,
            Amount = price.Amount,
            Currency = price.Currency,
            EffectiveFrom = price.EffectiveFrom,
            EffectiveTo = price.EffectiveTo,
            IsActive = price.IsActive
        };

        private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
        private static string NormalizeAiTier(string? aiTier)
        {
            var normalized = string.IsNullOrWhiteSpace(aiTier) ? "ADVANCED" : aiTier.Trim().ToUpperInvariant();
            return normalized is "STANDARD" or "ADVANCED" or "ENTERPRISE" ? normalized : "ADVANCED";
        }
        private static DateTime AsUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }
}
