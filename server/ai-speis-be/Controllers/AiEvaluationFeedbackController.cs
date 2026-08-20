using ai_speis_be.Services.AiEvaluationFeedbackService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ai_speis_be.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/ai-feedback")]
    public sealed class AiEvaluationFeedbackController : ControllerBase
    {
        private readonly IAiEvaluationFeedbackService _service;

        public AiEvaluationFeedbackController(IAiEvaluationFeedbackService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateAiEvaluationFeedbackRequest request,
            CancellationToken cancellationToken)
        {
            var userId = UserId();
            if (userId <= 0) return Unauthorized();
            return ToActionResult(await _service.CreateAsync(userId, request, cancellationToken));
        }

        [HttpGet("me")]
        public async Task<IActionResult> Mine(CancellationToken cancellationToken)
        {
            var userId = UserId();
            if (userId <= 0) return Unauthorized();
            return Ok(await _service.GetMineAsync(userId, cancellationToken));
        }

        private int UserId() => int.TryParse(User.FindFirst("UserId")?.Value, out var value) ? value : 0;

        private IActionResult ToActionResult<T>(AiEvaluationFeedbackOperationResult<T> result)
        {
            return result.Status switch
            {
                AiEvaluationFeedbackOperationStatus.Ok => Ok(result.Value),
                AiEvaluationFeedbackOperationStatus.Created => StatusCode(StatusCodes.Status201Created, result.Value),
                AiEvaluationFeedbackOperationStatus.BadRequest => BadRequest(new { result.ErrorCode, result.Message }),
                AiEvaluationFeedbackOperationStatus.NotFound => NotFound(new { result.ErrorCode, result.Message }),
                AiEvaluationFeedbackOperationStatus.Forbidden => Forbid(),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }
    }
}
