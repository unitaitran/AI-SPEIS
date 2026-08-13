using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Immutable;
using ai_speis_be.Models;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Rubrics;
using ai_speis_be.TechnicalInterviews.Scoring;
using ai_speis_be.TechnicalInterviews.Validation;
using ai_speis_be.BehaviouralInterviews.AI;
using ai_speis_be.BehaviouralInterviews.Rubrics;
using ai_speis_be.BehaviouralInterviews.Scoring;
using ai_speis_be.BehaviouralInterviews.Validation;
using ai_speis_be.BehaviouralInterviews.Configuration;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Services
{
    public interface ISingleQuestionRetryService
    {
        Task<SingleQuestionRetryResultDto> RetryQuestionAsync(int userId, SingleQuestionRetryRequest request, CancellationToken cancellationToken);
        Task<List<SingleQuestionRetryResultDto>> GetRetryHistoryAsync(int userId, int questionId, CancellationToken cancellationToken);
    }

    public sealed class SingleQuestionRetryRequest
    {
        public int QuestionId { get; set; }
        public int? OriginalSessionId { get; set; }
        public string RoundType { get; set; } = "Technical";
        public string Transcript { get; set; } = string.Empty;
    }

    public sealed class SingleQuestionRetryResultDto
    {
        public int RetryId { get; set; }
        public int QuestionId { get; set; }
        public string Question { get; set; } = string.Empty;
        public string? Skill { get; set; }
        public string Transcript { get; set; } = string.Empty;
        public decimal? Score { get; set; }
        public decimal MaxScore { get; set; } = 10m;
        public string EvaluationStatus { get; set; } = string.Empty;
        public List<RetryDimensionDto> Dimensions { get; set; } = new();
        public List<string> Strengths { get; set; } = new();
        public List<string> MissingPoints { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public sealed class RetryDimensionDto
    {
        public string RubricCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public decimal Weight { get; set; }
        public List<string> Evidence { get; set; } = new();
        public List<string> Strengths { get; set; } = new();
        public List<string> MissingEvidence { get; set; } = new();
    }

    public sealed class SingleQuestionRetryService : ISingleQuestionRetryService
    {
        private const string RubricVersion = "technical-v2-runtime";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        private readonly ApplicationDbContext _context;
        private readonly ITechnicalInterviewAIProviderResolver _providerResolver;
        private readonly ITechnicalAIResponseValidator _validator;
        private readonly ITechnicalRubricProvider _rubricProvider;
        private readonly ITechnicalRubricScoringService _scoringService;
        private readonly ILogger<SingleQuestionRetryService> _logger;
        private readonly IBehaviouralInterviewAIProviderResolver _behaviouralProviderResolver;
        private readonly IBehaviouralAIResponseValidator _behaviouralValidator;
        private readonly IBehaviouralRubricProvider _behaviouralRubricProvider;
        private readonly IBehaviouralRubricScoringService _behaviouralScoringService;
        private readonly BehaviouralInterviewOptions _behaviouralOptions;

        public SingleQuestionRetryService(
            ApplicationDbContext context,
            ITechnicalInterviewAIProviderResolver providerResolver,
            ITechnicalAIResponseValidator validator,
            ITechnicalRubricProvider rubricProvider,
            ITechnicalRubricScoringService scoringService,
            ILogger<SingleQuestionRetryService> logger,
            IBehaviouralInterviewAIProviderResolver behaviouralProviderResolver,
            IBehaviouralAIResponseValidator behaviouralValidator,
            IBehaviouralRubricProvider behaviouralRubricProvider,
            IBehaviouralRubricScoringService behaviouralScoringService,
            BehaviouralInterviewOptions behaviouralOptions)
        {
            _context = context;
            _providerResolver = providerResolver;
            _validator = validator;
            _rubricProvider = rubricProvider;
            _scoringService = scoringService;
            _logger = logger;
            _behaviouralProviderResolver = behaviouralProviderResolver;
            _behaviouralValidator = behaviouralValidator;
            _behaviouralRubricProvider = behaviouralRubricProvider;
            _behaviouralScoringService = behaviouralScoringService;
            _behaviouralOptions = behaviouralOptions;
        }

        public async Task<SingleQuestionRetryResultDto> RetryQuestionAsync(int userId, SingleQuestionRetryRequest request, CancellationToken cancellationToken)
        {
            if (request.QuestionId <= 0 || string.IsNullOrWhiteSpace(request.Transcript) || request.Transcript.Trim().Length > 30000)
                throw new ArgumentException("INVALID_REQUEST");
            request.RoundType = string.IsNullOrWhiteSpace(request.RoundType) ? "Technical" : request.RoundType.Trim();
            if (!string.Equals(request.RoundType, "Technical", StringComparison.OrdinalIgnoreCase)
                && !IsBehaviouralRound(request.RoundType))
                throw new ArgumentException("UNSUPPORTED_ROUND_TYPE");

            var question = await _context.Questions.AsNoTracking()
                .FirstOrDefaultAsync(q => q.QuestionId == request.QuestionId && !q.IsDeleted, cancellationToken)
                ?? throw new InvalidOperationException("QUESTION_NOT_FOUND");

            var isBehavioural = IsBehaviouralRound(request.RoundType);
            var questionIsBehavioural = string.Equals(question.QuestionType, "Behavioral", StringComparison.OrdinalIgnoreCase)
                || string.Equals(question.QuestionType, "Behavioural", StringComparison.OrdinalIgnoreCase);
            if (isBehavioural != questionIsBehavioural)
                throw new ArgumentException("ROUND_QUESTION_MISMATCH");

            var retry = new SingleQuestionRetry
            {
                UserId = userId,
                QuestionId = request.QuestionId,
                OriginalSessionId = request.OriginalSessionId,
                RoundType = request.RoundType,
                QuestionSnapshot = question.QuestionContent,
                Skill = question.Skill ?? question.Major,
                Transcript = request.Transcript.Trim(),
                EvaluationStatus = "PROCESSING",
                CreatedAt = DateTime.UtcNow,
            };
            // Question-bank practice and interview-history retries share the
            // same persisted single-question retry record.
            try
            {
                if (IsBehaviouralRound(request.RoundType))
                {
                    await EvaluateBehaviouralAsync(retry, question, request.Transcript.Trim(), cancellationToken);
                }
                else
                {
                    var rubric = _rubricProvider.GetRequired(RubricVersion);
                    var provider = _providerResolver.Resolve();

                    var evalContext = new TechnicalV2AnswerProcessingContext
                {
                    SessionId = 0,
                    QuestionId = request.QuestionId,
                    QuestionType = "MAIN",
                    QuestionContent = question.QuestionContent,
                    ExpectedAnswer = question.SuggestedAnswer ?? string.Empty,
                    KeyPoints = question.ExpectedKeyPoints ?? string.Empty,
                    QuestionSpecificRubric = question.ScoringRubric ?? string.Empty,
                    GlobalRubricVersion = rubric.Version,
                    Rubric = ToPromptSnapshot(rubric),
                    CandidateAnswer = request.Transcript.Trim(),
                    JobRole = question.RoleTarget ?? string.Empty,
                    ExperienceLevel = question.ExperienceLevel ?? string.Empty,
                    Language = question.Language ?? "vi",
                    CvContext = string.Empty,
                    JdContext = string.Empty,
                    QuestionOrder = 1,
                    TargetQuestionCount = 1,
                    ScoringPolicyVersion = rubric.ScoringPolicyVersion,
                };

                    var ai = await provider.EvaluateAnswerV2Async(evalContext, cancellationToken);

                    if (ai.Success && ai.Data?.Evaluation?.DimensionEvaluations is not null)
                {
                    var check = _validator.ValidateEvaluationV2(ai.Data, rubric, evalContext.BuildAnswerContext());
                    if (!check.IsValid)
                        throw new InvalidOperationException(check.ErrorCode ?? "INVALID_V2_EVALUATION");

                    var evalData = check.NormalizedEvaluation ?? ai.Data;

                    var dimensions = evalData.Evaluation!.DimensionEvaluations!;
                    var score = _scoringService.ScoreQuestionV2(evalData, rubric);

                    retry.Score = score.FinalOverallScore;
                    retry.AiCriteriaDetailJson = JsonSerializer.Serialize(dimensions, JsonOptions);
                    retry.AiStrengths = JsonSerializer.Serialize(
                        dimensions
                            .Where(dimension => dimension.Evidence?.Count > 0)
                            .SelectMany(dimension => dimension.Evidence!
                                .Select(evidence => $"{dimension.RubricCode}: {evidence}"))
                            .Take(5),
                        JsonOptions);
                    retry.AiMissingPoints = JsonSerializer.Serialize(
                        dimensions.SelectMany(d => d.MissingEvidence ?? new List<string>()).Take(5), JsonOptions);
                    retry.EvaluationStatus = "COMPLETED";
                    retry.EvaluationModel = ai.Model;
                    retry.EvaluationInputTokens = ai.InputTokens;
                    retry.EvaluationOutputTokens = ai.OutputTokens;
                    retry.EvaluationLatencyMs = ai.LatencyMs;
                }
                    else
                    {
                        retry.Score = 0m;
                        retry.EvaluationStatus = "FAILED";
                        _logger.LogWarning("Single question retry AI evaluation failed for retry {RetryId}: {ErrorCode}", retry.RetryId, ai.ErrorCode);
                    }
                }
            }
            catch (Exception ex)
            {
                retry.Score = 0m;
                retry.EvaluationStatus = "FAILED";
                _logger.LogError(ex, "Single question retry evaluation error for retry {RetryId}", retry.RetryId);
            }

            retry.EvaluatedAt = DateTime.UtcNow;
            _context.SingleQuestionRetries.Add(retry);
            await _context.SaveChangesAsync(cancellationToken);
            return MapToDto(retry);
        }

        private async Task EvaluateBehaviouralAsync(SingleQuestionRetry retry, Question question, string transcript, CancellationToken cancellationToken)
        {
            const string rubricVersion = "behavioural-rubric-v2";
            var rubric = _behaviouralRubricProvider.GetRequired(rubricVersion);
            var provider = _behaviouralProviderResolver.Resolve(_behaviouralOptions.Provider);
            var evaluationRequest = new BehaviouralAIEvaluationRequest
            {
                RubricVersion = rubric.Version,
                Rubric = new { dimensions = rubric.Dimensions.Select(d => new { d.Code, d.Name, d.Description, d.Weight }), levels = rubric.Levels.Select(l => new { l.Code, l.Score, l.Description }) },
                JobRole = question.RoleTarget,
                ExperienceLevel = question.ExperienceLevel ?? string.Empty,
                Language = question.Language ?? "vi",
                Skill = question.Skill ?? question.Major,
                MainQuestion = question.QuestionContent,
                ExpectedKeyPoints = (question.ExpectedKeyPoints ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                QuestionScoringRubric = string.IsNullOrWhiteSpace(question.ScoringRubric)
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string> { ["question"] = question.ScoringRubric },
                AnswerContext = new[] { new BehaviouralAnswerContext("MAIN", question.QuestionContent, transcript) },
                ClarificationsUsed = 0,
                FollowUpsUsed = 0
            };
            var ai = await provider.EvaluateAnswerAsync(evaluationRequest, cancellationToken);
            if (!ai.Success || ai.Data is null)
            {
                retry.Score = 0m;
                retry.EvaluationStatus = "FAILED";
                return;
            }
            var validation = _behaviouralValidator.ValidateEvaluation(ai.Data, rubric, evaluationRequest.AnswerContext);
            if (!validation.IsValid) throw new InvalidOperationException(validation.ErrorCode ?? "INVALID_BEHAVIOURAL_EVALUATION");
            var score = _behaviouralScoringService.ScoreQuestion(ai.Data, rubric);
            retry.Score = score.FinalOverallScore;
            retry.AiCriteriaDetailJson = JsonSerializer.Serialize(ai.Data.DimensionEvaluations, JsonOptions);
            retry.AiStrengths = JsonSerializer.Serialize(ai.Data.DimensionEvaluations.Where(d => d.Evidence.Count > 0).SelectMany(d => d.Evidence.Select(e => $"{d.RubricCode}: {e}" )).Take(5), JsonOptions);
            retry.AiMissingPoints = JsonSerializer.Serialize(ai.Data.DimensionEvaluations.SelectMany(d => d.MissingEvidence).Take(5), JsonOptions);
            retry.EvaluationModel = ai.Model;
            retry.EvaluationInputTokens = ai.InputTokens;
            retry.EvaluationOutputTokens = ai.OutputTokens;
            retry.EvaluationLatencyMs = ai.LatencyMs;
            retry.EvaluationStatus = "COMPLETED";
        }

        public async Task<List<SingleQuestionRetryResultDto>> GetRetryHistoryAsync(int userId, int questionId, CancellationToken cancellationToken)
        {
            var retries = await _context.SingleQuestionRetries
                .AsNoTracking()
                .Where(r => r.UserId == userId && r.QuestionId == questionId)
                .OrderByDescending(r => r.CreatedAt)
                .Take(20)
                .ToListAsync(cancellationToken);

            return retries.Select(MapToDto).ToList();
        }

        private SingleQuestionRetryResultDto MapToDto(SingleQuestionRetry retry)
        {
            var dimensions = ParseDimensions(retry.AiCriteriaDetailJson);
            var isBehavioural = IsBehaviouralRound(retry.RoundType);
            var technicalRubric = isBehavioural ? null : _rubricProvider.GetRequired(RubricVersion);
            var behaviouralRubric = isBehavioural ? _behaviouralRubricProvider.GetRequired("behavioural-rubric-v2") : null;

            return new SingleQuestionRetryResultDto
            {
                RetryId = retry.RetryId,
                QuestionId = retry.QuestionId,
                Question = retry.QuestionSnapshot,
                Skill = retry.Skill,
                Transcript = retry.Transcript ?? string.Empty,
                Score = retry.Score,
                MaxScore = 10m,
                EvaluationStatus = retry.EvaluationStatus,
                Dimensions = dimensions.Select(d =>
                {
                    var rubricDim = technicalRubric?.Dimensions.FirstOrDefault(rd =>
                        string.Equals(rd.Code, d.RubricCode, StringComparison.OrdinalIgnoreCase));
                    var behaviouralDim = behaviouralRubric?.Dimensions.FirstOrDefault(rd =>
                        string.Equals(rd.Code, d.RubricCode, StringComparison.OrdinalIgnoreCase));
                    return new RetryDimensionDto
                    {
                        RubricCode = d.RubricCode ?? string.Empty,
                        Name = rubricDim?.Name ?? behaviouralDim?.Name ?? d.RubricCode ?? string.Empty,
                        Score = d.SuggestedScore ?? 0m,
                        Weight = rubricDim?.Weight ?? behaviouralDim?.Weight ?? 0m,
                        Evidence = d.Evidence?.ToList() ?? new(),
                        Strengths = new(),
                        MissingEvidence = d.MissingEvidence?.ToList() ?? new(),
                    };
                }).ToList(),
                Strengths = ParseStringList(retry.AiStrengths),
                MissingPoints = ParseStringList(retry.AiMissingPoints),
                CreatedAt = retry.CreatedAt,
            };
        }

        private static List<TechnicalV2DimensionEvaluation> ParseDimensions(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try { return JsonSerializer.Deserialize<List<TechnicalV2DimensionEvaluation>>(json, JsonOptions) ?? new(); }
            catch { return new(); }
        }

        private static List<string> ParseStringList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try { return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new(); }
            catch { return new(); }
        }

        private static bool IsBehaviouralRound(string? roundType) =>
            string.Equals(roundType, "Behavior", StringComparison.OrdinalIgnoreCase)
            || string.Equals(roundType, "Behavioral", StringComparison.OrdinalIgnoreCase)
            || string.Equals(roundType, "Behavioural", StringComparison.OrdinalIgnoreCase);

        private static TechnicalRubricPromptSnapshot ToPromptSnapshot(TechnicalRubricDefinition rubric) => new(
            rubric.MinimumScore,
            rubric.MaximumScore,
            rubric.EvidenceRequiredWhenScoreAbove,
            rubric.Dimensions.Select(item => new TechnicalRubricPromptDimension(item.Code, item.Name, item.Description, item.Weight)).ToImmutableArray(),
            rubric.Levels.Select(item => new TechnicalRubricPromptLevel(item.Code, item.Score, item.Description)).ToImmutableArray());
    }
}
