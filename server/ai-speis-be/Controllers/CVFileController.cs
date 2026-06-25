using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ai_speis_be.Services.CVService;
using ai_speis_be.DTOs.CvParsing;

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

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllCV()
        {
            var CV = await _cvService.GetAllCVsAsync();
            return Ok(CV);
        }

        [HttpGet("user/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUserCV(int userId)
        {
            var cv = await _cvService.GetCVByUserIdAsync(userId);
            if (cv == null) return NotFound(new { Message = "Không tìm thấy CV của người dùng này." });
            return Ok(cv);
        }

        [HttpPost("upload")]
        [Authorize]
        public async Task<IActionResult> UploadCV(IFormFile file)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) 
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng." });
            }
            int userId = int.Parse(userIdClaim);

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
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) 
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng trong token." });
            }
            int userId = int.Parse(userIdClaim);
            var cv = await _cvService.GetMyCVAsync(userId);
            if (cv == null) return NotFound(new { Message = "Bạn chưa tải lên CV nào hoặc CV đã bị xóa." });
            return Ok(cv);
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetCVById(int id)
        {
            var currentUserRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            var currentUserIdClaim = User.FindFirst("UserId")?.Value;
            
            if (string.IsNullOrEmpty(currentUserRole) || string.IsNullOrEmpty(currentUserIdClaim))
            {
                return Unauthorized();
            }

            int currentUserId = int.Parse(currentUserIdClaim);

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
            var currentUserIdClaim = User.FindFirst("UserId")?.Value;
            
            if (string.IsNullOrEmpty(currentUserRole) || string.IsNullOrEmpty(currentUserIdClaim))
            {
                return Unauthorized();
            }

            int currentUserId = int.Parse(currentUserIdClaim);

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
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng." });

            int userId = int.Parse(userIdClaim);

            var (success, errorMessage) = await _cvService.TriggerParseAsync(id, userId);
            if (!success)
                return BadRequest(new { Message = errorMessage });

            return Ok(new { Message = "Đã bắt đầu phân tích CV. Vui lòng kiểm tra trạng thái." });
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
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng." });

            int userId = int.Parse(userIdClaim);

            var (success, errorMessage) = await _cvService.ConfirmParsedDataAsync(id, userId, request);
            if (!success)
                return BadRequest(new { Message = errorMessage });

            return Ok(new { Message = "Xác nhận dữ liệu CV thành công." });
        }
    }
}
