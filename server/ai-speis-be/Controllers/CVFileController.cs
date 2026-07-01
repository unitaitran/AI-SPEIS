using ai_speis_be.DTOs.CvParsing;
using ai_speis_be.Repositories.CVRepo;
using ai_speis_be.Services.CVService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ai_speis_be.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CVFileController : ControllerBase
    {
        private readonly ICVService _cvService;
        private readonly ILogger<CVFileController> _logger;

        public CVFileController(ICVService cvService, ILogger<CVFileController> logger)
        {
            _cvService = cvService;
            _logger = logger;
        }

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var userIdClaim = User.FindFirst("UserId")?.Value;
            return !string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out userId);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllCV([FromQuery] CVQueryParameters query, CancellationToken cancellationToken)
        {
            var CV = await _cvService.GetAllCVsAsync(query);
            return Ok(CV);
        }

        [HttpGet("user/{userId:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUserCV(int userId, [FromQuery] CVQueryParameters query, CancellationToken cancellationToken)
        {
            if (userId <= 0)
                return BadRequest(new { title = "ID không hợp lệ", detail = "User ID phải là số nguyên dương." });
            var cv = await _cvService.GetCVByUserIdAsync(userId, query);
            if (cv == null) return NotFound(new { Message = "Người dùng chưa upload CV nào" });
            return Ok(cv);
        }

        [HttpPost("upload")]
        [Authorize]
        public async Task<IActionResult> UploadCV(IFormFile file)
        {
            if (!TryGetUserId(out int userId))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });
            }

            if (file == null)
            {
                return BadRequest(new { Message = "Vui lòng chọn file để tải lên." });
            }

            _logger.LogInformation("Bắt đầu tải lên CV cho User {UserId}", userId);
            
            var (success, errorMessage, cvDto) = await _cvService.UploadCVAsync(userId, file);
            if (!success)
            {
                return BadRequest(new { Message = errorMessage });
            }

            return Ok(cvDto);
        }

        [HttpGet("MyCV")]
        [Authorize]
        public async Task<IActionResult> GetMyCV()
        {
            if (!TryGetUserId(out int userId))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });
            }
            var cv = await _cvService.GetMyCVAsync(userId);
            if (cv == null) return NotFound(new { Message = "Bạn chưa tải lên CV nào hoặc CV đã bị xóa." });
            return Ok(cv);
        }

        [HttpGet("history")]
        [Authorize]
        public async Task<IActionResult> GetMyCVHistory([FromQuery] CVQueryParameters query)
        {
            if (!TryGetUserId(out int userId))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });
            }
            var cvs = await _cvService.GetCVByUserIdAsync(userId, query);
            return Ok(cvs);
        }

        [HttpGet("{id:int}")]
        [Authorize]
        public async Task<IActionResult> GetCVById(int id)
        {
            if (id <= 0)
                return BadRequest(new { title = "ID không hợp lệ", detail = "CV ID phải là số nguyên dương." });
                
            var currentUserRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (string.IsNullOrEmpty(currentUserRole) || !TryGetUserId(out int currentUserId))
            {
                return Unauthorized();
            }

            var cv = await _cvService.GetCVByIdAsync(id);
            if (cv == null)
            {
                return NotFound(new { Message = "Không tìm thấy file CV." });
            }

            // Regular user can only view their own CV, and it must not be archived
            if (currentUserRole != "Admin")
            {
                if (cv.UserId != currentUserId || cv.Status == ai_speis_be.Models.Enums.CVFileStatus.Archived)
                {
                    return NotFound(new { Message = "Không tìm thấy file CV." });
                }
            }

            return Ok(cv);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteCV(int id)
        {
            var currentUserRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            
            if (string.IsNullOrEmpty(currentUserRole) || !TryGetUserId(out int currentUserId))
            {
                return Unauthorized();
            }

            var cv = await _cvService.GetCVByIdAsync(id);
            if (cv == null)
            {
                return NotFound(new { Message = "Không tìm thấy file CV." });
            }

            // Regular user can only delete their own CV
            if (currentUserRole != "Admin" && cv.UserId != currentUserId)
            {
                return Forbid();
            }

            var (success, errorMessage) = await _cvService.DeleteCVAsync(id);
            if (!success)
            {
                return BadRequest(new { Message = errorMessage });
            }

            return Ok(new { Message = "Xóa file CV thành công." });
        }

        // ===================== CV PARSING ENDPOINTS (Step 7) =====================

        /// <summary>
        /// Trigger AI parse cho CV đã upload. Chỉ parse được khi status = Pending hoặc AnalysisFailed.
        /// </summary>
        [HttpPost("{id}/parse")]
        [Authorize]
        public async Task<IActionResult> TriggerParse(int id)
        {
            try
            {
                if (!TryGetUserId(out int userId))
                    return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });

                var (success, errorMessage) = await _cvService.TriggerParseAsync(id, userId);
                if (!success)
                    return BadRequest(new { Message = errorMessage });

                return Ok(new { Message = "Đã bắt đầu phân tích CV. Vui lòng kiểm tra trạng thái." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không mong muốn khi gọi TriggerParse cho CV ID: {Id}", id);
                return StatusCode(500, new { Message = "Đã xảy ra lỗi không mong muốn trên máy chủ." });
            }
        }

        /// <summary>
        /// Poll trạng thái xử lý CV (cho frontend polling mỗi 2s).
        /// </summary>
        [HttpGet("{id}/status")]
        [Authorize]
        public async Task<IActionResult> GetParseStatus(int id)
        {
            var result = await _cvService.GetParseStatusAsync(id);
            if (result == null)
                return NotFound(new { Message = "Không tìm thấy file CV." });

            return Ok(result);
        }

        /// <summary>
        /// Lấy dữ liệu AI đã trích xuất (skills, projects, education, experience).
        /// </summary>
        [HttpGet("{id}/parsed-data")]
        [Authorize]
        public async Task<IActionResult> GetParsedData(int id)
        {
            var result = await _cvService.GetParsedDataAsync(id);
            if (result == null)
                return NotFound(new { Message = "Chưa có dữ liệu trích xuất cho CV này." });

            return Ok(result);
        }

        /// <summary>
        /// User xác nhận (và có thể chỉnh sửa) dữ liệu AI đã trích xuất.
        /// </summary>
        [HttpPut("{id}/confirm")]
        [Authorize]
        public async Task<IActionResult> ConfirmParsedData(int id, [FromBody] CvConfirmRequest request)
        {
            try
            {
                if (!TryGetUserId(out int userId))
                    return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });

                var (success, errorMessage) = await _cvService.ConfirmParsedDataAsync(id, userId, request);
                if (!success)
                    return BadRequest(new { Message = errorMessage });

                return Ok(new { Message = "Xác nhận dữ liệu CV thành công." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không mong muốn khi xác nhận dữ liệu cho CV ID: {Id}", id);
                return StatusCode(500, new { Message = "Đã xảy ra lỗi không mong muốn trên máy chủ." });
            }
        }
    }
}
