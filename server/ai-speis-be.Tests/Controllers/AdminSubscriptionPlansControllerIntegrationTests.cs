using ai_speis_be.Controllers;
using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.SubscriptionPlanService;
using ai_speis_be.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace ai_speis_be.Tests.Controllers
{
    public sealed class AdminSubscriptionPlansControllerIntegrationTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly AdminSubscriptionPlansController _controller;

        public AdminSubscriptionPlansControllerIntegrationTests()
        {
            _context = TestDbContextFactory.Create();
            var service = new SubscriptionPlanService(_context);
            _controller = new AdminSubscriptionPlansController(service);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Fact]
        public async Task PriceLifecycle_CreateUpdateDisableEnableAndView_WorksEndToEnd()
        {
            var createPlanResult = await _controller.CreatePlan(new CreateSubscriptionPlanRequestDto
            {
                Code = "PLAN-FLOW",
                Name = "Flow Plan",
                Description = "Flow test",
                InterviewQuota = 20,
                QuotaResetDays = 30,
                IsFree = false,
                DisplayOrder = 3,
            }, CancellationToken.None);

            var createdPlan = Assert.IsType<CreatedAtActionResult>(createPlanResult);
            var createdPlanDto = Assert.IsType<SubscriptionPlanDto>(createdPlan.Value);

            var createPriceResult = await _controller.CreatePrice(createdPlanDto.PlanId, new CreateSubscriptionPriceRequestDto
            {
                BillingCycle = BillingCycle.Monthly,
                BillingCycleCount = 1,
                Amount = 159000m,
                Currency = "VND",
                EffectiveFrom = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                EffectiveTo = null,
            }, CancellationToken.None);

            var createdPrice = Assert.IsType<OkObjectResult>(createPriceResult);
            var createdPriceDto = Assert.IsType<SubscriptionPriceDto>(createdPrice.Value);

            var updatePriceResult = await _controller.UpdatePrice(createdPriceDto.PriceId, new UpdateSubscriptionPriceRequestDto
            {
                BillingCycle = BillingCycle.Monthly,
                BillingCycleCount = 1,
                Amount = 199000m,
                Currency = "VND",
                EffectiveFrom = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                EffectiveTo = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            }, CancellationToken.None);

            var updatedPrice = Assert.IsType<OkObjectResult>(updatePriceResult);
            var updatedPriceDto = Assert.IsType<SubscriptionPriceDto>(updatedPrice.Value);
            Assert.Equal(199000m, updatedPriceDto.Amount);
            Assert.Equal(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), updatedPriceDto.EffectiveTo);

            var disableResult = await _controller.SetPriceStatus(updatedPriceDto.PriceId, new SetActiveRequestDto
            {
                IsActive = false,
            }, CancellationToken.None);
            Assert.IsType<NoContentResult>(disableResult);

            var enableResult = await _controller.SetPriceStatus(updatedPriceDto.PriceId, new SetActiveRequestDto
            {
                IsActive = true,
            }, CancellationToken.None);
            Assert.IsType<NoContentResult>(enableResult);

            var plansResult = await _controller.GetPlans(CancellationToken.None);
            var okPlans = Assert.IsType<OkObjectResult>(plansResult.Result);
            var plans = Assert.IsAssignableFrom<IReadOnlyList<SubscriptionPlanDto>>(okPlans.Value);
            var reloadedPlan = Assert.Single(plans, plan => plan.PlanId == createdPlanDto.PlanId);
            var reloadedPrice = Assert.Single(reloadedPlan.Prices);
            Assert.True(reloadedPrice.IsActive);
            Assert.Equal(199000m, reloadedPrice.Amount);
        }

        [Fact]
        public async Task UpdatePlan_WorksEndToEnd()
        {
            var createPlanResult = await _controller.CreatePlan(new CreateSubscriptionPlanRequestDto
            {
                Code = "PLAN-UPDATE",
                Name = "Old Plan",
                Description = "Old description",
                InterviewQuota = 10,
                QuotaResetDays = 30,
                IsFree = false,
                DisplayOrder = 1,
                AiTier = "STANDARD",
                AdvancedAnalyticsEnabled = false,
                IsPopular = false,
                IsActive = true,
            }, CancellationToken.None);

            var createdPlan = Assert.IsType<CreatedAtActionResult>(createPlanResult);
            var plan = Assert.IsType<SubscriptionPlanDto>(createdPlan.Value);

            var updateResult = await _controller.UpdatePlan(plan.PlanId, new UpdateSubscriptionPlanRequestDto
            {
                Code = "PLAN-UPDATE",
                Name = "Updated Plan",
                Description = "Updated description",
                InterviewQuota = 25,
                QuotaResetDays = 60,
                IsFree = false,
                DisplayOrder = 2,
                AiTier = "ENTERPRISE",
                AdvancedAnalyticsEnabled = true,
                IsPopular = true,
                IsActive = false,
            }, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(updateResult);
            var updated = Assert.IsType<SubscriptionPlanDto>(ok.Value);
            Assert.Equal("Updated Plan", updated.Name);
            Assert.Equal(25, updated.InterviewQuota);
            Assert.Equal(60, updated.QuotaResetDays);
            Assert.Equal(2, updated.DisplayOrder);
            Assert.Equal("ENTERPRISE", updated.AiTier);
            Assert.True(updated.AdvancedAnalyticsEnabled);
            Assert.True(updated.IsPopular);
            Assert.False(updated.IsActive);

            var plansResult = await _controller.GetPlans(CancellationToken.None);
            var okPlans = Assert.IsType<OkObjectResult>(plansResult.Result);
            var plans = Assert.IsAssignableFrom<IReadOnlyList<SubscriptionPlanDto>>(okPlans.Value);
            var reloaded = Assert.Single(plans, item => item.PlanId == plan.PlanId);
            Assert.Equal("ENTERPRISE", reloaded.AiTier);
            Assert.True(reloaded.AdvancedAnalyticsEnabled);
            Assert.True(reloaded.IsPopular);
            Assert.False(reloaded.IsActive);
        }

        [Fact]
        public async Task DeletePlan_WithoutHistory_ReturnsNoContent()
        {
            var createPlanResult = await _controller.CreatePlan(new CreateSubscriptionPlanRequestDto
            {
                Code = "PLAN-DELETE",
                Name = "Delete Me",
                Description = "Temporary",
                InterviewQuota = 5,
                QuotaResetDays = 30,
                IsFree = false,
                DisplayOrder = 9,
            }, CancellationToken.None);

            var createdPlan = Assert.IsType<CreatedAtActionResult>(createPlanResult);
            var plan = Assert.IsType<SubscriptionPlanDto>(createdPlan.Value);

            var deleteResult = await _controller.DeletePlan(plan.PlanId, CancellationToken.None);

            Assert.IsType<NoContentResult>(deleteResult);
            Assert.DoesNotContain(_context.SubscriptionPlans, item => item.PlanId == plan.PlanId);
        }

        [Fact]
        public async Task DeletePlan_WithPaymentHistory_ReturnsBusinessError()
        {
            var createPlanResult = await _controller.CreatePlan(new CreateSubscriptionPlanRequestDto
            {
                Code = "PLAN-HISTORY",
                Name = "History Plan",
                Description = "Protected",
                InterviewQuota = 20,
                QuotaResetDays = 30,
                IsFree = false,
                DisplayOrder = 5,
            }, CancellationToken.None);

            var createdPlan = Assert.IsType<CreatedAtActionResult>(createPlanResult);
            var plan = Assert.IsType<SubscriptionPlanDto>(createdPlan.Value);

            var createPriceResult = await _controller.CreatePrice(plan.PlanId, new CreateSubscriptionPriceRequestDto
            {
                BillingCycle = BillingCycle.Monthly,
                BillingCycleCount = 1,
                Amount = 159000m,
                Currency = "VND",
                EffectiveFrom = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                EffectiveTo = null,
            }, CancellationToken.None);
            var createdPrice = Assert.IsType<OkObjectResult>(createPriceResult);
            var price = Assert.IsType<SubscriptionPriceDto>(createdPrice.Value);

            _context.Payments.Add(new Payment
            {
                UserId = 1,
                PackageId = plan.PlanId,
                PriceId = price.PriceId,
                Amount = 159000m,
                OriginalAmount = 159000m,
                DiscountAmount = 0m,
                RewardPointsUsed = 0,
                Currency = "VND",
                OrderCode = "ORDER-PLAN-HISTORY",
                Status = PaymentStatus.Paid,
                CreatedAt = DateTime.UtcNow,
                PaidAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var deleteResult = await _controller.DeletePlan(plan.PlanId, CancellationToken.None);

            var badRequest = Assert.IsType<BadRequestObjectResult>(deleteResult);
            var payload = System.Text.Json.JsonSerializer.Serialize(badRequest.Value);
            Assert.Contains("INVALID_PLAN_DELETE", payload, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("payment history", payload, StringComparison.OrdinalIgnoreCase);
        }
    }
}