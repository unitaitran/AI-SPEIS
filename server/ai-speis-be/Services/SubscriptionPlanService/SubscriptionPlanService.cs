using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
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
                IsActive = true,
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
            plan.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null, MapPlan(plan));
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

        public async Task<(bool Success, string? Error, SubscriptionPriceDto? Price)> CreatePriceAsync(
            int planId,
            CreateSubscriptionPriceRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (!await _context.SubscriptionPlans.AnyAsync(plan => plan.PlanId == planId, cancellationToken))
                return (false, "Không tìm thấy gói.", null);
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

        public async Task<(bool Success, string? Error, SubscriptionPriceDto? Price)> UpdatePriceAsync(
            int priceId,
            UpdateSubscriptionPriceRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var price = await _context.SubscriptionPrices.FirstOrDefaultAsync(item => item.PriceId == priceId, cancellationToken);
            if (price is null) return (false, "Không tìm thấy giá gói.", null);
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

        private async Task<string?> ValidatePriceAsync(
            int planId,
            int? priceId,
            CreateSubscriptionPriceRequestDto request,
            CancellationToken cancellationToken)
        {
            if (!Enum.IsDefined(request.BillingCycle)) return "Chu kỳ thanh toán không hợp lệ.";
            var start = AsUtc(request.EffectiveFrom);
            var end = request.EffectiveTo.HasValue ? AsUtc(request.EffectiveTo.Value) : (DateTime?)null;
            if (end.HasValue && end.Value <= start) return "Ngày kết thúc hiệu lực phải sau ngày bắt đầu.";

            var overlaps = await _context.SubscriptionPrices.AnyAsync(price =>
                price.PlanId == planId
                && price.PriceId != priceId
                && price.BillingCycle == request.BillingCycle
                && price.IsActive
                && (!price.EffectiveTo.HasValue || price.EffectiveTo.Value > start)
                && (!end.HasValue || price.EffectiveFrom < end.Value), cancellationToken);
            return overlaps ? "Khoảng hiệu lực giá bị trùng với một giá đang hoạt động." : null;
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
        private static DateTime AsUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }
}
