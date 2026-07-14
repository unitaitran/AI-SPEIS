using ai_speis_be.Models.DTOs;
using ai_speis_be.Services.SpeechToTextService;
using ai_speis_be.Services.TextToSpeechService;
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
        private readonly ITextToSpeechService _textToSpeechService;

        public AudioController(
            ISpeechToTextService speechToTextService,
            ITextToSpeechService textToSpeechService)
        {
            _speechToTextService = speechToTextService;
            _textToSpeechService = textToSpeechService;
        }

        [HttpPost("speech-to-text")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SpeechToText([FromForm] SpeechToTextRequestDto request)
        {
            if (request?.AudioFile == null || request.AudioFile.Length == 0)
                return BadRequest(new { Message = "Audio file is required." });

            try
            {
                var transcript = await _speechToTextService.RecognizeSpeechAsync(request.AudioFile, request.LanguageCode);
                return Ok(new { transcript });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { Message = "Error transcribing audio: " + ex.Message });
            }
        }

        [HttpPost("text-to-speech")]
        [Authorize]
        public async Task<IActionResult> TextToSpeech(
            [FromBody] TextToSpeechRequestDto request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request?.Text))
                return BadRequest(new { Message = "Text is required." });

            try
            {
                var audioBytes = await _textToSpeechService.SynthesizeSpeechAsync(request, cancellationToken);

                if (audioBytes == null || audioBytes.Length == 0)
                    return BadRequest(new { Message = "Could not synthesize speech from the provided text." });

                return File(audioBytes, "audio/mpeg");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { Message = "Error synthesizing speech: " + ex.Message });
            }
        }
    }
}
