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
                var activeCampaign = await _service.GetActiveCampaignAsync(userId);
                if (activeCampaign != null)
                {
                    var conflict = BuildActiveConflict(activeCampaign);
                    return Conflict(new
                    {
                        Code = conflict.Status == "Active"
                            ? "ACTIVE_INTERVIEW_SESSION_EXISTS"
                            : "ACTIVE_CAMPAIGN_EXISTS",
                        Message = "An active interview campaign or session already exists.",
                        Data = conflict
                    });
                }

                return BadRequest(new
                {
                    Code = "INTERVIEW_CAMPAIGN_CREATION_FAILED",
                    Message = errorMessage
                });
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

        [HttpPost("{id:int}/start")]
        public async Task<IActionResult> StartSession(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { Message = "ID không hợp lệ." });
            }

            if (!TryGetUserId(out int userId))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });
            }

            var (success, errorMessage, campaign) = await _service.StartSessionAsync(userId, id);
            if (!success)
            {
                return BadRequest(new { Message = errorMessage });
            }

            return Ok(campaign);
        }

        [HttpPost("{id:int}/complete")]
        public async Task<IActionResult> CompleteSession(int id)
        {
            if (id <= 0) return BadRequest(new { Message = "ID không hợp lệ." });
            if (!TryGetUserId(out int userId)) return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });

            var (success, errorMessage, campaign) = await _service.CompleteSessionAsync(userId, id);
            if (!success) return BadRequest(new { Message = errorMessage });
            return Ok(campaign);
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

        [HttpGet("campaign/{campaignId:int}/result")]
        public async Task<IActionResult> GetCampaignResult(int campaignId)
        {
            if (campaignId <= 0)
            {
                return BadRequest(new { Message = "ID campaign is invalid." });
            }

            if (!TryGetUserId(out int userId))
            {
                return Unauthorized(new { Message = "The authenticated token does not contain a valid UserId." });
            }

            var campaign = await _service.GetCampaignByIdAsync(userId, campaignId);
            if (campaign == null)
            {
                return NotFound(new { Message = "Interview campaign was not found." });
            }

            if (!string.Equals(campaign.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new
                {
                    Code = "CAMPAIGN_RESULT_NOT_READY",
                    Message = "The final campaign result is available only after every selected round is completed."
                });
            }

            var result = await _service.GetCampaignResultAsync(userId, campaignId);
            return result == null
                ? NotFound(new { Message = "Interview campaign result was not found." })
                : Ok(result);
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveCampaign()
        {
            if (!TryGetUserId(out int userId))
            {
                return Unauthorized(new
                {
                    Code = "INVALID_USER_IDENTITY",
                    Message = "The authenticated token does not contain a valid UserId."
                });
            }

            var campaign = await _service.GetActiveCampaignAsync(userId);
            return campaign == null ? NoContent() : Ok(campaign);
        }

        [HttpPost("campaign/{campaignId:int}/cancel")]
        public async Task<IActionResult> CancelCampaign(int campaignId)
        {
            if (campaignId <= 0) return BadRequest(new { Message = "ID đợt phỏng vấn không hợp lệ." });
            if (!TryGetUserId(out int userId)) return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });

            var (success, errorMessage, campaign) = await _service.CancelCampaignAsync(userId, campaignId);
            if (!success)
            {
                return Conflict(new
                {
                    Code = "CAMPAIGN_ALREADY_CLOSED",
                    Message = errorMessage
                });
            }
            return Ok(campaign);
        }

        [HttpPost("campaign/{campaignId:int}/expire")]
        public async Task<IActionResult> ExpireCampaign(int campaignId)
        {
            if (campaignId <= 0) return BadRequest(new { Message = "ID đợt phỏng vấn không hợp lệ." });
            if (!TryGetUserId(out int userId)) return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });

            var (success, errorMessage, campaign) = await _service.ExpireCampaignAsync(userId, campaignId);
            if (!success) return BadRequest(new { Message = errorMessage });
            return Ok(campaign);
        }

        [HttpGet("quota")]
        public async Task<IActionResult> GetQuota()
        {
            if (!TryGetUserId(out int userId)) return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng hoặc token không hợp lệ." });
            var quota = await _service.GetQuotaAsync(userId);
            return quota == null ? NotFound(new { Message = "Không tìm thấy người dùng." }) : Ok(quota);
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

        private static ActiveInterviewConflictDto BuildActiveConflict(InterviewCampaignDto campaign)
        {
            var session = campaign.Sessions
                .Where(candidate => candidate.Status == "Active")
                .OrderByDescending(candidate => candidate.UpdatedAt ?? candidate.CreatedAt)
                .FirstOrDefault()
                ?? campaign.Sessions
                    .Where(candidate => candidate.Status == "Pending")
                    .OrderBy(candidate => candidate.CreatedAt)
                    .FirstOrDefault();

            return new ActiveInterviewConflictDto
            {
                CampaignId = campaign.InterviewCampaignId,
                SessionId = session?.InterviewSessionId,
                InterviewType = session?.InterviewRoundType ?? string.Empty,
                Status = session?.Status ?? campaign.Status,
                StartedAt = campaign.StartedAt,
                UpdatedAt = session?.UpdatedAt ?? campaign.UpdatedAt,
                CompletedQuestionCount = session?.CompletedQuestionCount ?? 0,
                CanResume = session != null,
                CanEnd = session?.Status == "Active",
                CanCloseCampaign = campaign.Status == "Pending" || campaign.Status == "Active",
                Campaign = campaign
            };
        }
    }
}
