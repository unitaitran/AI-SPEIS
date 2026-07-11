using ai_speis_be.Services.SpeechToTextService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ai_speis_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AudioController : ControllerBase
    {
        private readonly ISpeechToTextService _speechToTextService;

        public AudioController(ISpeechToTextService speechToTextService)
        {
            _speechToTextService = speechToTextService;
        }

        [HttpPost("speech-to-text")]
        [Authorize]
        public async Task<IActionResult> SpeechToText([FromForm] IFormFile audioFile, [FromForm] string languageCode = "vi-VN")
        {
            if (audioFile == null || audioFile.Length == 0)
                return BadRequest(new { Message = "Audio file is required." });

            try
            {
                var transcript = await _speechToTextService.RecognizeSpeechAsync(audioFile, languageCode);
                return Ok(new { transcript });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { Message = "Error transcribing audio: " + ex.Message });
            }
        }
    }
}
