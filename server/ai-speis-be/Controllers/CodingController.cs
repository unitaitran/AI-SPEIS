using ai_speis_be.Models.DTOs;
using ai_speis_be.Services.CodingService;
using ai_speis_be.Services.Judge0Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ai_speis_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CodingController : ControllerBase
    {
        private readonly ICodingService _codingService;
        private readonly IJudge0Service _judge0Service;

        public CodingController(
            ICodingService codingService,
            IJudge0Service judge0Service)
        {
            _codingService = codingService;
            _judge0Service = judge0Service;
        }

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var userIdClaim = User.FindFirst("UserId")?.Value;
            return !string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out userId);
        }

        /// <summary>
        /// Submit code để chạy qua Judge0 sandbox.
        /// Gửi source code + language → chạy qua tất cả test cases → trả về kết quả.
        /// </summary>
        [HttpPost("submit")]
        [ProducesResponseType(typeof(SubmissionResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> SubmitCode(
            [FromBody] SubmitCodeRequestDto request,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out int userId))
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var (success, errorMessage, data) = await _codingService.SubmitCodeAsync(
                userId, request, cancellationToken);

            if (!success)
                return BadRequest(new { Message = errorMessage });

            return Ok(data);
        }

        /// <summary>
        /// Lấy danh sách câu hỏi coding của 1 session.
        /// Chỉ trả về sample test cases (không trả hidden).
        /// </summary>
        [HttpGet("questions/{sessionId:int}")]
        [ProducesResponseType(typeof(List<CodingQuestionResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetCodingQuestions(
            int sessionId,
            CancellationToken cancellationToken)
        {
            if (sessionId < 0)
                return BadRequest(new { Message = "ID phiên phỏng vấn không hợp lệ." });

            if (!TryGetUserId(out int userId))
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });

            var (success, errorMessage, data) = await _codingService.GetCodingQuestionsAsync(
                userId, sessionId, cancellationToken);

            if (!success)
                return BadRequest(new { Message = errorMessage });

            return Ok(data);
        }

        /// <summary>
        /// Xem chi tiết kết quả 1 submission.
        /// </summary>
        [HttpGet("submission/{submissionId:int}")]
        [ProducesResponseType(typeof(SubmissionResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSubmission(
            int submissionId,
            CancellationToken cancellationToken)
        {
            if (submissionId <= 0)
                return BadRequest(new { Message = "ID submission không hợp lệ." });

            if (!TryGetUserId(out int userId))
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });

            var (success, errorMessage, data) = await _codingService.GetSubmissionAsync(
                userId, submissionId, cancellationToken);

            if (!success)
                return NotFound(new { Message = errorMessage });

            return Ok(data);
        }

        /// <summary>
        /// Lấy lịch sử submissions của 1 câu hỏi trong 1 session.
        /// </summary>
        [HttpGet("submissions/{sessionId:int}/{questionId:int}")]
        [ProducesResponseType(typeof(List<SubmissionSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSubmissionHistory(
            int sessionId,
            int questionId,
            CancellationToken cancellationToken)
        {
            if (sessionId <= 0 || questionId <= 0)
                return BadRequest(new { Message = "ID không hợp lệ." });

            if (!TryGetUserId(out int userId))
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });

            var (success, errorMessage, data) = await _codingService.GetSubmissionHistoryAsync(
                userId, sessionId, questionId, cancellationToken);

            if (!success)
                return BadRequest(new { Message = errorMessage });

            return Ok(data);
        }

        /// <summary>
        /// Proxy lấy danh sách ngôn ngữ lập trình từ Judge0.
        /// </summary>
        [HttpGet("languages")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<Judge0LanguageDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLanguages(CancellationToken cancellationToken)
        {
            try
            {
                var languages = await _judge0Service.GetLanguagesAsync(cancellationToken);
                return Ok(languages);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { Message = "Không thể kết nối đến Judge0.", Detail = ex.Message });
            }
        }

        /// <summary>
        /// Import Coding Questions từ file Excel.
        /// </summary>
        [HttpPost("admin/import")]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ImportAdminCodingQuestions(
            IFormFile file,
            CancellationToken cancellationToken)
        {
            var (success, errorMessage, importedCount) = await _codingService.ImportAdminCodingQuestionsAsync(
                file, cancellationToken);

            if (!success)
                return BadRequest(new { Message = errorMessage });

            return Ok(new { Message = $"Import thành công {importedCount} câu hỏi coding." });
        }
    }
}
