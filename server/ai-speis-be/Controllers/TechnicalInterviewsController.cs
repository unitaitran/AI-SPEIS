using ai_speis_be.TechnicalInterviews.DTOs;
using ai_speis_be.TechnicalInterviews.Orchestration;
using ai_speis_be.TechnicalInterviews.PreGeneration;
using ai_speis_be.TechnicalInterviews.AI;
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
        private readonly ITechnicalPreGenerationService _preGenerationService;
        private readonly ITechnicalInterviewAIProviderResolver _aiProviderResolver;

        public TechnicalInterviewsController(
            ITechnicalInterviewOrchestrator orchestrator,
            ITechnicalPreGenerationService preGenerationService,
            ITechnicalInterviewAIProviderResolver aiProviderResolver)
        {
            _orchestrator = orchestrator;
            _preGenerationService = preGenerationService;
            _aiProviderResolver = aiProviderResolver;
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

        /// <summary>
        /// Kích hoạt tạo trước câu hỏi Technical chạy ngầm (background).
        /// Gọi từ trang Behavioral khi câu hỏi đầu tiên xuất hiện.
        /// </summary>
        [HttpPost("{sessionId:int}/pre-generate")]
        public async Task<IActionResult> PreGenerate(int sessionId, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId)) return UnauthorizedProblem();
            var status = await _preGenerationService.PreGenerateAsync(userId, sessionId, cancellationToken);
            return Ok(status);
        }

        /// <summary>
        /// Lấy trạng thái hiện tại của tiến trình tạo trước câu hỏi Technical.
        /// </summary>
        [HttpGet("{sessionId:int}/pre-generate-status")]
        public IActionResult GetPreGenerateStatus(int sessionId)
        {
            if (!TryGetUserId(out _)) return UnauthorizedProblem();
            return Ok(_preGenerationService.GetStatus(sessionId));
        }

        /// <summary>
        /// Hủy tiến trình tạo trước câu hỏi Technical (khi user thoát phỏng vấn).
        /// </summary>
        [HttpPost("{sessionId:int}/cancel-pre-generate")]
        public IActionResult CancelPreGenerate(int sessionId)
        {
            if (!TryGetUserId(out _)) return UnauthorizedProblem();
            _preGenerationService.CancelPreGeneration(sessionId);
            return NoContent();
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

        [HttpGet("health/ai")]
        [AllowAnonymous]
        public IActionResult CheckAIHealth()
        {
            var provider = _aiProviderResolver.Resolve();
            return Ok(new
            {
                status = "healthy",
                provider = provider.ProviderName,
                timestamp = DateTime.UtcNow
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
