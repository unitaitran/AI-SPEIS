using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ai_speis_be.DTOs.CvParsing;
using ai_speis_be.DTOs.JdParsing;
using Microsoft.Extensions.Configuration;


namespace ai_speis_be.Services.GeminiAiParsingService
{
    public class GeminiAiParsingService : IGeminiAiParsingService
    {
        private readonly string _apiKey;
        private readonly string _model;
        private readonly IHttpClientFactory _httpClientFactory;
        private const string GeminiApiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public GeminiAiParsingService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _apiKey = configuration["GeminiAI:ApiKey"] 
                ?? configuration["GEMINI_API_KEY"]
                ?? configuration["GEMINI_AI_API_KEY"]
                ?? configuration["GeminiAI__ApiKey"]
                ?? throw new InvalidOperationException("Gemini API key is missing. Add GeminiAI:ApiKey or GEMINI_API_KEY to appsettings or environment variables.");
            _model = configuration["GeminiAI:Model"] ?? configuration["GEMINI_AI_MODEL"] ?? "gemini-3-flash-preview";
            _httpClientFactory = httpClientFactory;
        }

        public async Task<(bool Success, CvParsedResult? Data, string? RawResponse, string? Error)> ParseCvTextAsync(string cvText)
        {
            try
            {
                string prompt = @"
You are an expert HR recruiter and document classifier.
You will receive text extracted from a PDF file. The document can be in Vietnamese or English.
Perform ALL 3 steps below and return a single JSON object.
IMPORTANT: YOU MUST OUTPUT ALL EXTRACTED DATA AND ASSESSMENTS STRICTLY IN VIETNAMESE, REGARDLESS OF THE ORIGINAL DOCUMENT'S LANGUAGE.

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
- overallAssessment: 2-3 sentence overall evaluation of the candidate (in Vietnamese)
- strengths: Key strengths (skills, experience, education highlights) (in Vietnamese)
- weaknesses: Areas for improvement or gaps (in Vietnamese)

=== STEP 3: STRUCTURED DATA EXTRACTION (skip if isValidCv=false) ===
RULES:
1. For ""roleTarget"": Extract the applied position or infer from skills/projects. It MUST be normalized to one of: ""Backend"", ""Frontend"", ""Fullstack"", ""Mobile"", ""BA"", ""QA/Tester"", ""DevOps"", ""Data Analyst"", or ""Other"". (If the position is not one of the 8 supported roles above, output ""Other"").
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

                var rawJson = await CallGeminiRestApiWithRetryAsync(prompt);

                if (string.IsNullOrWhiteSpace(rawJson))
                {
                    return (false, null, null, "Không nhận được phản hồi từ Gemini.");
                }

                rawJson = StripMarkdownFence(rawJson);

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

        public async Task<(bool Success, JdParsedResult? Data, string? RawResponse, string? Error)> ParseJdTextAsync(string jdText)
        {
            try
            {
                string prompt = @"
You are an expert IT recruiter and document classifier.
You will receive text extracted from a Job Description (JD) file or raw text input. The JD can be in Vietnamese or English.
Perform ALL 3 steps below and return a single JSON object.
IMPORTANT: YOU MUST OUTPUT ALL EXTRACTED DATA STRICTLY IN VIETNAMESE, REGARDLESS OF THE ORIGINAL JD'S LANGUAGE. Translate fields like Job Title, Experience Level, and Responsibilities if necessary.

=== STEP 1: DOCUMENT CLASSIFICATION ===
Determine if this document is actually a Job Description (JD). Score it from 0.0 to 1.0 based on these signals:
- Has a clear Job Title (weight: 0.20)
- Has Requirements / Required Skills section (weight: 0.25)
- Has Responsibilities / What you will do section (weight: 0.25)
- Has Company introduction / Benefits / Salary (weight: 0.20)
- Is NOT a CV/resume, invoice, contract, report, or syllabus (weight: 0.10)
Sum the weights of signals found to get jdConfidenceScore.
Set isValidJd=false with invalidReason if score < 0.50.

=== STEP 2: STRUCTURED DATA EXTRACTION (skip if isValidJd=false) ===
Extract the following information from the JD (Translate to Vietnamese):
- jobTitle: The main job title being recruited (e.g. Lập trình viên Backend, Chuyên viên Frontend).
- experienceLevel: Normalize and output strictly as one of: ""Intern"", ""Fresher"", ""Junior"", ""Mid-level"", ""Senior"", ""Lead"". (Infer if not explicit).
- roleTarget: Identify the target role. It MUST be normalized to one of the following exact values: ""Backend"", ""Frontend"", ""Fullstack"", ""Mobile"", ""BA"", ""QA/Tester"", ""DevOps"", ""Data Analyst"", or ""Other"". (If the role is not one of the 8 supported roles above, use ""Other"").
- requiredSkills: Array of MUST HAVE technical and soft skills.
- niceToHaveSkills: Array of PLUS or nice-to-have skills.
- responsibilities: A short paragraph summarizing the key responsibilities (max 3 sentences in Vietnamese).
- companyCharacteristics: Extract any specific traits, culture, domain, or environment of the company (e.g. ""Công ty làm Product về EdTech"", ""Môi trường Startup năng động"", ""Làm việc Agile"").

=== STEP 3: JSON FORMATTING ===
Return ONLY a raw JSON object (no markdown tags, no ```json) matching this exact structure:
{
  ""isValidJd"": true/false,
  ""jdConfidenceScore"": 0.85,
  ""invalidReason"": ""Đây là công thức nấu ăn, không phải JD"" (or null if valid),
  ""jobTitle"": ""Lập trình viên Backend"",
  ""experienceLevel"": ""Junior"",
  ""roleTarget"": ""Backend"",
  ""requiredSkills"": [""C#"", "".NET Core"", ""SQL Server""],
  ""niceToHaveSkills"": [""Docker"", ""Redis""],
  ""responsibilities"": ""Phát triển và bảo trì các API. Phối hợp với đội frontend để hoàn thiện tính năng."",
  ""companyCharacteristics"": ""Công ty Product tập trung vào AI trong mảng EdTech. Văn hóa Agile.""
}

Ensure the output is valid JSON.

=== JD TEXT TO ANALYZE ===
" + jdText;

                var rawJson = await CallGeminiRestApiWithRetryAsync(prompt);
                string jsonText = rawJson ?? "";

                jsonText = StripMarkdownFence(jsonText);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<JdParsedResult>(jsonText, options);

                return (true, result, rawJson, null);
            }
            catch (Exception ex)
            {
                return (false, null, null, ex.Message);
            }
        }

        public async Task<(bool Success, CvJdMatchResultResponse? Data, string? RawResponse, string? Error)> EvaluateCvAgainstJdAsync(string cvJson, string jdJson)
        {
            try
            {
                string prompt = $@"
You are an expert Technical Recruiter.
You are given two JSON strings: one represents a Candidate's CV, and the other represents a Job Description (JD).
Your task is to compare them and evaluate how well the candidate fits the job.
Please return a single JSON object.

IMPORTANT: YOU MUST OUTPUT ALL EXTRACTED DATA (Advice, SuitabilityLevel, etc.) STRICTLY IN VIETNAMESE.

=== JSON SCHEMA (return STRICTLY this exact structure, no markdown tags) ===
{{
  ""success"": true,
  ""matchScore"": 85,
  ""suitabilityLevel"": ""Rất phù hợp"", // e.g. ""Rất phù hợp"", ""Phù hợp"", ""Cần cải thiện"", ""Không phù hợp""
  ""matchingSkills"": [""C#"", ""SQL Server"", ""React""],
  ""missingSkills"": [""Docker"", ""Kubernetes""],
  ""advice"": ""Ứng viên có nền tảng tốt về Backend nhưng thiếu kinh nghiệm triển khai Cloud (Docker/K8s). Nên trau dồi thêm.""
}}

=== CV JSON ===
{cvJson}

=== JD JSON ===
{jdJson}
";

                var rawJson = await CallGeminiRestApiWithRetryAsync(prompt);
                string jsonText = rawJson ?? "";

                jsonText = StripMarkdownFence(jsonText);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<CvJdMatchResultResponse>(jsonText, options);

                if (result == null)
                    return (false, null, jsonText, "Lỗi deserialize JSON trả về từ Gemini.");

                return (true, result, rawJson, null);
            }
            catch (Exception ex)
            {
                return (false, null, null, $"Lỗi khi gọi Gemini API: {ex.Message}");
            }
        }

        /// <summary>
        /// Calls the Gemini REST API directly using HttpClient, bypassing the Mscc.GenerativeAI
        /// library which incorrectly uses Application Default Credentials (ADC) even when an
        /// API key is provided, causing PERMISSION_DENIED errors.
        /// </summary>
        private async Task<string?> CallGeminiRestApiWithRetryAsync(string prompt)
        {
            int maxRetries = 3;
            int delayMs = 2000;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    return await CallGeminiRestApiAsync(prompt);
                }
                catch (HttpRequestException ex) when (
                    ex.Message.Contains("503") || 
                    ex.Message.Contains("500") || 
                    ex.Message.Contains("429"))
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

        private async Task<string?> CallGeminiRestApiAsync(string prompt)
        {
            var url = $"{GeminiApiBaseUrl}{_model}:generateContent?key={_apiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var jsonBody = JsonSerializer.Serialize(requestBody, JsonOptions);
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(120);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"The request was not successful. Last API response: {responseText}");
            }

            // Parse the Gemini REST API response
            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;

            if (root.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0)
                {
                    var finalTexts = new List<string>();
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("thought", out var thought) && thought.GetBoolean())
                        {
                            continue; // Skip the reasoning process
                        }
                        if (part.TryGetProperty("text", out var textProp))
                        {
                            finalTexts.Add(textProp.GetString() ?? "");
                        }
                    }
                    if (finalTexts.Count > 0)
                    {
                        return string.Join("\n", finalTexts);
                    }
                    // Fallback to first part if no non-thought text found
                    return parts[0].GetProperty("text").GetString();
                }
            }

            return null;
        }

        private static string StripMarkdownFence(string content)
        {
            var trimmed = content.Trim();
            if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(7);
                if (trimmed.EndsWith("```"))
                {
                    trimmed = trimmed.Substring(0, trimmed.Length - 3);
                }
            }
            else if (trimmed.StartsWith("```"))
            {
                trimmed = trimmed.Substring(3);
                if (trimmed.EndsWith("```"))
                {
                    trimmed = trimmed.Substring(0, trimmed.Length - 3);
                }
            }
            return trimmed.Trim();
        }
    }
}
