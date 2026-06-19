using ai_speis_be.Services.QuestionService;
using ai_speis_be.Models.DTOs;
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
        [ProducesResponseType(
            typeof(PagedResultDto<QuestionResponseDto>),
            StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResultDto<QuestionResponseDto>>> GetQuestionsAsync(
            [FromQuery] UserQuestionQueryDto query,
            CancellationToken cancellationToken)
        {
            var questions = await _service.GetQuestionsAsync(
                query,
                cancellationToken);

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
