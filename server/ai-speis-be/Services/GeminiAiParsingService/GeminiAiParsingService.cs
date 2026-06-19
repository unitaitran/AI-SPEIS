using System;
using System.Text.Json;
using System.Threading.Tasks;
using ai_speis_be.DTOs.CvParsing;
using Microsoft.Extensions.Configuration;
using Mscc.GenerativeAI;

namespace ai_speis_be.Services.GeminiAiParsingService
{
    public class GeminiAiParsingService : IGeminiAiParsingService
    {
        private readonly string _apiKey;

        public GeminiAiParsingService(IConfiguration configuration)
        {
            _apiKey = configuration["GeminiAI:ApiKey"] 
                ?? throw new InvalidOperationException("Gemini API key is missing. Add GeminiAI:ApiKey to appsettings or environment variables.");
        }

        public async Task<(bool Success, CvParsedResult? Data, string? RawResponse, string? Error)> ParseCvTextAsync(string cvText)
        {
            try
            {
                var googleAi = new GoogleAI(_apiKey);
                var model = googleAi.GenerativeModel(model: "gemini-3.5-flash");

                string prompt = @"
You are an expert HR recruiter assistant. Your task is to extract structured information from the following CV text.
Extract the data and return it STRICTLY as a JSON object matching this schema, without any markdown formatting or extra text:

{
  ""roleTarget"": ""Backend / Frontend / ... (Extract the applied position or main expertise from the CV)"",
  ""education"": [
    { ""school"": """", ""major"": """", ""gpa"": """", ""graduationYear"": """" }
  ],
  ""experience"": [
    { ""company"": """", ""position"": """", ""duration"": """", ""description"": """" }
  ],
  ""projects"": [
    { ""projectName"": """", ""roleDescription"": """", ""technologyStack"": """", ""projectSummary"": """" }
  ],
  ""skills"": [
    { ""skillName"": """", ""source"": ""CV"" }
  ]
}

If a field is not found, leave it as an empty string or empty array.
Here is the CV text:
--------------------
" + cvText;

                var response = await model.GenerateContent(prompt);
                var rawJson = response.Text;

                if (string.IsNullOrWhiteSpace(rawJson))
                {
                    return (false, null, null, "Không nhận được phản hồi từ Gemini.");
                }

                // Remove markdown block if Gemini wraps it in ```json ... ```
                rawJson = rawJson.Trim();
                if (rawJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                {
                    rawJson = rawJson.Substring(7);
                    if (rawJson.EndsWith("```"))
                    {
                        rawJson = rawJson.Substring(0, rawJson.Length - 3);
                    }
                }
                else if (rawJson.StartsWith("```"))
                {
                    rawJson = rawJson.Substring(3);
                    if (rawJson.EndsWith("```"))
                    {
                        rawJson = rawJson.Substring(0, rawJson.Length - 3);
                    }
                }

                rawJson = rawJson.Trim();

                var parsedData = JsonSerializer.Deserialize<CvParsedResult>(rawJson, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (parsedData == null)
                {
                    return (false, null, rawJson, "Lỗi deserialize JSON trả về từ Gemini.");
                }

                return (true, parsedData, rawJson, null);
            }
            catch (Exception ex)
            {
                return (false, null, null, $"Lỗi khi gọi Gemini API: {ex.Message}");
            }
        }
    }
}
