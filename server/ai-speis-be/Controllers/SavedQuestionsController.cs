using ai_speis_be.Services.SavedQuestionService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ai_speis_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SavedQuestionsController : ControllerBase
    {
        private readonly ISavedQuestionService _service;
        public SavedQuestionsController(ISavedQuestionService service)
        {
            _service = service;
        }
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetSavedQuestionsAsync()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized(new { Message = "Không tìm thấy thông tin người dùng trong token." });
            }
            int userId = int.Parse(userIdClaim);
            var savedQuestions = await _service.GetSavedQuestionsAsync(userId);
            return Ok(savedQuestions);
        }
    }
}
