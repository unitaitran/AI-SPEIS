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
        private const int MaxTextLength = 5000;
        private const int DefaultTimeoutSeconds = 20;
        private readonly ISpeechToTextService _speechToTextService;
        private readonly ITextToSpeechService _textToSpeechService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AudioController> _logger;

        public AudioController(
            ISpeechToTextService speechToTextService,
            ITextToSpeechService textToSpeechService,
            IConfiguration configuration,
            ILogger<AudioController> logger)
        {
            _speechToTextService = speechToTextService;
            _textToSpeechService = textToSpeechService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("stream-stt")]
        public async Task StreamStt([FromQuery] string languageCode = "vi-VN")
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                
                try
                {
                    var transcript = await _speechToTextService.RecognizeSpeechWebSocketAsync(webSocket, languageCode, HttpContext.RequestAborted);
                    
                    if (webSocket.State == System.Net.WebSockets.WebSocketState.Open)
                    {
                        var resultBytes = System.Text.Encoding.UTF8.GetBytes(transcript);
                        await webSocket.SendAsync(new ArraySegment<byte>(resultBytes), System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
                        await webSocket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Transcription finished", CancellationToken.None);
                    }
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, "Error in WebSocket STT");
                    if (webSocket.State == System.Net.WebSockets.WebSocketState.Open)
                    {
                        await webSocket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.InternalServerError, "Error transcribing", CancellationToken.None);
                    }
                }
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        [HttpPost("speech-to-text")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SpeechToText(
            [FromForm] SpeechToTextRequestDto request,
            CancellationToken cancellationToken)
        {
            if (request?.AudioFile == null || request.AudioFile.Length == 0)
                return BadRequest(new { Message = "Audio file is required." });

            try
            {
                var transcript = await _speechToTextService.RecognizeSpeechAsync(
                    request.AudioFile,
                    request.LanguageCode,
                    cancellationToken);
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
                return BadRequest(new { Code = "TTS_TEXT_REQUIRED", Message = "Text is required." });
            if (request.Text.Trim().Length > MaxTextLength)
                return BadRequest(new
                {
                    Code = "TTS_TEXT_TOO_LONG",
                    Message = $"Text cannot exceed {MaxTextLength} characters."
                });

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(GetTimeoutSeconds()));
            try
            {
                var audioBytes = await _textToSpeechService.SynthesizeSpeechAsync(request, timeoutSource.Token);

                if (audioBytes == null || audioBytes.Length == 0)
                {
                    return StatusCode(StatusCodes.Status502BadGateway, new
                    {
                        Code = "TTS_GENERATION_FAILED",
                        Message = "Could not synthesize speech from the provided text."
                    });
                }

                return File(audioBytes, "audio/mpeg");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return StatusCode(StatusCodes.Status504GatewayTimeout, new
                {
                    Code = "TTS_GENERATION_TIMEOUT",
                    Message = "Speech synthesis timed out."
                });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499, new
                {
                    Code = "TTS_REQUEST_CANCELLED",
                    Message = "Speech synthesis was cancelled."
                });
            }
            catch (System.Exception)
            {
                _logger.LogWarning(
                    "Text-to-speech generation failed for session {SessionId}, question {QuestionId}, attempt {AttemptId}.",
                    request.SessionId,
                    request.QuestionId,
                    request.AttemptId);
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    Code = "TTS_GENERATION_FAILED",
                    Message = "Unable to generate question audio."
                });
            }
        }

        private int GetTimeoutSeconds()
        {
            var configured = _configuration["TTS_TIMEOUT_SECONDS"];
            return int.TryParse(configured, out var seconds) && seconds is >= 1 and <= 120
                ? seconds
                : DefaultTimeoutSeconds;
        }
    }
}
