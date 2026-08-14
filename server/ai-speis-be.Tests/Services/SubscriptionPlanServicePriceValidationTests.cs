using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.SubscriptionPlanService;
using ai_speis_be.Tests.Helpers;

namespace ai_speis_be.Tests.Services
{
    public sealed class SubscriptionPlanServicePriceValidationTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly SubscriptionPlanService _service;

        public SubscriptionPlanServicePriceValidationTests()
        {
            _context = TestDbContextFactory.Create();
            _service = new SubscriptionPlanService(_context);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Fact]
        public async Task UpdatePriceAsync_SameRecordSameWindow_DoesNotCountAsOverlap()
        {
            var plan = new SubscriptionPlan
            {
                Code = "PREM-MONTH",
                Name = "Premium Monthly",
                InterviewQuota = 15,
                DisplayOrder = 1,
                IsFree = false,
                IsActive = true,
            };

            _context.SubscriptionPlans.Add(plan);
            await _context.SaveChangesAsync();

            var price = new SubscriptionPrice
            {
                PlanId = plan.PlanId,
                BillingCycle = BillingCycle.Monthly,
                BillingCycleCount = 1,
                Amount = 159000m,
                Currency = "VND",
                EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EffectiveTo = null,
                IsActive = true,
            };

            _context.SubscriptionPrices.Add(price);
            await _context.SaveChangesAsync();

            var request = new UpdateSubscriptionPriceRequestDto
            {
                BillingCycle = BillingCycle.Monthly,
                BillingCycleCount = 1,
                Amount = 199000m,
                Currency = "VND",
                EffectiveFrom = price.EffectiveFrom,
                EffectiveTo = price.EffectiveTo,
            };

            var result = await _service.UpdatePriceAsync(price.PriceId, request);

            Assert.True(result.Success);
            Assert.Null(result.Error);
            Assert.NotNull(result.Price);
            Assert.Equal(199000m, result.Price.Amount);
        }

        [Fact]
        public async Task CreatePriceAsync_OverlappingActivePrice_ReturnsInvalidPrice()
        {
            var plan = new SubscriptionPlan
            {
                Code = "PREM-YEAR",
                Name = "Premium Yearly",
                InterviewQuota = 15,
                DisplayOrder = 1,
                IsFree = false,
                IsActive = true,
            };

            _context.SubscriptionPlans.Add(plan);
            await _context.SaveChangesAsync();

            _context.SubscriptionPrices.Add(new SubscriptionPrice
            {
                PlanId = plan.PlanId,
                BillingCycle = BillingCycle.Yearly,
                BillingCycleCount = 1,
                Amount = 999000m,
                Currency = "VND",
                EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EffectiveTo = null,
                IsActive = true,
            });
            await _context.SaveChangesAsync();

            var request = new CreateSubscriptionPriceRequestDto
            {
                BillingCycle = BillingCycle.Yearly,
                BillingCycleCount = 1,
                Amount = 1099000m,
                Currency = "VND",
                EffectiveFrom = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                EffectiveTo = null,
            };

            var result = await _service.CreatePriceAsync(plan.PlanId, request);

            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.Equal("Khoảng hiệu lực giá bị trùng với một giá đang hoạt động.", result.Error.Message);
            Assert.Equal(nameof(CreateSubscriptionPriceRequestDto.EffectiveFrom), result.Error.Field);
        }

        [Fact]
        public async Task CreatePriceAsync_OverlappingInactivePrice_IsAllowed()
        {
            var plan = new SubscriptionPlan
            {
                Code = "PREM-ALT",
                Name = "Premium Alternative",
                InterviewQuota = 15,
                DisplayOrder = 1,
                IsFree = false,
                IsActive = true,
            };

            _context.SubscriptionPlans.Add(plan);
            await _context.SaveChangesAsync();

            _context.SubscriptionPrices.Add(new SubscriptionPrice
            {
                PlanId = plan.PlanId,
                BillingCycle = BillingCycle.Monthly,
                BillingCycleCount = 1,
                Amount = 49000m,
                Currency = "VND",
                EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EffectiveTo = null,
                IsActive = false,
            });
            await _context.SaveChangesAsync();

            var request = new CreateSubscriptionPriceRequestDto
            {
                BillingCycle = BillingCycle.Monthly,
                BillingCycleCount = 1,
                Amount = 59000m,
                Currency = "VND",
                EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EffectiveTo = null,
            };

            var result = await _service.CreatePriceAsync(plan.PlanId, request);

            Assert.True(result.Success);
            Assert.Null(result.Error);
        }

        [Fact]
        public async Task CreatePriceAsync_AdjacentBoundaryDate_DoesNotOverlap()
        {
            var plan = new SubscriptionPlan
            {
                Code = "PREM-BOUNDARY",
                Name = "Boundary Plan",
                InterviewQuota = 15,
                DisplayOrder = 1,
                IsFree = false,
                IsActive = true,
            };

            _context.SubscriptionPlans.Add(plan);
            await _context.SaveChangesAsync();

            _context.SubscriptionPrices.Add(new SubscriptionPrice
            {
                PlanId = plan.PlanId,
                BillingCycle = BillingCycle.Monthly,
                BillingCycleCount = 1,
                Amount = 59000m,
                Currency = "VND",
                EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EffectiveTo = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
            });
            await _context.SaveChangesAsync();

            var result = await _service.CreatePriceAsync(plan.PlanId, new CreateSubscriptionPriceRequestDto
            {
                BillingCycle = BillingCycle.Monthly,
                BillingCycleCount = 1,
                Amount = 69000m,
                Currency = "VND",
                EffectiveFrom = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                EffectiveTo = null,
            });

            Assert.True(result.Success);
            Assert.Null(result.Error);
        }

        [Fact]
        public async Task CreatePriceAsync_DifferentBillingCycle_DoesNotOverlap()
        {
            var plan = new SubscriptionPlan
            {
                Code = "PREM-DIFF-CYCLE",
                Name = "Different Cycle Plan",
                InterviewQuota = 15,
                DisplayOrder = 1,
                IsFree = false,
                IsActive = true,
            };

            _context.SubscriptionPlans.Add(plan);
            await _context.SaveChangesAsync();

            _context.SubscriptionPrices.Add(new SubscriptionPrice
            {
                PlanId = plan.PlanId,
                BillingCycle = BillingCycle.Monthly,
                BillingCycleCount = 1,
                Amount = 59000m,
                Currency = "VND",
                EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EffectiveTo = null,
                IsActive = true,
            });
            await _context.SaveChangesAsync();

            var result = await _service.CreatePriceAsync(plan.PlanId, new CreateSubscriptionPriceRequestDto
            {
                BillingCycle = BillingCycle.Yearly,
                BillingCycleCount = 1,
                Amount = 599000m,
                Currency = "VND",
                EffectiveFrom = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                EffectiveTo = null,
            });

            Assert.True(result.Success);
            Assert.Null(result.Error);
        }

        [Fact]
        public async Task CreatePriceAsync_ExpiredHistoricalRangeWithoutOverlap_IsAllowed()
        {
            var plan = new SubscriptionPlan
            {
                Code = "PREM-EXPIRED",
                Name = "Expired Plan",
                InterviewQuota = 15,
                DisplayOrder = 1,
                IsFree = false,
                IsActive = true,
            };

            _context.SubscriptionPlans.Add(plan);
            await _context.SaveChangesAsync();

            _context.SubscriptionPrices.Add(new SubscriptionPrice
            {
                PlanId = plan.PlanId,
                BillingCycle = BillingCycle.Monthly,
                BillingCycleCount = 1,
                Amount = 49000m,
                Currency = "VND",
                EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EffectiveTo = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
            });
            await _context.SaveChangesAsync();

            var result = await _service.CreatePriceAsync(plan.PlanId, new CreateSubscriptionPriceRequestDto
            {
                BillingCycle = BillingCycle.Monthly,
                BillingCycleCount = 1,
                Amount = 59000m,
                Currency = "VND",
                EffectiveFrom = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                EffectiveTo = null,
            });

            Assert.True(result.Success);
            Assert.Null(result.Error);
        }

        [Fact]
        public async Task CreatePriceAsync_ClosedRangeOverlap_ReturnsConflictMetadata()
        {
            var plan = new SubscriptionPlan
            {
                Code = "PREM-CLOSED",
                Name = "Closed Range Plan",
                InterviewQuota = 15,
                DisplayOrder = 1,
                IsFree = false,
                IsActive = true,
            };

            _context.SubscriptionPlans.Add(plan);
            await _context.SaveChangesAsync();

            var existingPrice = new SubscriptionPrice
            {
                PlanId = plan.PlanId,
                BillingCycle = BillingCycle.Monthly,
                BillingCycleCount = 1,
                Amount = 59000m,
                Currency = "VND",
                EffectiveFrom = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                EffectiveTo = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
            };
            _context.SubscriptionPrices.Add(existingPrice);
            await _context.SaveChangesAsync();

            var result = await _service.CreatePriceAsync(plan.PlanId, new CreateSubscriptionPriceRequestDto
            {
                BillingCycle = BillingCycle.Monthly,
                BillingCycleCount = 1,
                Amount = 69000m,
                Currency = "USD",
                EffectiveFrom = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
                EffectiveTo = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            });

            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.Equal(existingPrice.PriceId, result.Error.ConflictPriceId);
            Assert.Equal(BillingCycle.Monthly, result.Error.ConflictBillingCycle);
            Assert.Equal(existingPrice.EffectiveFrom, result.Error.ConflictEffectiveFrom);
            Assert.Equal(existingPrice.EffectiveTo, result.Error.ConflictEffectiveTo);
        }
    }
}