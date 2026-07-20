using System.Text.Json;
using ai_speis_be.Controllers;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Services.SpeechToTextService;
using ai_speis_be.Services.TextToSpeechService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace ai_speis_be.Tests.Controllers;

public sealed class AudioControllerTests
{
    private readonly Mock<ITextToSpeechService> _textToSpeech = new();
    private readonly AudioController _controller;

    public AudioControllerTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TTS_TIMEOUT_SECONDS"] = "20"
            })
            .Build();
        _controller = new AudioController(
            Mock.Of<ISpeechToTextService>(),
            _textToSpeech.Object,
            configuration,
            Mock.Of<ILogger<AudioController>>());
    }

    [Fact]
    public async Task TextToSpeech_ValidQuestion_ReturnsAudioStream()
    {
        _textToSpeech
            .Setup(service => service.SynthesizeSpeechAsync(
                It.IsAny<TextToSpeechRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);

        var result = await _controller.TextToSpeech(new TextToSpeechRequestDto
        {
            Text = "Explain dependency inversion.",
            LanguageCode = "en-US",
            SessionId = 17,
            QuestionId = 9
        }, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("audio/mpeg", file.ContentType);
        Assert.Equal([1, 2, 3], file.FileContents);
    }

    [Fact]
    public async Task TextToSpeech_ProviderFailure_ReturnsStableCodeWithoutExceptionDetails()
    {
        _textToSpeech
            .Setup(service => service.SynthesizeSpeechAsync(
                It.IsAny<TextToSpeechRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("secret provider detail"));

        var result = await _controller.TextToSpeech(new TextToSpeechRequestDto
        {
            Text = "Question"
        }, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, response.StatusCode);
        var json = JsonSerializer.Serialize(response.Value);
        Assert.Contains("TTS_GENERATION_FAILED", json);
        Assert.DoesNotContain("secret provider detail", json);
    }

    [Fact]
    public async Task TextToSpeech_WhitespaceText_ReturnsValidationCode()
    {
        var result = await _controller.TextToSpeech(new TextToSpeechRequestDto
        {
            Text = "   "
        }, CancellationToken.None);

        var response = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("TTS_TEXT_REQUIRED", JsonSerializer.Serialize(response.Value));
        _textToSpeech.Verify(service => service.SynthesizeSpeechAsync(
            It.IsAny<TextToSpeechRequestDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
