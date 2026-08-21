using System.Net.Http.Json;
using System.Text.Json;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ai_speis_be.Services.RagService
{
    public sealed class RagQuestionRetrievalClient : IRagQuestionRetrievalClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RagQuestionRetrievalClient> _logger;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public RagQuestionRetrievalClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<RagQuestionRetrievalClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public Task<RagRetrievalResult> RetrieveQuestionsAsync(
            string jobRole,
            string experienceLevel,
            IReadOnlyList<string> skills,
            string language,
            int count,
            CancellationToken cancellationToken)
        {
            return RetrieveQuestionsAsync(jobRole, experienceLevel, skills, language, count, "technical", cancellationToken);
        }

        public async Task<RagRetrievalResult> RetrieveQuestionsAsync(
            string jobRole,
            string experienceLevel,
            IReadOnlyList<string> skills,
            string language,
            int count,
            string interviewType,
            CancellationToken cancellationToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("PythonRAG");
                var baseUrl = _configuration["PythonRAG:BaseUrl"] ?? "http://localhost:8000";
                if (client.BaseAddress is null)
                {
                    client.BaseAddress = new Uri(baseUrl);
                }

                var effectiveInterviewType = string.Equals(interviewType, "behavioral", StringComparison.OrdinalIgnoreCase)
                    ? "behavioral"
                    : "technical";

                var requestBody = new
                {
                    cv_profile = new
                    {
                        role_target = jobRole,
                        job_role = jobRole,
                        experience_level = experienceLevel,
                        skills = skills
                    },
                    interview_type = effectiveInterviewType,
                    count = Math.Clamp(count, 1, 3),
                    language = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "vi"
                };

                _logger.LogInformation("Calling Python RAG service at {BaseUrl}/questions/retrieve for role '{Role}', type '{Type}' with count {Count}", baseUrl, jobRole, effectiveInterviewType, count);
                var response = await client.PostAsJsonAsync("/questions/retrieve", requestBody, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Python RAG service returned HTTP {StatusCode}: {ErrorDetail}", response.StatusCode, errorText);
                    return new RagRetrievalResult(false, Array.Empty<Question>(), "RAG_SERVICE_UNAVAILABLE", $"HTTP {(int)response.StatusCode}: {errorText}");
                }

                using var jsonDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                var root = jsonDoc.RootElement;
                if (!root.TryGetProperty("questions", out var questionsElement) || questionsElement.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("Python RAG service returned invalid payload schema: missing 'questions' array.");
                    return new RagRetrievalResult(false, Array.Empty<Question>(), "RAG_INVALID_RESPONSE", "Response missing 'questions' array.");
                }

                var rawList = JsonSerializer.Deserialize<List<RagQuestionDto>>(questionsElement.GetRawText(), JsonOptions) ?? new List<RagQuestionDto>();
                if (rawList.Count == 0)
                {
                    _logger.LogWarning("Python RAG service returned 0 eligible questions for role '{Role}'.", jobRole);
                    return new RagRetrievalResult(false, Array.Empty<Question>(), "NO_ELIGIBLE_RAG_QUESTION", "No eligible RAG questions returned.");
                }

                var questions = new List<Question>();
                foreach (var dto in rawList)
                {
                    if (string.IsNullOrWhiteSpace(dto.QuestionText)) continue;

                    var difficulty = Enum.TryParse<QuestionDifficultyEnum>(dto.Difficulty, true, out var parsedDiff)
                        ? parsedDiff
                        : QuestionDifficultyEnum.Medium;

                    var payloadObj = new
                    {
                        source_id = dto.Id ?? string.Empty,
                        subskill = dto.Subskill ?? string.Empty,
                        expected_answer = dto.ExpectedAnswer ?? string.Empty,
                        expected_key_points = dto.ExpectedKeyPoints ?? new List<string>(),
                        clarification_question = dto.ClarificationQuestion ?? dto.FollowUp1 ?? string.Empty,
                        follow_up_1 = dto.FollowUp1 ?? string.Empty,
                        follow_up_2 = dto.FollowUp2 ?? string.Empty
                    };

                    var parsedId = int.TryParse(dto.Id, out var idVal) && idVal > 0 ? idVal : Random.Shared.Next(10000, 99999);
                    var question = new Question
                    {
                        QuestionId = parsedId,
                        QuestionContent = dto.QuestionText,
                        SuggestedAnswer = dto.ExpectedAnswer ?? string.Empty,
                        Skill = string.IsNullOrWhiteSpace(dto.Skill) ? (skills.FirstOrDefault() ?? "General Technical") : dto.Skill,
                        Language = dto.Language ?? language,
                        Difficulty = difficulty,
                        ExperienceLevel = string.IsNullOrWhiteSpace(dto.ExperienceLevel) ? experienceLevel : dto.ExperienceLevel,
                        ExpectedKeyPoints = dto.ExpectedKeyPoints != null && dto.ExpectedKeyPoints.Count > 0 ? string.Join(",", dto.ExpectedKeyPoints) : null,
                        ClarificationQuestion = !string.IsNullOrWhiteSpace(dto.ClarificationQuestion) ? dto.ClarificationQuestion : dto.FollowUp1,
                        FollowUp1 = dto.FollowUp1,
                        FollowUp2 = dto.FollowUp2,
                        IsDeleted = false,
                        QdrantPayloadJson = JsonSerializer.Serialize(payloadObj, JsonOptions)
                    };
                    questions.Add(question);
                }

                _logger.LogInformation("Successfully retrieved {Count} questions from Qdrant RAG via Python service.", questions.Count);
                return new RagRetrievalResult(true, questions, null, null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("RAG question retrieval request was cancelled.");
                return new RagRetrievalResult(false, Array.Empty<Question>(), "RAG_REQUEST_CANCELLED", "Request cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to communicate with Python RAG service.");
                return new RagRetrievalResult(false, Array.Empty<Question>(), "RAG_SERVICE_UNAVAILABLE", ex.Message);
            }
        }
    }
}
