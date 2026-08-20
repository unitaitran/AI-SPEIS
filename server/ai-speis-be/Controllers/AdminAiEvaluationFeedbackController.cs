using ai_speis_be.Services.AiEvaluationFeedbackService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ai_speis_be.Controllers
{
    [ApiController]
    [Authorize(Roles = "admin,Admin")]
    [Route("api/admin/ai-feedback")]
    public sealed class AdminAiEvaluationFeedbackController : ControllerBase
    {
        private readonly IAiEvaluationFeedbackService _service;

        public AdminAiEvaluationFeedbackController(IAiEvaluationFeedbackService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> List(
            [FromQuery] string? search,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            return Ok(await _service.GetAdminPageAsync(search, pageNumber, pageSize, cancellationToken));
        }

        [HttpGet("{feedbackId:int}")]
        public async Task<IActionResult> Detail(int feedbackId, CancellationToken cancellationToken)
        {
            return ToActionResult(await _service.GetAdminDetailAsync(feedbackId, cancellationToken));
        }

        private IActionResult ToActionResult<T>(AiEvaluationFeedbackOperationResult<T> result)
        {
            return result.Status switch
            {
                AiEvaluationFeedbackOperationStatus.Ok => Ok(result.Value),
                AiEvaluationFeedbackOperationStatus.BadRequest => BadRequest(new { result.ErrorCode, result.Message }),
                AiEvaluationFeedbackOperationStatus.NotFound => NotFound(new { result.ErrorCode, result.Message }),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }
    }
}
