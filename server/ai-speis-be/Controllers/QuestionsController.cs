using ai_speis_be.Services.QuestionService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ai_speis_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QuestionsController : ControllerBase
    {
        private readonly IQuestionService _service;
        public QuestionsController(IQuestionService service)
        {
            _service = service;
        }
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetQuestionsAsync([FromQuery]string? roleTarget, [FromQuery] string? major, [FromQuery] string? difficulty)
        {
            var questions = await _service.GetQuestionsAsync(roleTarget, major, difficulty);
            return Ok(questions);
        }
        [HttpGet("admin/{questionId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetQuestionsByIdAdminAsync([FromRoute]int questionId)
        {
            var questions = await _service.GetQuestionByIdAdminAsync(questionId);
            return Ok(questions);
        }
        [HttpGet("{questionId}")]
        [Authorize]
        public async Task<IActionResult> GetQuestionByIdAsync([FromRoute]int questionId)
        {
            var questions = await _service.GetQuestionByIdAsync(questionId);
            return Ok(questions);
        }
    }
}
