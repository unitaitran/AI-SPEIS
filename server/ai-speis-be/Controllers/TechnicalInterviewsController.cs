using ai_speis_be.TechnicalInterviews.DTOs;
using ai_speis_be.TechnicalInterviews.V2;
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
        private readonly ITechnicalV2InterviewOrchestrator _v2Orchestrator;

        public TechnicalInterviewsController(ITechnicalV2InterviewOrchestrator v2Orchestrator)
        {
            _v2Orchestrator = v2Orchestrator;
        }

        [HttpPost("sessions")]
        public async Task<IActionResult> Initialize(
            [FromBody] InitializeTechnicalInterviewRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var result = await _v2Orchestrator.InitializeAsync(
                userId,
                request.InterviewSessionId,
                new InitializeTechnicalV2Request { RequiredSkills = request.SelectedSkills ?? new List<string>() },
                cancellationToken);
            return ToActionResult(result);
        }

        [HttpPost("{sessionId:int}/start")]
        public async Task<IActionResult> Start(int sessionId, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var result = await _v2Orchestrator.StartAsync(userId, sessionId, cancellationToken);
            return ToActionResult(result);
        }

        [HttpGet("{sessionId:int}")]
        public async Task<IActionResult> GetSession(int sessionId, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var result = await _v2Orchestrator.GetStateAsync(userId, sessionId, cancellationToken);
            return ToActionResult(result);
        }

        [HttpGet("{sessionId:int}/current-question")]
        public async Task<IActionResult> GetCurrentQuestion(int sessionId, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var result = await _v2Orchestrator.GetCurrentQuestionAsync(userId, sessionId, cancellationToken);
            return ToActionResult(result);
        }

        [HttpPost("{sessionId:int}/answers")]
        public async Task<IActionResult> SubmitAnswer(
            int sessionId,
            [FromBody] SubmitTechnicalAnswerRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var result = await _v2Orchestrator.SubmitAnswerAsync(
                userId,
                sessionId,
                request.SessionQuestionId,
                new SubmitTechnicalV2AnswerRequest
                {
                    Transcript = request.Transcript,
                    AudioId = request.AudioId,
                    SttConfidence = request.SttConfidence
                },
                idempotencyKey ?? string.Empty,
                cancellationToken);
            return ToActionResult(result);
        }

        [HttpPost("{sessionId:int}/complete")]
        public async Task<IActionResult> Complete(int sessionId, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var result = await _v2Orchestrator.CompleteAsync(userId, sessionId, cancellationToken);
            return ToActionResult(result);
        }

        [HttpGet("{sessionId:int}/result")]
        public async Task<IActionResult> GetResult(int sessionId, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var result = await _v2Orchestrator.GetResultAsync(userId, sessionId, cancellationToken);
            return ToActionResult(result);
        }

        [HttpPost("{sessionId:int}/feedback")]
        public async Task<IActionResult> GenerateFeedback(int sessionId, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var result = await _v2Orchestrator.GenerateFeedbackAsync(userId, sessionId, cancellationToken);
            return ToActionResult(result);
        }

        [HttpPost("{sessionId:int}/pre-generate")]
        public IActionResult PreGenerate(int sessionId)
        {
            return Ok(new { SessionId = sessionId, Status = "COMPLETED", Message = "Real-time selection enabled." });
        }

        [HttpGet("{sessionId:int}/pre-generate-status")]
        public IActionResult GetPreGenerateStatus(int sessionId)
        {
            return Ok(new { SessionId = sessionId, Status = "COMPLETED", Message = "Real-time selection enabled." });
        }

        [HttpPost("{sessionId:int}/cancel-pre-generate")]
        public IActionResult CancelPreGenerate(int sessionId)
        {
            return Ok(new { SessionId = sessionId, Status = "CANCELLED" });
        }

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var claim = User.FindFirst("UserId")?.Value;
            return !string.IsNullOrEmpty(claim) && int.TryParse(claim, out userId);
        }

        private IActionResult ToActionResult<T>(TechnicalV2OperationResult<T> result)
        {
            return result.Status switch
            {
                TechnicalV2OperationStatus.Ok => Ok(result.Value),
                TechnicalV2OperationStatus.Created => StatusCode(StatusCodes.Status201Created, result.Value),
                TechnicalV2OperationStatus.BadRequest => BadRequest(new { code = result.ErrorCode, message = result.Message }),
                TechnicalV2OperationStatus.NotFound => NotFound(new { code = result.ErrorCode, message = result.Message }),
                TechnicalV2OperationStatus.Conflict => Conflict(new { code = result.ErrorCode, message = result.Message }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { code = result.ErrorCode, message = result.Message })
            };
        }
    }
}
