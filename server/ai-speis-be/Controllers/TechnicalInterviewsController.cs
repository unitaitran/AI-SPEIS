using ai_speis_be.TechnicalInterviews.DTOs;
using ai_speis_be.TechnicalInterviews.Orchestration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ai_speis_be.Controllers
{
    [ApiController]
    [Authorize]
    [EnableRateLimiting("technical-interview")]
    [Route("api/technical-interviews")]
    public sealed class TechnicalInterviewsController : ControllerBase
    {
        private readonly ITechnicalInterviewOrchestrator _orchestrator;

        public TechnicalInterviewsController(ITechnicalInterviewOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        [HttpPost("sessions")]
        public async Task<IActionResult> Initialize(
            [FromBody] InitializeTechnicalInterviewRequest request,
            CancellationToken cancellationToken)
        {
            return TryGetUserId(out var userId)
                ? ToActionResult(await _orchestrator.InitializeAsync(userId, request, cancellationToken))
                : UnauthorizedProblem();
        }

        [HttpPost("{sessionId:int}/start")]
        public async Task<IActionResult> Start(int sessionId, CancellationToken cancellationToken)
        {
            return TryGetUserId(out var userId)
                ? ToActionResult(await _orchestrator.StartAsync(userId, sessionId, cancellationToken))
                : UnauthorizedProblem();
        }

        [HttpGet("{sessionId:int}")]
        public async Task<IActionResult> GetSession(int sessionId, CancellationToken cancellationToken)
        {
            return TryGetUserId(out var userId)
                ? ToActionResult(await _orchestrator.GetSessionAsync(userId, sessionId, cancellationToken))
                : UnauthorizedProblem();
        }

        [HttpGet("{sessionId:int}/current-question")]
        public async Task<IActionResult> GetCurrentQuestion(int sessionId, CancellationToken cancellationToken)
        {
            return TryGetUserId(out var userId)
                ? ToActionResult(await _orchestrator.GetCurrentQuestionAsync(userId, sessionId, cancellationToken))
                : UnauthorizedProblem();
        }

        [HttpPost("{sessionId:int}/answers")]
        public async Task<IActionResult> SubmitAnswer(
            int sessionId,
            [FromBody] SubmitTechnicalAnswerRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            CancellationToken cancellationToken)
        {
            return TryGetUserId(out var userId)
                ? ToActionResult(await _orchestrator.SubmitAnswerAsync(
                    userId,
                    sessionId,
                    request,
                    idempotencyKey ?? string.Empty,
                    cancellationToken))
                : UnauthorizedProblem();
        }

        [HttpPost("{sessionId:int}/complete")]
        public async Task<IActionResult> Complete(int sessionId, CancellationToken cancellationToken)
        {
            return TryGetUserId(out var userId)
                ? ToActionResult(await _orchestrator.CompleteAsync(userId, sessionId, cancellationToken))
                : UnauthorizedProblem();
        }

        [HttpGet("{sessionId:int}/result")]
        public async Task<IActionResult> GetResult(int sessionId, CancellationToken cancellationToken)
        {
            return TryGetUserId(out var userId)
                ? ToActionResult(await _orchestrator.GetResultAsync(userId, sessionId, cancellationToken))
                : UnauthorizedProblem();
        }

        [HttpPost("{sessionId:int}/feedback")]
        public async Task<IActionResult> GenerateFeedback(int sessionId, CancellationToken cancellationToken)
        {
            return TryGetUserId(out var userId)
                ? ToActionResult(await _orchestrator.GenerateFeedbackAsync(userId, sessionId, cancellationToken))
                : UnauthorizedProblem();
        }

        private bool TryGetUserId(out int userId)
        {
            return int.TryParse(User.FindFirst("UserId")?.Value, out userId) && userId > 0;
        }

        private IActionResult UnauthorizedProblem()
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "INVALID_USER_IDENTITY",
                Detail = "The authenticated token does not contain a valid UserId."
            });
        }

        private IActionResult ToActionResult<T>(TechnicalOperationResult<T> result)
        {
            return result.Status switch
            {
                TechnicalOperationStatus.Ok => Ok(result.Value),
                TechnicalOperationStatus.Created => StatusCode(StatusCodes.Status201Created, result.Value),
                TechnicalOperationStatus.BadRequest => ProblemResult(StatusCodes.Status400BadRequest, result),
                TechnicalOperationStatus.NotFound => ProblemResult(StatusCodes.Status404NotFound, result),
                TechnicalOperationStatus.Conflict => ProblemResult(StatusCodes.Status409Conflict, result),
                TechnicalOperationStatus.ExternalFailure => ProblemResult(StatusCodes.Status502BadGateway, result),
                _ => ProblemResult(StatusCodes.Status500InternalServerError, result)
            };
        }

        private ObjectResult ProblemResult<T>(int statusCode, TechnicalOperationResult<T> result)
        {
            return StatusCode(statusCode, new ProblemDetails
            {
                Status = statusCode,
                Title = result.ErrorCode ?? "TECHNICAL_INTERVIEW_ERROR",
                Detail = result.Message
            });
        }
    }
}
