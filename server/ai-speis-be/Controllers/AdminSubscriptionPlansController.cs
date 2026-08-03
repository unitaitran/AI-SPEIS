using ai_speis_be.Models.DTOs;
using ai_speis_be.Services.SubscriptionPlanService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ai_speis_be.Controllers
{
    [ApiController]
    [Route("api/admin/subscription-plans")]
    [Authorize(Roles = "admin,Admin")]
    public sealed class AdminSubscriptionPlansController : ControllerBase
    {
        private readonly ISubscriptionPlanService _service;

        public AdminSubscriptionPlansController(ISubscriptionPlanService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<SubscriptionPlanDto>>> GetPlans(CancellationToken cancellationToken)
            => Ok(await _service.GetAdminPlansAsync(cancellationToken));

        [HttpPost]
        public async Task<IActionResult> CreatePlan(CreateSubscriptionPlanRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _service.CreatePlanAsync(request, cancellationToken);
            return result.Success ? CreatedAtAction(nameof(GetPlans), result.Plan) : BadRequest(new { code = "INVALID_PLAN", message = result.Error });
        }

        [HttpPut("{planId:int}")]
        public async Task<IActionResult> UpdatePlan(int planId, UpdateSubscriptionPlanRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _service.UpdatePlanAsync(planId, request, cancellationToken);
            return result.Success ? Ok(result.Plan) : BadRequest(new { code = "INVALID_PLAN", message = result.Error });
        }

        [HttpDelete("{planId:int}")]
        public async Task<IActionResult> DeletePlan(int planId, CancellationToken cancellationToken)
        {
            var result = await _service.DeletePlanAsync(planId, cancellationToken);
            return result.Success ? NoContent() : BadRequest(new { code = "INVALID_PLAN_DELETE", message = result.Error });
        }

        [HttpPatch("{planId:int}/status")]
        public async Task<IActionResult> SetPlanStatus(int planId, SetActiveRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _service.SetPlanActiveAsync(planId, request.IsActive, cancellationToken);
            return result.Success ? NoContent() : BadRequest(new { code = "INVALID_PLAN_STATUS", message = result.Error });
        }

        [HttpPost("{planId:int}/prices")]
        public async Task<IActionResult> CreatePrice(int planId, CreateSubscriptionPriceRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _service.CreatePriceAsync(planId, request, cancellationToken);
            return result.Success ? Ok(result.Price) : BadRequest(new
            {
                code = "INVALID_PRICE",
                message = result.Error?.Message,
                field = result.Error?.Field,
                conflictPriceId = result.Error?.ConflictPriceId,
                conflictBillingCycle = result.Error?.ConflictBillingCycle?.ToString(),
                conflictEffectiveFrom = result.Error?.ConflictEffectiveFrom,
                conflictEffectiveTo = result.Error?.ConflictEffectiveTo,
            });
        }

        [HttpPut("prices/{priceId:int}")]
        public async Task<IActionResult> UpdatePrice(int priceId, UpdateSubscriptionPriceRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _service.UpdatePriceAsync(priceId, request, cancellationToken);
            return result.Success ? Ok(result.Price) : BadRequest(new
            {
                code = "INVALID_PRICE",
                message = result.Error?.Message,
                field = result.Error?.Field,
                conflictPriceId = result.Error?.ConflictPriceId,
                conflictBillingCycle = result.Error?.ConflictBillingCycle?.ToString(),
                conflictEffectiveFrom = result.Error?.ConflictEffectiveFrom,
                conflictEffectiveTo = result.Error?.ConflictEffectiveTo,
            });
        }

        [HttpPatch("prices/{priceId:int}/status")]
        public async Task<IActionResult> SetPriceStatus(int priceId, SetActiveRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _service.SetPriceActiveAsync(priceId, request.IsActive, cancellationToken);
            return result.Success ? NoContent() : BadRequest(new { code = "INVALID_PRICE_STATUS", message = result.Error });
        }
    }
}
