using System.Threading.Tasks;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Services.InterviewSessionService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ai_speis_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class InterviewSessionController : ControllerBase
    {
        private readonly IInterviewSessionService _service;

        public InterviewSessionController(IInterviewSessionService service)
        {
            _service = service;
        }

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var userIdClaim = User.FindFirst("UserId")?.Value;
            return !string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out userId);
        }

        [HttpPost]
        public async Task<IActionResult> CreateSession([FromBody] CreateInterviewSessionRequest request)
        {
            if (!TryGetUserId(out int userId))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (success, errorMessage, campaign) = await _service.CreateSessionsAsync(userId, request);
            if (!success)
            {
                return BadRequest(new { Message = errorMessage });
            }

            return Ok(campaign);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetSessionById(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { Message = "ID không hợp lệ." });
            }

            if (!TryGetUserId(out int userId))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });
            }

            var session = await _service.GetSessionByIdAsync(userId, id);
            if (session == null)
            {
                return NotFound(new { Message = "Không tìm thấy phiên phỏng vấn." });
            }

            return Ok(session);
        }

        [HttpGet("campaign/{campaignId:int}")]
        public async Task<IActionResult> GetCampaignById(int campaignId)
        {
            if (campaignId <= 0)
            {
                return BadRequest(new { Message = "ID đợt phỏng vấn không hợp lệ." });
            }

            if (!TryGetUserId(out int userId))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });
            }

            var campaign = await _service.GetCampaignByIdAsync(userId, campaignId);
            if (campaign == null)
            {
                return NotFound(new { Message = "Không tìm thấy đợt phỏng vấn." });
            }

            return Ok(campaign);
        }

        [HttpGet]
        public async Task<IActionResult> GetMyCampaigns()
        {
            if (!TryGetUserId(out int userId))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });
            }

            var campaigns = await _service.GetUserCampaignsAsync(userId);
            return Ok(campaigns);
        }

        [HttpGet("jd/{jdId:int}/available-types")]
        public async Task<IActionResult> GetAvailableTypes(int jdId)
        {
            if (jdId <= 0)
            {
                return BadRequest(new { Message = "ID JD không hợp lệ." });
            }

            if (!TryGetUserId(out int userId))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });
            }

            var result = await _service.GetAvailableRoundsAsync(userId, jdId);
            if (result == null)
            {
                return NotFound(new { Message = "Không tìm thấy thông tin JD hoặc JD chưa được phân tích." });
            }

            return Ok(result);
        }
    }
}
