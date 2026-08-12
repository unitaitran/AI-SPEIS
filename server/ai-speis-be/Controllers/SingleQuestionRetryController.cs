using ai_speis_be.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ai_speis_be.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/question-retry")]
    [Route("api/single-question-interview")]
    public sealed class SingleQuestionRetryController : ControllerBase
    {
        private readonly ISingleQuestionRetryService _service;

        public SingleQuestionRetryController(ISingleQuestionRetryService service)
        {
            _service = service;
        }

        /// <summary>
        /// Evaluate a transient single-question interview answer. No session or retry row is created.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RetryQuestion([FromBody] SingleQuestionRetryRequest request, CancellationToken cancellationToken)
        {
            var userId = UserId();
            if (userId <= 0) return Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Transcript))
                return BadRequest(new ProblemDetails { Title = "INVALID_TRANSCRIPT", Detail = "Transcript is required." });

            try
            {
                var result = await _service.RetryQuestionAsync(userId, request, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex) when (ex.Message == "QUESTION_NOT_FOUND")
            {
                return NotFound(new ProblemDetails { Title = "QUESTION_NOT_FOUND", Detail = "Question not found." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ProblemDetails { Title = ex.Message, Detail = "Invalid retry request." });
            }
        }

        /// <summary>
        /// Get retry history for a specific question.
        /// </summary>
        [HttpGet("question/{questionId:int}")]
        public async Task<IActionResult> GetRetryHistory(int questionId, CancellationToken cancellationToken)
        {
            var userId = UserId();
            if (userId <= 0) return Unauthorized();
            var history = await _service.GetRetryHistoryAsync(userId, questionId, cancellationToken);
            return Ok(history);
        }

        private int UserId()
        {
            return int.TryParse(User.FindFirst("UserId")?.Value, out var userId) && userId > 0 ? userId : 0;
        }
    }
}
