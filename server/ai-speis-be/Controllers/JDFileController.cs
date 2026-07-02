using ai_speis_be.Services.JDService;
using ai_speis_be.Models.DTOs;
using ai_speis_be.DTOs.JdParsing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ai_speis_be.Repositories.JDRepo;
namespace ai_speis_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JDFileController : ControllerBase
    {
        private readonly IJDService _jdService;
        private readonly ILogger<JDFileController> _logger;
        public JDFileController(IJDService jdService, ILogger<JDFileController> logger)
        {
            _jdService = jdService;
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
        public async Task<IActionResult> GetAllJD([FromQuery] JDQueryParameters query, CancellationToken cancellationToken)
        {
            var JD = await _jdService.GetAllJDsAsync(query);
            return Ok(JD);
        }
        [HttpGet("user/{userId:int}")]
        [Authorize]
        public async Task<IActionResult> GetUserJD(int userId, [FromQuery] JDQueryParameters query, CancellationToken cancellationToken)
        {
            if (userId <= 0)
                return BadRequest(new { title = "ID không hợp lệ", detail = "User ID phải là số nguyên dương." });
            var currentUserRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
             if (currentUserRole != "Admin")
            {
                if (!TryGetUserId(out int currentUserId) || currentUserId != userId)
                {
                    return NotFound(new { Message = "Không tìm thấy file JD." });
                }
            }
            var JD = await _jdService.GetJDByUserIdAsync(userId, query);
           
            if (JD == null) return NotFound(new { Message = "Người dùng chưa upload JD nào" });
          
            return Ok(JD);
        }

        [HttpGet("history")]
        [Authorize]
        public async Task<IActionResult> GetMyJDHistory([FromQuery] JDQueryParameters query)
        {
            if (!TryGetUserId(out int userId))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });
            }
            var jds = await _jdService.GetJDByUserIdAsync(userId, query);
            return Ok(jds);
        }
        [HttpPost("upload")]
        [Authorize]
        public async Task<IActionResult> UploadJD(IFormFile file)
        {
            if (!TryGetUserId(out int userId))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });
            }

            if (file == null)
            {
                return BadRequest(new { Message = "Vui lòng chọn file để tải lên." });
            }

            _logger.LogInformation("Bắt đầu tải lên JD cho User {UserId}", userId);

            var (success, errorMessage, jdDto) = await _jdService.UploadJDAsync (userId, file);
            if (!success)
            {
                return BadRequest(new { Message = errorMessage });
            }

            return Ok(jdDto);
        }

        [HttpPost("text")]
        [Authorize]
        public async Task<IActionResult> SubmitJDText([FromBody] SubmitJDTextRequest request)
        {
            if (!TryGetUserId(out int userId))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });
            }

            _logger.LogInformation("User {UserId} submit JD bằng text ({Length} ký tự)", userId, request.RawText.Length);

            var (success, errorMessage, jdDto) = await _jdService.SubmitJDTextAsync(userId, request.RawText);
            if (!success)
            {
                return BadRequest(new { Message = errorMessage });
            }

            return Ok(jdDto);
        }
        [HttpGet("{id:int}")]
        [Authorize]
        public async Task<IActionResult> GetJDById(int id)
        {
            if (id <= 0)
                return BadRequest(new { title = "ID không hợp lệ", detail = "JD ID phải là số nguyên dương." });

            var currentUserRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (string.IsNullOrEmpty(currentUserRole) || !TryGetUserId(out int currentUserId))
            {
                return Unauthorized();
            }

            var jd = await _jdService.GetJDByIdAsync(id);
            if (jd == null)
            {
                return NotFound(new { Message = "Không tìm thấy file JD." });
            }

            // Regular user can only view their own JD, and it must not be archived
            if (currentUserRole != "Admin")
            {
                if (jd.UserId != currentUserId)
                {
                    return NotFound(new { Message = "Không tìm thấy file JD." });
                }
            }

            return Ok(jd);
        }
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteJD(int id)
        {
            var currentUserRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(currentUserRole) || !TryGetUserId(out int currentUserId))
            {
                return Unauthorized();
            }

            var jd = await _jdService.GetJDByIdAsync(id);
            if (jd == null)
            {
                return NotFound(new { Message = "Không tìm thấy file JD." });
            }

            // Regular user can only delete their own JD
            if (currentUserRole != "Admin" && jd.UserId != currentUserId)
            {
                return Forbid();
            }

            var (success, errorMessage) = await _jdService.DeleteJDAsync(id);
            if (!success)
            {
                return BadRequest(new { Message = errorMessage });
            }

            return Ok(new { Message = "Xóa file JD thành công." });
        }

        // ===================== AI PARSING ENDPOINTS =====================

        [HttpPost("{jdId}/parse")]
        [Authorize]
        public async Task<IActionResult> TriggerParse(int jdId)
        {
            if (jdId <= 0)
                return BadRequest(new { title = "ID không hợp lệ", detail = "JD ID phải là số nguyên dương." });

            if (!TryGetUserId(out int userId)) return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });

            var result = await _jdService.TriggerParseAsync(userId, jdId);
            if (!result) return BadRequest(new { success = false, message = "Could not start parsing. File might not exist or is already parsed." });

            return Ok(new { success = true, message = "Parsing started successfully" });
        }

        [HttpGet("{jdId}/status")]
        [Authorize]
        public async Task<IActionResult> GetParseStatus(int jdId)
        {
            if (jdId <= 0)
                return BadRequest(new { title = "ID không hợp lệ", detail = "JD ID phải là số nguyên dương." });

            if (!TryGetUserId(out int userId)) return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });

            var status = await _jdService.GetParseStatusAsync(userId, jdId);
            if (status == null) return NotFound(new { success = false, message = "JD not found" });

            return Ok(new { success = true, data = status });
        }

        [HttpGet("{jdId}/parsed-data")]
        [Authorize]
        public async Task<IActionResult> GetParsedData(int jdId)
        {
            if (jdId <= 0)
                return BadRequest(new { title = "ID không hợp lệ", detail = "JD ID phải là số nguyên dương." });

            if (!TryGetUserId(out int userId)) return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });

            var data = await _jdService.GetParsedDataAsync(userId, jdId);
            if (data == null) return NotFound(new { success = false, message = "Parsed data not found or JD not parsed yet" });

            return Ok(new { success = true, data });
        }

        [HttpPut("{jdId}/confirm")]
        [Authorize]
        public async Task<IActionResult> ConfirmParsedData(int jdId, [FromBody] JdConfirmRequest request)
        {
            if (jdId <= 0)
                return BadRequest(new { title = "ID không hợp lệ", detail = "JD ID phải là số nguyên dương." });

            if (!TryGetUserId(out int userId)) return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });

            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _jdService.ConfirmParsedDataAsync(userId, jdId, request);
            if (!result) return BadRequest(new { success = false, message = "Failed to confirm data. JD might not exist or data is invalid." });

            return Ok(new { success = true, message = "JD data confirmed successfully" });
        }

        /// <summary>
        /// FT-23: Đánh giá độ phù hợp của CV so với JD
        /// </summary>
        [HttpPost("{jdId}/match-cv/{cvId}")]
        [Authorize]
        public async Task<IActionResult> MatchCvToJd(int jdId, int cvId)
        {
            if (jdId <= 0 || cvId <= 0)
                return BadRequest(new { title = "ID không hợp lệ", detail = "JD ID và CV ID phải là số nguyên dương." });

            if (!TryGetUserId(out int userId)) return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });

            var result = await _jdService.MatchCvToJdAsync(userId, jdId, cvId);

            if (result == null)
            {
                return NotFound(new { Message = "Không tìm thấy CV/JD, hoặc CV/JD chưa được AI trích xuất dữ liệu. Vui lòng parse trước khi thực hiện Matching." });
            }

            if (!result.Success)
            {
                return StatusCode(500, new { Message = result.ErrorMessage });
            }

            return Ok(result);
        }
    }
}