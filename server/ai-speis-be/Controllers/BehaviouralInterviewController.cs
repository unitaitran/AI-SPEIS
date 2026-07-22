using ai_speis_be.BehaviouralInterviews.DTOs;
using ai_speis_be.BehaviouralInterviews.Orchestration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ai_speis_be.Controllers
{
    [Route("api/interviews/{sessionId:int}/behaviour")]
    [ApiController]
    [Authorize]
    public class BehaviouralInterviewController : ControllerBase
    {
        private readonly IBehaviouralInterviewOrchestrator _orchestrator;

        public BehaviouralInterviewController(IBehaviouralInterviewOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var userIdClaim = User.FindFirst("UserId")?.Value;
            return !string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out userId);
        }

        /// <summary>Khởi tạo round (tùy chọn — start sẽ tự init nếu chưa có).</summary>
        [HttpPost("initialize")]
        public async Task<IActionResult> Initialize(
            int sessionId,
            [FromBody] InitializeBehaviouralInterviewRequest? request,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });
            }

            request ??= new InitializeBehaviouralInterviewRequest();
            request.InterviewSessionId = sessionId;

            var result = await _orchestrator.InitializeAsync(userId, request, cancellationToken);
            return ToActionResult(result);
        }

        /// <summary>Lock bộ câu hỏi (nếu chưa) và trả về câu hỏi đầu tiên / hiện tại.</summary>
        [HttpPost("start")]
        public async Task<IActionResult> Start(int sessionId, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });
            }

            var result = await _orchestrator.StartAsync(userId, sessionId, cancellationToken);
            return ToActionResult(result);
        }

        [HttpGet("current-question")]
        public async Task<IActionResult> GetCurrentQuestion(int sessionId, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });
            }

            var result = await _orchestrator.GetCurrentQuestionAsync(userId, sessionId, cancellationToken);
            return ToActionResult(result);
        }

        /// <summary>Trạng thái round để resume.</summary>
        [HttpGet("state")]
        public async Task<IActionResult> GetState(int sessionId, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });
            }

            var result = await _orchestrator.GetSessionAsync(userId, sessionId, cancellationToken);
            return ToActionResult(result);
        }

        /// <summary>Submit transcript câu trả lời — backend gọi AI đánh giá và quyết định câu tiếp theo.</summary>
        [HttpPost("questions/{sessionQuestionId:int}/answers")]
        public async Task<IActionResult> SubmitAnswer(
            int sessionId,
            int sessionQuestionId,
            [FromBody] SubmitBehaviouralAnswerRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            request.SessionQuestionId = sessionQuestionId;
            var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;

            var result = await _orchestrator.SubmitAnswerAsync(
                userId, sessionId, request, idempotencyKey, cancellationToken);
            return ToActionResult(result);
        }

        [HttpPost("complete")]
        public async Task<IActionResult> Complete(int sessionId, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });
            }

            var result = await _orchestrator.CompleteAsync(userId, sessionId, cancellationToken);
            return ToActionResult(result);
        }

        [HttpGet("result")]
        public async Task<IActionResult> GetResult(int sessionId, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });
            }

            var result = await _orchestrator.GetResultAsync(userId, sessionId, cancellationToken);
            return ToActionResult(result);
        }

        private IActionResult ToActionResult<T>(BehaviouralOperationResult<T> result)
        {
            return result.Status switch
            {
                BehaviouralOperationStatus.Ok => Ok(result.Value),
                BehaviouralOperationStatus.Created => StatusCode(StatusCodes.Status201Created, result.Value),
                BehaviouralOperationStatus.BadRequest => BadRequest(new { result.ErrorCode, result.Message }),
                BehaviouralOperationStatus.NotFound => NotFound(new { result.ErrorCode, result.Message }),
                BehaviouralOperationStatus.Conflict => Conflict(new { result.ErrorCode, result.Message }),
                BehaviouralOperationStatus.ExternalFailure => StatusCode(
                    StatusCodes.Status502BadGateway, new { result.ErrorCode, result.Message }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { result.ErrorCode, result.Message })
            };
        }
    }
}
