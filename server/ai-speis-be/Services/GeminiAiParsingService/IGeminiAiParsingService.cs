using System.Threading.Tasks;
using ai_speis_be.DTOs.CvParsing;
using ai_speis_be.DTOs.JdParsing;

namespace ai_speis_be.Services.GeminiAiParsingService
{
    public interface IGeminiAiParsingService
    {
        Task<(bool Success, CvParsedResult? Data, string? RawResponse, string? Error)> ParseCvTextAsync(string cvText);
        Task<(bool Success, JdParsedResult? Data, string? RawResponse, string? Error)> ParseJdTextAsync(string jdText);
        Task<(bool Success, CvJdMatchResultResponse? Data, string? RawResponse, string? Error)> EvaluateCvAgainstJdAsync(string cvJson, string jdJson);
    }
}
