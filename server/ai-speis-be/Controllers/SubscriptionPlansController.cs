using ai_speis_be.Models.DTOs;
using ai_speis_be.Services.SubscriptionPlanService;
using Microsoft.AspNetCore.Mvc;

namespace ai_speis_be.Controllers
{
    [ApiController]
    [Route("api/subscription-plans")]
    public sealed class SubscriptionPlansController : ControllerBase
    {
        private readonly ISubscriptionPlanService _service;

        public SubscriptionPlansController(ISubscriptionPlanService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<SubscriptionPlanDto>>> GetPlans(CancellationToken cancellationToken)
            => Ok(await _service.GetPublicPlansAsync(cancellationToken));
    }
}
