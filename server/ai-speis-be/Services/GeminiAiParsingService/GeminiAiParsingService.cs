using System;
using System.Text.Json;
using System.Threading.Tasks;
using ai_speis_be.DTOs.CvParsing;
using Microsoft.Extensions.Configuration;
using Mscc.GenerativeAI;
using Mscc.GenerativeAI.Types;

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
                var model = googleAi.GenerativeModel(model: "gemini-2.5-flash");

                string prompt = @"
You are an expert HR recruiter and technical interviewer assistant.
Your task is to extract structured information from the following CV/resume text.

IMPORTANT RULES:
1. Return STRICTLY a JSON object matching the schema below. No markdown, no extra text.
2. For ""roleTarget"": Extract the applied position, job title, or main technical expertise (e.g., ""Backend Developer"", ""Frontend Developer"", ""Fullstack Developer"", ""DevOps Engineer"", ""Mobile Developer""). If not explicitly stated, infer from skills and projects.
3. For ""skills"": Extract ONLY technical/programming skills. Categories:
   - ""Language"": Programming languages (Java, C#, Python, JavaScript, etc.)
   - ""Framework"": Frameworks and libraries (React, Spring Boot, .NET, Angular, etc.)
   - ""Database"": Database systems (MySQL, PostgreSQL, MongoDB, SQL Server, etc.)
   - ""Tool"": Development tools (Git, Docker, Jira, Jenkins, Kubernetes, etc.)
   - ""Cloud"": Cloud services (AWS, Azure, GCP, etc.)
   - ""Other"": Other technical skills that don't fit above
   DO NOT include: natural languages (English, Vietnamese), soft skills (teamwork, communication), or non-technical items.
   Normalize skill names: use ""JavaScript"" not ""JS"", ""TypeScript"" not ""TS"". Merge duplicates (""Git"" and ""GitHub"" → keep only ""Git"").
4. For ""projectSummary"": Write a brief 1-2 sentence summary of WHAT the project does and its purpose. Do NOT just put the duration here.
5. For ""projects"": Put project duration in a separate ""duration"" field.
6. For ""experience"": Extract only actual work/internship experience at companies. Do NOT merge projects into experience.

JSON Schema:
{
  ""roleTarget"": """",
  ""education"": [
    { ""school"": """", ""major"": """", ""gpa"": """", ""graduationYear"": """" }
  ],
  ""experience"": [
    { ""company"": """", ""position"": """", ""duration"": """", ""description"": """" }
  ],
  ""projects"": [
    { ""projectName"": """", ""roleDescription"": """", ""technologyStack"": """", ""projectSummary"": """", ""duration"": """" }
  ],
  ""skills"": [
    { ""skillName"": """", ""source"": ""CV"", ""category"": ""Language|Framework|Database|Tool|Cloud|Other"" }
  ]
}

If a field is not found, leave it as an empty string or empty array.

CV text:
--------------------
" + cvText;

                var response = await CallGeminiWithRetryAsync(model, prompt);
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

        private async Task<GenerateContentResponse> CallGeminiWithRetryAsync(GenerativeModel model, string prompt)
        {
            int maxRetries = 3;
            int delayMs = 2000;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    return await model.GenerateContent(prompt);
                }
                catch (Exception ex) when (ex.Message.Contains("503") || ex.Message.Contains("500") || ex.Message.Contains("429"))
                {
                    if (i == maxRetries - 1)
                    {
                        throw;
                    }
                    await Task.Delay(delayMs);
                    delayMs *= 2; // Exponential backoff (2s, 4s, 8s)
                }
            }
            throw new Exception("Quá số lần thử lại khi gọi API Gemini.");
        }
    }
}
