using System.Net.Http.Json;
using System.Text.Json;
using ai_speis_be.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ai_speis_be.Services.RagService
{
    public sealed class QuestionVectorSyncClient : IQuestionVectorSyncClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<QuestionVectorSyncClient> _logger;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public QuestionVectorSyncClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<QuestionVectorSyncClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        private HttpClient GetClient()
        {
            var client = _httpClientFactory.CreateClient("PythonRAG");
            var baseUrl = _configuration["PythonRAG:BaseUrl"]
                ?? _configuration["PYTHON_RAG_BASE_URL"]
                ?? "http://localhost:8000";
            if (client.BaseAddress is null)
            {
                client.BaseAddress = new Uri(baseUrl);
            }
            return client;
        }

        private static object MapToSyncDto(Question q)
        {
            return new
            {
                question_id = q.QuestionId,
                question_content = q.QuestionContent,
                suggested_answer = q.SuggestedAnswer,
                difficulty = q.Difficulty.ToString(),
                role_target = q.RoleTarget,
                major = q.Major,
                question_type = q.QuestionType,
                language = q.Language ?? "vi",
                skill = q.Skill ?? string.Empty,
                experience_level = q.ExperienceLevel ?? string.Empty,
                level_tags = q.LevelTags ?? string.Empty,
                company_category = q.CompanyCategory ?? string.Empty,
                company_subcategory = q.CompanySubcategory ?? string.Empty,
                expected_key_points = q.ExpectedKeyPoints ?? string.Empty,
                scoring_rubric = q.ScoringRubric ?? string.Empty,
                clarification_question = q.ClarificationQuestion ?? string.Empty,
                follow_up_1 = q.FollowUp1 ?? string.Empty,
                follow_up_2 = q.FollowUp2 ?? string.Empty,
                time_limit_seconds = q.TimeLimitSeconds ?? 120,
                keyword_tags = q.KeywordTags ?? string.Empty
            };
        }

        public async Task<bool> SyncQuestionAsync(Question question, CancellationToken cancellationToken = default)
        {
            if (question == null || question.QuestionId <= 0)
            {
                _logger.LogWarning("Invalid question entity provided for vector synchronization.");
                return false;
            }

            try
            {
                var client = GetClient();
                var requestBody = new
                {
                    question = MapToSyncDto(question)
                };

                _logger.LogInformation("Sending Question #{QuestionId} to Python RAG /questions/sync", question.QuestionId);
                var response = await client.PostAsJsonAsync("/questions/sync", requestBody, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Python RAG /questions/sync failed for Question #{QuestionId} with status {StatusCode}: {Error}",
                        question.QuestionId, response.StatusCode, error);
                    return false;
                }

                _logger.LogInformation("Successfully synchronized Question #{QuestionId} to Qdrant via Python RAG.", question.QuestionId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while synchronizing Question #{QuestionId} to Qdrant.", question.QuestionId);
                throw; // Rethrow to allow Hangfire automatic retry
            }
        }

        public async Task<bool> DeleteQuestionAsync(int questionId, CancellationToken cancellationToken = default)
        {
            if (questionId <= 0)
            {
                return true;
            }

            try
            {
                var client = GetClient();
                _logger.LogInformation("Sending DELETE request for Question #{QuestionId} to Python RAG", questionId);
                var response = await client.DeleteAsync($"/questions/{questionId}", cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Python RAG DELETE /questions/{QuestionId} failed with status {StatusCode}: {Error}",
                        questionId, response.StatusCode, error);
                    return false;
                }

                _logger.LogInformation("Successfully deleted Question #{QuestionId} from Qdrant via Python RAG.", questionId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while deleting Question #{QuestionId} from Qdrant.", questionId);
                throw; // Rethrow to allow Hangfire automatic retry
            }
        }

        public async Task<bool> SyncBatchAsync(IReadOnlyList<Question> questions, CancellationToken cancellationToken = default)
        {
            if (questions == null || questions.Count == 0)
            {
                return true;
            }

            try
            {
                var client = GetClient();
                var dtoList = questions.Select(MapToSyncDto).ToList();
                var requestBody = new
                {
                    questions = dtoList
                };

                _logger.LogInformation("Sending batch of {Count} questions to Python RAG /questions/upsert-batch", questions.Count);
                var response = await client.PostAsJsonAsync("/questions/upsert-batch", requestBody, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Python RAG /questions/upsert-batch failed with status {StatusCode}: {Error}",
                        response.StatusCode, error);
                    return false;
                }

                _logger.LogInformation("Successfully synchronized batch of {Count} questions to Qdrant via Python RAG.", questions.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while synchronizing batch of {Count} questions to Qdrant.", questions.Count);
                throw; // Rethrow to allow Hangfire automatic retry
            }
        }
    }
}
