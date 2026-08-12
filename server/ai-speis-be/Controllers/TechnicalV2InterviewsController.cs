using ai_speis_be.TechnicalInterviews.V2;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ai_speis_be.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/interviews/{sessionId:int}/technical")]
    public sealed class TechnicalV2InterviewsController : ControllerBase
    {
        private readonly ITechnicalV2InterviewOrchestrator _orchestrator;

        public TechnicalV2InterviewsController(ITechnicalV2InterviewOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        [HttpPost("initialize")]
        public async Task<IActionResult> Initialize(int sessionId, [FromBody] InitializeTechnicalV2Request? request, CancellationToken cancellationToken)
        {
            return await ExecuteAsync(() => _orchestrator.InitializeAsync(UserId(), sessionId, request ?? new(), cancellationToken));
        }

        [HttpPost("start")]
        public async Task<IActionResult> Start(int sessionId, CancellationToken cancellationToken)
        {
            return await ExecuteAsync(() => _orchestrator.StartAsync(UserId(), sessionId, cancellationToken));
        }

        [HttpGet("state")]
        public async Task<IActionResult> State(int sessionId, CancellationToken cancellationToken)
        {
            return await ExecuteAsync(() => _orchestrator.GetStateAsync(UserId(), sessionId, cancellationToken));
        }

        [HttpGet("current-question")]
        public async Task<IActionResult> CurrentQuestion(int sessionId, CancellationToken cancellationToken)
        {
            return await ExecuteAsync(() => _orchestrator.GetCurrentQuestionAsync(UserId(), sessionId, cancellationToken));
        }

        [HttpPost("questions/{questionId:int}/answers")]
        public async Task<IActionResult> SubmitAnswer(int sessionId, int questionId, [FromBody] SubmitTechnicalV2AnswerRequest request, CancellationToken cancellationToken)
        {
            var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
            return await ExecuteAsync(() => _orchestrator.SubmitAnswerAsync(UserId(), sessionId, questionId, request, idempotencyKey, cancellationToken));
        }

        [HttpPost("complete")]
        public async Task<IActionResult> Complete(int sessionId, CancellationToken cancellationToken)
        {
            return await ExecuteAsync(() => _orchestrator.CompleteAsync(UserId(), sessionId, cancellationToken));
        }

        [HttpGet("result")]
        public async Task<IActionResult> Result(int sessionId, CancellationToken cancellationToken)
        {
            return await ExecuteAsync(() => _orchestrator.GetResultAsync(UserId(), sessionId, cancellationToken));
        }

        [HttpPost("feedback")]
        public async Task<IActionResult> Feedback(int sessionId, CancellationToken cancellationToken)
        {
            return await ExecuteAsync(() => _orchestrator.GenerateFeedbackAsync(UserId(), sessionId, cancellationToken));
        }

        private int UserId()
        {
            return int.TryParse(User.FindFirst("UserId")?.Value, out var userId) && userId > 0 ? userId : 0;
        }

        private async Task<IActionResult> ExecuteAsync<T>(Func<Task<TechnicalV2OperationResult<T>>> operation)
        {
            var userId = UserId();
            if (userId <= 0) return Unauthorized();
            return ToActionResult(await operation());
        }

        private IActionResult ToActionResult<T>(TechnicalV2OperationResult<T> result)
        {
            return result.Status switch
            {
                TechnicalV2OperationStatus.Ok => Ok(result.Value),
                TechnicalV2OperationStatus.Created => StatusCode(StatusCodes.Status201Created, result.Value),
                TechnicalV2OperationStatus.BadRequest => Problem(StatusCodes.Status400BadRequest, result),
                TechnicalV2OperationStatus.NotFound => Problem(StatusCodes.Status404NotFound, result),
                TechnicalV2OperationStatus.Conflict => Problem(StatusCodes.Status409Conflict, result),
                TechnicalV2OperationStatus.ExternalFailure => Problem(StatusCodes.Status502BadGateway, result),
                _ => Problem(StatusCodes.Status500InternalServerError, result)
            };
        }

        private ObjectResult Problem<T>(int status, TechnicalV2OperationResult<T> result)
        {
            return StatusCode(status, new ProblemDetails { Status = status, Title = result.ErrorCode ?? "TECHNICAL_V2_ERROR", Detail = result.Message });
        }
    }
}
