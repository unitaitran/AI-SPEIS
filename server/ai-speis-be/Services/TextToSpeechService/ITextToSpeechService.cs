using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Services.TextToSpeechService
{
    public interface ITextToSpeechService
    {
        Task<byte[]> SynthesizeSpeechAsync(TextToSpeechRequestDto request, CancellationToken cancellationToken = default);
    }
}
