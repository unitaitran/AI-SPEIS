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
You are an expert HR recruiter and document classifier.
You will receive text extracted from a PDF file. Perform ALL 3 steps below and return a single JSON object.

=== STEP 1: DOCUMENT CLASSIFICATION ===
Determine if this document is a CV/resume. Score it from 0.0 to 1.0 based on these signals:
- Has applicant/person name (weight: 0.15)
- Has contact info: email, phone, or linkedin (weight: 0.15)
- Has Education section with school/major/graduation info (weight: 0.20)
- Has Skills or Technical Skills section (weight: 0.20)
- Has Experience or Projects section with timeline (weight: 0.20)
- Is NOT an invoice, contract, report, certificate, syllabus, or brochure (weight: 0.10)
Sum the weights of signals found to get cvConfidenceScore.
Set isValidCv=false with invalidReason if score < 0.50.

=== STEP 2: CV ASSESSMENT (skip if isValidCv=false) ===
Write in Vietnamese:
- overallAssessment: 2-3 sentence overall evaluation of the candidate
- strengths: Key strengths (skills, experience, education highlights)
- weaknesses: Areas for improvement or gaps

=== STEP 3: STRUCTURED DATA EXTRACTION (skip if isValidCv=false) ===
RULES:
1. For ""roleTarget"": Extract the applied position or infer from skills/projects.
2. For ""skills"": Extract ONLY technical/programming skills. Categories:
   - ""Language"": Programming languages (Java, C#, Python, JavaScript, etc.)
   - ""Framework"": Frameworks and libraries (React, Spring Boot, .NET, Angular, etc.)
   - ""Database"": Database systems (MySQL, PostgreSQL, MongoDB, SQL Server, etc.)
   - ""Tool"": Development tools (Git, Docker, Jira, Jenkins, Kubernetes, etc.)
   - ""Cloud"": Cloud services (AWS, Azure, GCP, etc.)
   - ""Other"": Other technical skills
   DO NOT include: natural languages, soft skills, or non-technical items.
   Normalize: ""JavaScript"" not ""JS"", ""TypeScript"" not ""TS"". Merge duplicates.
3. For ""projectSummary"": Write 1-2 sentences about WHAT the project does. NOT the duration.
4. For ""projects"": Put duration in the ""duration"" field.
5. For ""experience"": Only actual work/internship at companies. Do NOT merge projects here.

JSON Schema (return STRICTLY this, no markdown, no extra text):
{
  ""isValidCv"": true,
  ""invalidReason"": """",
  ""cvConfidenceScore"": 0.85,
  ""overallAssessment"": """",
  ""strengths"": """",
  ""weaknesses"": """",
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

If isValidCv=false, leave education/experience/projects/skills as empty arrays and roleTarget as empty string.
If a field is not found, leave it as empty string or empty array.

Document text:
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
