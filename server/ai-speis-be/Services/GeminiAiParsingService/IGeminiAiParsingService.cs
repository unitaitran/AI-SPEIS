using System.Threading.Tasks;
using ai_speis_be.DTOs.CvParsing;

namespace ai_speis_be.Services.GeminiAiParsingService
{
    public interface IGeminiAiParsingService
    {
        Task<(bool Success, CvParsedResult? Data, string? RawResponse, string? Error)> ParseCvTextAsync(string cvText);
    }
}
