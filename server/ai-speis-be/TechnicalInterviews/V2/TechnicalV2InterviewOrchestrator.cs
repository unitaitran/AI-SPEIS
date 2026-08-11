using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.QuestionRepo;
using ai_speis_be.Services.InterviewSessionService;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.TechnicalInterviews.Planning;
using ai_speis_be.TechnicalInterviews.Rubrics;
using ai_speis_be.TechnicalInterviews.Scoring;
using ai_speis_be.TechnicalInterviews.Selection;
using ai_speis_be.TechnicalInterviews.Validation;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.TechnicalInterviews.V2
{
    public sealed class TechnicalV2InterviewOrchestrator : ITechnicalV2InterviewOrchestrator
    {
        private const string RuntimeVersion = "V2";
        private const string RubricVersion = "technical-v2-runtime";
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> SessionGates = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        private readonly ApplicationDbContext _context;
        private readonly ITechnicalQuestionSelectionService _selectionService;
        private readonly ITechnicalInterviewAIProviderResolver _providerResolver;
        private readonly ITechnicalAIResponseValidator _validator;
        private readonly ITechnicalRubricProvider _rubricProvider;
        private readonly ITechnicalRubricScoringService _scoringService;
        private readonly IInterviewSessionService _sessionLifecycle;
        private readonly TechnicalInterviewOptions _options;
        private readonly ILogger<TechnicalV2InterviewOrchestrator> _logger;

        public TechnicalV2InterviewOrchestrator(
            ApplicationDbContext context,
            ITechnicalQuestionSelectionService selectionService,
            ITechnicalInterviewAIProviderResolver providerResolver,
            ITechnicalAIResponseValidator validator,
            ITechnicalRubricProvider rubricProvider,
            ITechnicalRubricScoringService scoringService,
            IInterviewSessionService sessionLifecycle,
            ILogger<TechnicalV2InterviewOrchestrator> logger,
            TechnicalInterviewOptions? options = null)
        {
            _context = context;
            _selectionService = selectionService;
            _providerResolver = providerResolver;
            _validator = validator;
            _rubricProvider = rubricProvider;
            _scoringService = scoringService;
            _sessionLifecycle = sessionLifecycle;
            _options = options ?? new TechnicalInterviewOptions();
            _logger = logger;
        }

        public async Task<TechnicalV2OperationResult<TechnicalV2SessionDto>> InitializeAsync(
            int userId,
            int sessionId,
            InitializeTechnicalV2Request request,
            CancellationToken cancellationToken)
        {
            using var gate = await EnterAsync(sessionId, cancellationToken);
            var session = await LoadSessionAsync(userId, sessionId, cancellationToken);
            var validation = ValidateSession<TechnicalV2SessionDto>(session);
            if (validation is not null) return validation;
            if (session!.InterviewCampaign.Status is InterviewCampaignStatus.Completed or InterviewCampaignStatus.Cancelled or InterviewCampaignStatus.Expired)
                return Failure<TechnicalV2SessionDto>(TechnicalV2OperationStatus.Conflict, "CAMPAIGN_NOT_ACTIVE", "The interview campaign is no longer active.");

            var set = await LoadSetAsync(sessionId, cancellationToken);
            if (set is not null)
            {
                return TechnicalV2OperationResult<TechnicalV2SessionDto>.Ok(BuildSessionDto(session!, set));
            }

            var campaign = session!.InterviewCampaign;
            var jd = campaign.JDExtractedProfile;
            var cv = campaign.CVExtractedProfile;
            var requiredSkills = CleanSkills(request.RequiredSkills);
            if (requiredSkills.Count == 0) requiredSkills = ParseList(jd?.RequiredSkills);
            var jobRole = jd?.RoleTarget ?? jd?.JobTitle ?? cv?.RoleTarget ?? string.Empty;
            var experience = jd?.ExperienceLevel ?? string.Empty;
            var context = new TechnicalSelectionContext
            {
                Language = campaign.Language,
                JobRole = jobRole,
                ExperienceLevel = experience,
                Difficulty = session.Difficulty,
                SelectedSkills = requiredSkills,
                CvSkills = cv?.Skills.Select(item => item.SkillName).Where(item => !string.IsNullOrWhiteSpace(item)).ToList() ?? new(),
                JdSkills = requiredSkills,
                RequiredJdSkills = requiredSkills,
                AskedQuestionIds = new HashSet<int>(),
                CvJdMatchScore = campaign.CvJdMatchScore,
                AiProvider = session.TechnicalAiProvider
            };

            var pool = await _selectionService.PreparePoolAsync(context, cancellationToken);
            if (pool.Candidates.Count == 0)
            {
                return Failure<TechnicalV2SessionDto>(TechnicalV2OperationStatus.ExternalFailure,
                    pool.ErrorCode ?? "NO_TECHNICAL_CANDIDATE", "Could not select technical questions.");
            }

            var targetCount = Math.Clamp(session.QuestionCount > 0 ? session.QuestionCount : 3, 1, 20);
            IReadOnlyList<Question>? aiSelection = null;
            if (targetCount == 3)
            {
                aiSelection = await _selectionService.SelectMainQuestionsWithAIAsync(context, pool.Candidates, 0, 0, cancellationToken);
            }

            var selected = (aiSelection is { Count: > 0 } ? aiSelection : pool.Candidates)
                .GroupBy(item => item.QuestionId)
                .Select(group => group.First())
                .Take(targetCount)
                .ToList();
            if (selected.Count < targetCount)
            {
                return Failure<TechnicalV2SessionDto>(TechnicalV2OperationStatus.ExternalFailure,
                    "INSUFFICIENT_TECHNICAL_QUESTIONS", "The question bank cannot satisfy this interview.");
            }

            set = new TechnicalQuestionSet
            {
                InterviewSessionId = sessionId,
                SelectionSource = aiSelection is { Count: > 0 }
                    ? TechnicalQuestionSetSelectionSource.ExternalAi
                    : TechnicalQuestionSetSelectionSource.DeterministicFallback,
                QuestionCount = selected.Count,
                CoveredSkillsJson = JsonSerializer.Serialize(selected.Select(item => item.Skill).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(), JsonOptions),
                ConstraintsJson = JsonSerializer.Serialize(new { RequiredSkills = requiredSkills, Language = campaign.Language, JobRole = jobRole }, JsonOptions),
                Status = TechnicalQuestionSetStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            for (var index = 0; index < selected.Count; index++)
            {
                var question = selected[index];
                set.Questions.Add(new TechnicalSessionQuestion
                {
                    QuestionId = question.QuestionId,
                    QuestionOrder = index + 1,
                    QuestionType = TechnicalSessionQuestionType.Main,
                    QuestionSnapshotJson = JsonSerializer.Serialize(QuestionSnapshot.FromQuestion(question), JsonOptions),
                    Status = index == 0 ? TechnicalSessionQuestionStatus.Asked : TechnicalSessionQuestionStatus.Pending,
                    AskedAt = index == 0 ? DateTime.UtcNow : null,
                    Skill = question.Skill,
                    Subskill = TechnicalQuestionMetadata.GetSubskill(question.QdrantPayloadJson),
                    DifficultySnapshot = question.Difficulty.ToString(),
                    EvaluationObjective = "Technical competence and practical application"
                });
            }

            await _context.TechnicalQuestionSets.AddAsync(set, cancellationToken);
            _context.AIInteractionLogs.Add(new AIInteractionLog
            {
                InterviewSessionId = sessionId,
                AttemptId = null,
                Provider = ResolveProvider(session).ProviderName,
                Model = string.Empty,
                OperationType = AIInteractionOperationType.QuestionSelection,
                PromptVersion = TechnicalPromptVersions.Selection,
                RubricVersion = RubricVersion,
                Status = aiSelection is { Count: > 0 } ? AIInteractionStatus.Succeeded : AIInteractionStatus.FallbackUsed,
                FallbackUsed = aiSelection is not { Count: > 0 },
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
            return TechnicalV2OperationResult<TechnicalV2SessionDto>.Created(BuildSessionDto(session, set));
        }

        public async Task<TechnicalV2OperationResult<TechnicalV2CurrentQuestionDto>> StartAsync(int userId, int sessionId, CancellationToken cancellationToken)
        {
            using var gate = await EnterAsync(sessionId, cancellationToken);
            var session = await LoadSessionAsync(userId, sessionId, cancellationToken);
            var validation = ValidateSession<TechnicalV2CurrentQuestionDto>(session);
            if (validation is not null) return validation;
            if (session!.Status == InterviewSessionStatus.Completed)
                return Failure<TechnicalV2CurrentQuestionDto>(TechnicalV2OperationStatus.Conflict, "ROUND_COMPLETED", "Technical round is already completed.");
            if (session.Status == InterviewSessionStatus.Cancelled)
                return Failure<TechnicalV2CurrentQuestionDto>(TechnicalV2OperationStatus.Conflict, "SESSION_CANCELLED", "Technical session is cancelled.");
            if (session!.Status is InterviewSessionStatus.Pending or InterviewSessionStatus.Active)
            {
                var lifecycle = await _sessionLifecycle.StartSessionAsync(userId, sessionId);
                if (!lifecycle.Success) return Failure<TechnicalV2CurrentQuestionDto>(TechnicalV2OperationStatus.Conflict, "SESSION_START_REJECTED", lifecycle.ErrorMessage ?? "Session cannot start.");
            }
            var set = await LoadSetAsync(sessionId, cancellationToken);
            if (set is null)
            {
                var initResult = await InitializeAsync(userId, sessionId, new InitializeTechnicalV2Request(), cancellationToken);
                if (initResult.Status == TechnicalV2OperationStatus.Ok || initResult.Status == TechnicalV2OperationStatus.Created)
                {
                    set = await LoadSetAsync(sessionId, cancellationToken);
                }
            }
            if (set is null) return Failure<TechnicalV2CurrentQuestionDto>(TechnicalV2OperationStatus.NotFound, "NOT_INITIALIZED", "Technical interview is not initialized.");
            var current = FindCurrent(set);
            if (current is null) return Failure<TechnicalV2CurrentQuestionDto>(TechnicalV2OperationStatus.Conflict, "ALL_QUESTIONS_ANSWERED", "All questions are answered. Call complete.");
            if (current.Status == TechnicalSessionQuestionStatus.Pending)
            {
                current.Status = TechnicalSessionQuestionStatus.Asked;
                current.AskedAt ??= DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }
            return TechnicalV2OperationResult<TechnicalV2CurrentQuestionDto>.Ok(BuildQuestionDto(current, set));
        }

        public async Task<TechnicalV2OperationResult<TechnicalV2SessionDto>> GetStateAsync(int userId, int sessionId, CancellationToken cancellationToken)
        {
            var session = await LoadSessionAsync(userId, sessionId, cancellationToken);
            var validation = ValidateSession<TechnicalV2SessionDto>(session);
            if (validation is not null) return validation;
            var set = await LoadSetAsync(sessionId, cancellationToken);
            if (set is null && session!.Status != InterviewSessionStatus.Cancelled && session.Status != InterviewSessionStatus.Completed)
            {
                var initResult = await InitializeAsync(userId, sessionId, new InitializeTechnicalV2Request(), cancellationToken);
                if (initResult.Status == TechnicalV2OperationStatus.Ok || initResult.Status == TechnicalV2OperationStatus.Created)
                {
                    set = await LoadSetAsync(sessionId, cancellationToken);
                }
            }
            return set is null
                ? Failure<TechnicalV2SessionDto>(TechnicalV2OperationStatus.NotFound, "NOT_INITIALIZED", "Technical interview is not initialized.")
                : TechnicalV2OperationResult<TechnicalV2SessionDto>.Ok(BuildSessionDto(session!, set));
        }

        public async Task<TechnicalV2OperationResult<TechnicalV2CurrentQuestionDto>> GetCurrentQuestionAsync(int userId, int sessionId, CancellationToken cancellationToken)
        {
            var state = await GetStateAsync(userId, sessionId, cancellationToken);
            if (state.Value?.CurrentQuestion is null)
            {
                return Failure<TechnicalV2CurrentQuestionDto>(state.Status == TechnicalV2OperationStatus.Ok ? TechnicalV2OperationStatus.Conflict : state.Status,
                    state.ErrorCode ?? "NO_CURRENT_QUESTION", state.Message ?? "No current question.");
            }
            return TechnicalV2OperationResult<TechnicalV2CurrentQuestionDto>.Ok(state.Value.CurrentQuestion);
        }

        public async Task<TechnicalV2OperationResult<TechnicalV2SubmitAnswerResponseDto>> SubmitAnswerAsync(
            int userId,
            int sessionId,
            int sessionQuestionId,
            SubmitTechnicalV2AnswerRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            using var gate = await EnterAsync(sessionId, cancellationToken);
            var session = await LoadSessionAsync(userId, sessionId, cancellationToken);
            var validation = ValidateSession<TechnicalV2SubmitAnswerResponseDto>(session);
            if (validation is not null) return validation;
            if (session!.Status is InterviewSessionStatus.Completed or InterviewSessionStatus.Cancelled)
                return Failure<TechnicalV2SubmitAnswerResponseDto>(TechnicalV2OperationStatus.Conflict, "ROUND_CLOSED", "Technical round is closed.");
            var set = await LoadSetAsync(sessionId, cancellationToken);
            if (set is null) return Failure<TechnicalV2SubmitAnswerResponseDto>(TechnicalV2OperationStatus.NotFound, "NOT_INITIALIZED", "Technical interview is not initialized.");
            var question = set.Questions.FirstOrDefault(item => item.TechnicalSessionQuestionId == sessionQuestionId);
            if (question is null) return Failure<TechnicalV2SubmitAnswerResponseDto>(TechnicalV2OperationStatus.NotFound, "QUESTION_NOT_FOUND", "Question does not belong to this technical session.");
            if (string.IsNullOrWhiteSpace(idempotencyKey)) return Failure<TechnicalV2SubmitAnswerResponseDto>(TechnicalV2OperationStatus.BadRequest, "IDEMPOTENCY_KEY_REQUIRED", "Idempotency-Key is required.");
            var transcript = request.Transcript?.Trim() ?? string.Empty;
            if (transcript.Length == 0)
                return Failure<TechnicalV2SubmitAnswerResponseDto>(TechnicalV2OperationStatus.BadRequest, "INVALID_TRANSCRIPT", "Transcript is required.");
            var existing = question.Answer;
            if (existing is not null)
            {
                if (string.Equals(existing.SubmissionIdempotencyKey, idempotencyKey, StringComparison.Ordinal))
                {
                    if (!string.Equals(existing.Transcript, transcript, StringComparison.Ordinal)
                        || !string.Equals(existing.AudioId, request.AudioId, StringComparison.Ordinal)
                        || existing.SttConfidence != request.SttConfidence)
                        return Failure<TechnicalV2SubmitAnswerResponseDto>(TechnicalV2OperationStatus.Conflict, "IDEMPOTENCY_PAYLOAD_MISMATCH", "The idempotency key was already used with another payload.");
                    if (existing.EvaluationStatus == TechnicalAnswerEvaluationStatus.Processing)
                        return Failure<TechnicalV2SubmitAnswerResponseDto>(TechnicalV2OperationStatus.Conflict, "EVALUATION_PROCESSING", "Answer evaluation is still processing.");
                    return TechnicalV2OperationResult<TechnicalV2SubmitAnswerResponseDto>.Ok(BuildSubmitResponse(session!, set, question, existing, FindCurrent(set)));
                }
                return Failure<TechnicalV2SubmitAnswerResponseDto>(TechnicalV2OperationStatus.Conflict, "ALREADY_ANSWERED", "This canonical question already has an answer.");
            }
            if (question.Status != TechnicalSessionQuestionStatus.Asked)
                return Failure<TechnicalV2SubmitAnswerResponseDto>(TechnicalV2OperationStatus.Conflict, "QUESTION_NOT_ACTIVE", "This question is not awaiting an answer.");

            var answer = new TechnicalAnswer
            {
                TechnicalSessionQuestionId = question.TechnicalSessionQuestionId,
                Transcript = transcript,
                AudioId = request.AudioId,
                SubmissionIdempotencyKey = idempotencyKey,
                SttConfidence = request.SttConfidence,
                EvaluationStatus = TechnicalAnswerEvaluationStatus.Processing,
                CreatedAt = DateTime.UtcNow
            };
            question.Answer = answer;
            question.Status = TechnicalSessionQuestionStatus.Answered;
            question.AnsweredAt = DateTime.UtcNow;
            await _context.TechnicalAnswers.AddAsync(answer, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var evaluation = await EvaluateAsync(session!, question, answer, cancellationToken);
            answer.AiProvider = ResolveProvider(session).ProviderName;
            ApplyEvaluation(answer, question, evaluation);

            var rubric = GetRubric();
            var root = ResolveMainQuestion(question, set);
            var decision = ResolveDecision(question, root, set, answer.FinalQuestionScore ?? rubric.MinimumScore, rubric);
            TechnicalSessionQuestion? next = null;
            if (!decision.FinalizeMainQuestion && decision.NextQuestionType is not null)
            {
                var followUpNumber = set.Questions.Count(item =>
                    item.ParentQuestionId == root.TechnicalSessionQuestionId
                    && item.QuestionType == TechnicalSessionQuestionType.FollowUp) + 1;
                next = await CreateSubQuestionAsync(
                    session!,
                    set,
                    root,
                    decision.NextQuestionType.Value,
                    followUpNumber,
                    cancellationToken);
                if (next is null)
                {
                    // A missing bank probe must never strand the round. Finalize
                    // the main chain and advance using the deterministic main order.
                    decision = V2Decision.NextMain;
                }
            }

            if (decision.FinalizeMainQuestion)
            {
                FinalizeMainQuestion(root, set, rubric);
                next = ActivateNextMain(set, root);
                if (next is null)
                {
                    decision = V2Decision.Complete;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            return TechnicalV2OperationResult<TechnicalV2SubmitAnswerResponseDto>.Ok(
                BuildSubmitResponse(session!, set, question, answer, next, decision.ApiDecision));
        }

        public async Task<TechnicalV2OperationResult<TechnicalV2ResultDto>> CompleteAsync(int userId, int sessionId, CancellationToken cancellationToken)
        {
            using var gate = await EnterAsync(sessionId, cancellationToken);
            var session = await LoadSessionAsync(userId, sessionId, cancellationToken);
            var validation = ValidateSession<TechnicalV2ResultDto>(session);
            if (validation is not null) return validation;
            if (session!.Status == InterviewSessionStatus.Cancelled)
                return Failure<TechnicalV2ResultDto>(TechnicalV2OperationStatus.Conflict, "SESSION_CANCELLED", "Technical session is cancelled.");
            var set = await LoadSetAsync(sessionId, cancellationToken);
            if (set is null)
            {
                var initResult = await InitializeAsync(userId, sessionId, new InitializeTechnicalV2Request(), cancellationToken);
                if (initResult.Status == TechnicalV2OperationStatus.Ok || initResult.Status == TechnicalV2OperationStatus.Created)
                {
                    set = await LoadSetAsync(sessionId, cancellationToken);
                }
            }
            if (set is null) return Failure<TechnicalV2ResultDto>(TechnicalV2OperationStatus.NotFound, "NOT_INITIALIZED", "Technical interview is not initialized.");
            var required = set.Questions.Where(question => question.QuestionType == TechnicalSessionQuestionType.Main).ToList();
            if (set.Questions.Any(question => question.Status != TechnicalSessionQuestionStatus.Skipped && question.Answer is null)
                || required.Any(question => !IsMainQuestionFinalized(question, set)))
                return Failure<TechnicalV2ResultDto>(TechnicalV2OperationStatus.Conflict, "QUESTIONS_NOT_FINALIZED", "All technical answers must be finalized before completion.");
            var result = await _context.TechnicalRoundResults.FirstOrDefaultAsync(item => item.InterviewSessionId == sessionId, cancellationToken);
            if (result is null)
            {
                var rubric = GetRubric();
                var score = _scoringService.ScoreSession(required.Select(question => question.Answer!.FinalQuestionScore ?? 0m), rubric, required.Count);
                result = new TechnicalRoundResult
                {
                    InterviewSessionId = sessionId,
                    OverallScore = score,
                    SkillScoresJson = JsonSerializer.Serialize(BuildSkillScores(required), JsonOptions),
                    CriteriaAveragesJson = JsonSerializer.Serialize(BuildCriteriaAverages(required), JsonOptions),
                    AiLevelAssessment = rubric.GetPerformanceBandCode(score),
                    CompletedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };
                _context.TechnicalRoundResults.Add(result);
                set.Status = TechnicalQuestionSetStatus.Completed;
                await _context.SaveChangesAsync(cancellationToken);
            }
            if (session!.Status != InterviewSessionStatus.Completed)
                await _sessionLifecycle.CompleteSessionAsync(userId, sessionId);
            return TechnicalV2OperationResult<TechnicalV2ResultDto>.Ok(BuildResultDto(session!, set, result));
        }

        public async Task<TechnicalV2OperationResult<TechnicalV2ResultDto>> GetResultAsync(int userId, int sessionId, CancellationToken cancellationToken)
        {
            var session = await LoadSessionAsync(userId, sessionId, cancellationToken);
            var validation = ValidateSession<TechnicalV2ResultDto>(session);
            if (validation is not null) return validation;
            var set = await LoadSetAsync(sessionId, cancellationToken);
            var result = await _context.TechnicalRoundResults.AsNoTracking().FirstOrDefaultAsync(item => item.InterviewSessionId == sessionId, cancellationToken);
            return set is null || result is null
                ? Failure<TechnicalV2ResultDto>(TechnicalV2OperationStatus.NotFound, "RESULT_NOT_AVAILABLE", "Technical result is not available.")
                : TechnicalV2OperationResult<TechnicalV2ResultDto>.Ok(BuildResultDto(session!, set, result));
        }

        public async Task<TechnicalV2OperationResult<TechnicalV2ResultDto>> GenerateFeedbackAsync(int userId, int sessionId, CancellationToken cancellationToken)
        {
            var resultOperation = await CompleteAsync(userId, sessionId, cancellationToken);
            if (resultOperation.Value is null) return resultOperation;
            var session = await LoadSessionAsync(userId, sessionId, cancellationToken);
            var set = await LoadSetAsync(sessionId, cancellationToken);
            var result = await _context.TechnicalRoundResults.FirstAsync(item => item.InterviewSessionId == sessionId, cancellationToken);
            if (result.FinalFeedbackStatus == "COMPLETED")
                return TechnicalV2OperationResult<TechnicalV2ResultDto>.Ok(BuildResultDto(session!, set!, result));

            if (result.FinalFeedbackStatus == "PROCESSING"
                && result.FinalFeedbackStartedAt > DateTime.UtcNow.AddMinutes(-5))
            {
                return Failure<TechnicalV2ResultDto>(TechnicalV2OperationStatus.Conflict, "FINAL_FEEDBACK_PROCESSING", "Technical feedback is already being generated.");
            }

            result.FinalFeedbackStatus = "PROCESSING";
            result.FinalFeedbackStartedAt = DateTime.UtcNow;
            result.FinalFeedbackError = null;
            result.FeedbackConcurrencyVersion++;
            await _context.SaveChangesAsync(cancellationToken);
            var rubric = GetRubric();
            var provider = ResolveProvider(session!);
            var ai = await provider.GenerateFinalSummaryAsync(new TechnicalAIFinalSummaryRequest
            {
                RubricVersion = rubric.Version,
                JobRole = session!.InterviewCampaign.JDExtractedProfile?.RoleTarget ?? string.Empty,
                ExperienceLevel = session.InterviewCampaign.JDExtractedProfile?.ExperienceLevel ?? string.Empty,
                Language = session.InterviewCampaign.Language,
                OverallScore = result.OverallScore ?? 0m,
                PerformanceBand = rubric.GetPerformanceBandCode(result.OverallScore ?? 0m),
                MainQuestionResults = set!.Questions.Where(item => item.QuestionType == TechnicalSessionQuestionType.Main).Select(item => new { item.QuestionOrder, item.Skill, Score = item.Answer!.FinalQuestionScore, item.Answer.Transcript }).Cast<object>().ToList()
            }, cancellationToken);
            AddInteractionLog(session, AIInteractionOperationType.FeedbackGeneration, TechnicalPromptVersions.Summary, ai, !ai.Success, ai.ErrorCode);
            if (ai.Success && ai.Data is not null)
            {
                result.AiExecutiveSummary = ai.Data.OverallTechnicalAssessment;
                result.AiStrengths = JsonSerializer.Serialize(ai.Data.Strengths, JsonOptions);
                result.AiGaps = JsonSerializer.Serialize(ai.Data.KnowledgeGaps, JsonOptions);
                result.AiLevelAssessment = ai.Data.OverallTechnicalAssessment;
                result.AiRecommendations = JsonSerializer.Serialize(ai.Data.RecommendationsForImprovement, JsonOptions);
                result.FinalFeedbackJson = JsonSerializer.Serialize(new TechnicalV2SummaryDto
                {
                    OverallTechnicalAssessment = ai.Data.OverallTechnicalAssessment,
                    ExecutiveSummary = ai.Data.OverallTechnicalAssessment,
                    Strengths = ai.Data.Strengths ?? new(),
                    KnowledgeGaps = ai.Data.KnowledgeGaps ?? new(),
                    LevelAssessment = ai.Data.OverallTechnicalAssessment,
                    RecommendationsForImprovement = ai.Data.RecommendationsForImprovement ?? new(),
                    FinalTechnicalScore = result.OverallScore ?? 0m
                }, JsonOptions);
                result.FinalFeedbackStatus = "COMPLETED";
                result.FinalFeedbackModel = ai.Model;
                result.FinalFeedbackPromptVersion = TechnicalPromptVersions.Summary;
                result.FeedbackInputTokens = ai.InputTokens;
                result.FeedbackOutputTokens = ai.OutputTokens;
                result.FeedbackLatencyMs = ai.LatencyMs;
                result.FeedbackRetryCount = ai.RetryCount;
            }
            else
            {
                result.FinalFeedbackStatus = "FALLBACK";
                result.FinalFeedbackError = ai.ErrorCode ?? "AI_FEEDBACK_FAILED";
                result.AiExecutiveSummary = "Technical result calculated from validated answer scores.";
                result.AiStrengths = "[]";
                result.AiGaps = "[]";
                result.AiLevelAssessment = rubric.GetPerformanceBandCode(result.OverallScore ?? 0m);
                result.AiRecommendations = JsonSerializer.Serialize(new[] { "Review the missing evidence for each rubric dimension." }, JsonOptions);
                result.FinalFeedbackJson = JsonSerializer.Serialize(new TechnicalV2SummaryDto
                {
                    OverallTechnicalAssessment = result.AiExecutiveSummary,
                    ExecutiveSummary = result.AiExecutiveSummary,
                    LevelAssessment = result.AiLevelAssessment,
                    RecommendationsForImprovement = ParseListValue(result.AiRecommendations),
                    FinalTechnicalScore = result.OverallScore ?? 0m
                }, JsonOptions);
            }
            await _context.SaveChangesAsync(cancellationToken);
            return TechnicalV2OperationResult<TechnicalV2ResultDto>.Ok(BuildResultDto(session, set, result));
        }

        private async Task<AIProviderResult<TechnicalV2EvaluationResponse>> EvaluateAsync(InterviewSession session, TechnicalSessionQuestion question, TechnicalAnswer answer, CancellationToken cancellationToken)
        {
            var rubric = GetRubric();
            var snapshot = ParseSnapshot(question.QuestionSnapshotJson);
            var context = new TechnicalV2AnswerProcessingContext
            {
                SessionId = session.InterviewSessionId,
                QuestionId = question.QuestionId,
                QuestionType = question.QuestionType.ToString().ToUpperInvariant(),
                QuestionContent = snapshot.QuestionText,
                ExpectedAnswer = snapshot.SuggestedAnswer ?? string.Empty,
                KeyPoints = string.Join(", ", snapshot.ExpectedKeyPoints ?? new()),
                QuestionSpecificRubric = snapshot.ScoringRubric is null ? string.Empty : JsonSerializer.Serialize(snapshot.ScoringRubric, JsonOptions),
                GlobalRubricVersion = rubric.Version,
                Rubric = ToPromptSnapshot(rubric),
                CandidateAnswer = answer.Transcript,
                JobRole = session.InterviewCampaign.JDExtractedProfile?.RoleTarget ?? string.Empty,
                ExperienceLevel = session.InterviewCampaign.JDExtractedProfile?.ExperienceLevel ?? string.Empty,
                Language = session.InterviewCampaign.Language,
                CvContext = string.Empty,
                JdContext = session.InterviewCampaign.JDExtractedProfile?.Responsibilities ?? string.Empty,
                QuestionOrder = question.QuestionOrder,
                TargetQuestionCount = session.QuestionCount,
                ScoringPolicyVersion = rubric.ScoringPolicyVersion,
            };
            var provider = ResolveProvider(session);
            var ai = await provider.EvaluateAnswerV2Async(context, cancellationToken);
            if (ai.Success && ai.Data is not null)
            {
                var check = _validator.ValidateEvaluationV2(ai.Data, rubric, context.BuildAnswerContext());
                if (!check.IsValid)
                {
                    _logger.LogWarning("Technical V2 AI evaluation rejected: {ErrorCode}", check.ErrorCode);
                    var rejected = new AIProviderResult<TechnicalV2EvaluationResponse>
                    {
                        Success = false,
                        ErrorCode = check.ErrorCode ?? "INVALID_V2_EVALUATION",
                        RetryCount = ai.RetryCount,
                        Model = ai.Model,
                        LatencyMs = ai.LatencyMs,
                        InputTokens = ai.InputTokens,
                        OutputTokens = ai.OutputTokens,
                        RawResponse = ai.RawResponse,
                        JsonRecovery = ai.JsonRecovery,
                        StartedAt = ai.StartedAt,
                        CompletedAt = ai.CompletedAt
                    };
                    AddInteractionLog(session, AIInteractionOperationType.AnswerEvaluation, TechnicalPromptVersions.EvaluationV2, rejected, true, rejected.ErrorCode);
                    return rejected;
                }

                if (check.IsPartial)
                {
                    ai = new AIProviderResult<TechnicalV2EvaluationResponse>
                    {
                        Success = true,
                        Data = check.NormalizedEvaluation ?? ai.Data,
                        ErrorCode = check.ErrorCode,
                        PartialEvaluation = true,
                        InvalidCriterionCodes = check.InvalidCriterionCodes,
                        RetryCount = ai.RetryCount,
                        Model = ai.Model,
                        LatencyMs = ai.LatencyMs,
                        InputTokens = ai.InputTokens,
                        OutputTokens = ai.OutputTokens,
                        RawResponse = ai.RawResponse,
                        JsonRecovery = ai.JsonRecovery,
                        StartedAt = ai.StartedAt,
                        CompletedAt = ai.CompletedAt
                    };
                }
            }
            AddInteractionLog(session, AIInteractionOperationType.AnswerEvaluation, TechnicalPromptVersions.EvaluationV2, ai, !ai.Success, ai.ErrorCode);
            return ai;
        }

        private void ApplyEvaluation(TechnicalAnswer answer, TechnicalSessionQuestion question, AIProviderResult<TechnicalV2EvaluationResponse> ai)
        {
            var rubric = GetRubric();
            if (ai.Success && ai.Data is not null)
            {
                var score = _scoringService.ScoreQuestionV2(ai.Data, rubric);
                var dimensions = ai.Data.Evaluation!.DimensionEvaluations!;
                answer.AiCriteriaDetailJson = JsonSerializer.Serialize(dimensions, JsonOptions);
                answer.FinalQuestionScore = score.FinalOverallScore;
                answer.ComputedScore = score.FinalOverallScore;

                var accuracyDim = dimensions.FirstOrDefault(d => string.Equals(d.RubricCode, "ACCURACY", StringComparison.OrdinalIgnoreCase));
                var depthDim = dimensions.FirstOrDefault(d => string.Equals(d.RubricCode, "TECHNICAL_DEPTH", StringComparison.OrdinalIgnoreCase));
                var reasoningDim = dimensions.FirstOrDefault(d => string.Equals(d.RubricCode, "REASONING", StringComparison.OrdinalIgnoreCase));
                var applicationDim = dimensions.FirstOrDefault(d => string.Equals(d.RubricCode, "APPLICATION", StringComparison.OrdinalIgnoreCase));
                var communicationDim = dimensions.FirstOrDefault(d => string.Equals(d.RubricCode, "COMMUNICATION", StringComparison.OrdinalIgnoreCase));

                answer.AiAccuracyScore = accuracyDim?.SuggestedScore;
                answer.AiTechnicalDepthScore = depthDim?.SuggestedScore;
                answer.AiReasoningScore = reasoningDim?.SuggestedScore;
                answer.AiApplicationScore = applicationDim?.SuggestedScore;
                answer.AiCommunicationScore = communicationDim?.SuggestedScore;

                answer.AiTechnicalAccuracyScore = accuracyDim?.SuggestedScore;
                answer.AiProfessionalKnowledgeScore = depthDim?.SuggestedScore;
                answer.AiProblemSolvingReasoningScore = reasoningDim?.SuggestedScore;
                answer.AiCommunicationExplanationScore = communicationDim?.SuggestedScore;

                answer.AiStrengths = "[]";
                answer.AiMissingPoints = JsonSerializer.Serialize(
                    dimensions.SelectMany(item => item.MissingEvidence ?? new List<string>()).Take(5),
                    JsonOptions);
                answer.EvaluationStatus = ai.PartialEvaluation
                    ? TechnicalAnswerEvaluationStatus.Partial
                    : TechnicalAnswerEvaluationStatus.Completed;
                answer.EvaluationModel = ai.Model;
                answer.AiErrorCode = ai.PartialEvaluation ? ai.ErrorCode : null;
            }
            else
            {
                var fallback = BuildFallbackEvaluation(rubric, ai.ErrorCode);
                var dimensions = fallback.Evaluation!.DimensionEvaluations!;
                answer.AiCriteriaDetailJson = JsonSerializer.Serialize(dimensions, JsonOptions);
                answer.FinalQuestionScore = 0m;
                answer.ComputedScore = 0m;
                answer.AiStrengths = "[]";
                answer.AiMissingPoints = JsonSerializer.Serialize(
                    dimensions.SelectMany(item => item.MissingEvidence ?? new List<string>()).ToList(),
                    JsonOptions);
                answer.EvaluationStatus = TechnicalAnswerEvaluationStatus.Fallback;
                answer.AiErrorCode = ai.ErrorCode ?? "AI_EVALUATION_FAILED";
            }
            answer.EvaluationPromptVersion = TechnicalPromptVersions.EvaluationV2;
            answer.EvaluationInputTokens = ai.InputTokens;
            answer.EvaluationOutputTokens = ai.OutputTokens;
            answer.EvaluationLatencyMs = ai.LatencyMs;
            answer.EvaluationRetryCount = ai.RetryCount;
            answer.EvaluatedAt = DateTime.UtcNow;
            question.Status = TechnicalSessionQuestionStatus.Evaluated;
        }

        private static TechnicalV2EvaluationResponse BuildFallbackEvaluation(
            TechnicalRubricDefinition rubric,
            string? errorCode)
        {
            var reason = string.IsNullOrWhiteSpace(errorCode) ? "AI evaluation unavailable." : $"AI evaluation unavailable ({errorCode}).";
            return new TechnicalV2EvaluationResponse
            {
                Evaluation = new TechnicalV2EvaluationPayload
                {
                    DimensionEvaluations = rubric.Dimensions.Select(dimension => new TechnicalV2DimensionEvaluation
                    {
                        RubricCode = dimension.Code,
                        SuggestedScore = rubric.MinimumScore,
                        Evidence = new List<string>(),
                        MissingEvidence = new List<string> { reason }
                    }).ToList()
                }
            };
        }

        private void AddInteractionLog<T>(InterviewSession session, AIInteractionOperationType operation, string promptVersion, AIProviderResult<T> ai, bool fallback, string? errorCode)
        {
            _context.AIInteractionLogs.Add(new AIInteractionLog
            {
                InterviewSessionId = session.InterviewSessionId,
                AttemptId = null,
                Provider = ResolveProvider(session).ProviderName,
                Model = ai.Model ?? string.Empty,
                OperationType = operation,
                PromptVersion = promptVersion,
                RubricVersion = RubricVersion,
                LatencyMs = ai.LatencyMs,
                RetryCount = ai.RetryCount,
                InputTokenCount = ai.InputTokens,
                OutputTokenCount = ai.OutputTokens,
                Status = fallback ? AIInteractionStatus.FallbackUsed : AIInteractionStatus.Succeeded,
                ErrorCode = errorCode,
                RawResponse = ai.RawResponse,
                RecoveryStatus = ai.JsonRecovery?.RecoveryStatus,
                RecoveryFlags = ai.JsonRecovery is null ? null : string.Join(',', ai.JsonRecovery.RecoveryFlags),
                JsonExceptionType = ai.JsonRecovery?.ExceptionType,
                JsonErrorPath = ai.JsonRecovery?.JsonErrorPath,
                JsonErrorOffset = ai.JsonRecovery?.JsonErrorOffset,
                SchemaVersion = RubricVersion,
                FallbackUsed = fallback,
                StartedAt = ai.StartedAt,
                CompletedAt = ai.CompletedAt,
                CreatedAt = DateTime.UtcNow
            });
        }

        private ITechnicalInterviewAIProvider ResolveProvider(InterviewSession session)
        {
            return string.IsNullOrWhiteSpace(session.TechnicalAiProvider)
                ? _providerResolver.Resolve()
                : _providerResolver.ResolveFor(session.TechnicalAiProvider);
        }

        private TechnicalV2SessionDto BuildSessionDto(InterviewSession session, TechnicalQuestionSet set)
        {
            var current = FindCurrent(set);
            var main = set.Questions.Where(item => item.QuestionType == TechnicalSessionQuestionType.Main).ToList();
            return new TechnicalV2SessionDto
            {
                SessionId = session.InterviewSessionId,
                JobRole = set.ConstraintsJson is null ? string.Empty : ParseString(set.ConstraintsJson, "jobRole"),
                ExperienceLevel = session.InterviewCampaign.JDExtractedProfile?.ExperienceLevel ?? string.Empty,
                Language = session.InterviewCampaign.Language,
                RequiredSkills = ParseList(set.ConstraintsJson, "requiredSkills"),
                TargetMainQuestionCount = main.Count,
                CompletedMainQuestionCount = main.Count(item => IsMainQuestionFinalized(item, set)),
                SessionStatus = session.Status.ToString(),
                QuestionSetStatus = set.Status.ToString(),
                EvaluationStatus = current?.Answer?.EvaluationStatus.ToString() ?? "NOT_STARTED",
                RecoverableError = current?.Answer?.AiErrorCode,
                IsComplete = set.Status == TechnicalQuestionSetStatus.Completed,
                CurrentQuestion = current is null ? null : BuildQuestionDto(current, set),
                Transcript = set.Questions
                    .OrderBy(item => item.AskedAt ?? DateTime.MaxValue)
                    .ThenBy(item => item.QuestionOrder)
                    .ThenBy(item => item.TechnicalSessionQuestionId)
                    .SelectMany(item => new[]
                {
                    new TechnicalV2TranscriptEntryDto { SessionQuestionId = item.TechnicalSessionQuestionId, ParentSessionQuestionId = item.ParentQuestionId, Role = "interviewer", Content = ParseSnapshot(item.QuestionSnapshotJson).QuestionText, QuestionType = item.QuestionType.ToString(), Status = item.Status.ToString(), CreatedAt = item.AskedAt ?? set.CreatedAt },
                    item.Answer is null ? null : new TechnicalV2TranscriptEntryDto { SessionQuestionId = item.TechnicalSessionQuestionId, ParentSessionQuestionId = item.ParentQuestionId, Role = "candidate", Content = item.Answer.Transcript, QuestionType = item.QuestionType.ToString(), Status = item.Answer.EvaluationStatus.ToString(), CreatedAt = item.Answer.CreatedAt }
                }).Where(item => item is not null).Cast<TechnicalV2TranscriptEntryDto>().ToList()
            };
        }

        private TechnicalV2SubmitAnswerResponseDto BuildSubmitResponse(InterviewSession session, TechnicalQuestionSet set, TechnicalSessionQuestion question, TechnicalAnswer answer, TechnicalSessionQuestion? next = null, string? decision = null)
        {
            return new TechnicalV2SubmitAnswerResponseDto
            {
                SessionQuestionId = question.TechnicalSessionQuestionId,
                EvaluationStatus = answer.EvaluationStatus.ToString(),
                FallbackUsed = answer.EvaluationStatus == TechnicalAnswerEvaluationStatus.Fallback,
                Decision = decision ?? ToApiDecision(next),
                NextQuestion = next is null ? null : BuildQuestionDto(next, set),
                State = BuildSessionDto(session, set)
            };
        }

        private TechnicalV2ResultDto BuildResultDto(InterviewSession session, TechnicalQuestionSet set, TechnicalRoundResult result)
        {
            var rubric = GetRubric();
            return new TechnicalV2ResultDto
            {
                SessionId = session.InterviewSessionId,
                RubricVersion = rubric.Version,
                ScoringPolicyVersion = rubric.ScoringPolicyVersion,
                OverallScore = result.OverallScore ?? 0m,
                PerformanceBand = rubric.GetPerformanceBandCode(result.OverallScore ?? 0m),
                FinalFeedbackStatus = result.FinalFeedbackStatus,
                MainQuestions = set.Questions.Where(item => item.QuestionType == TechnicalSessionQuestionType.Main).OrderBy(item => item.QuestionOrder).Select(item => BuildQuestionResult(item, rubric, set)).ToList(),
                Summary = new TechnicalV2SummaryDto
                {
                    OverallTechnicalAssessment = result.AiExecutiveSummary ?? string.Empty,
                    ExecutiveSummary = result.AiExecutiveSummary ?? string.Empty,
                    Strengths = ParseListValue(result.AiStrengths),
                    KnowledgeGaps = ParseListValue(result.AiGaps),
                    LevelAssessment = result.AiLevelAssessment ?? string.Empty,
                    RecommendationsForImprovement = ParseListValue(result.AiRecommendations),
                    FinalTechnicalScore = result.OverallScore ?? 0m
                }
            };
        }

        private TechnicalV2QuestionResultDto BuildQuestionResult(TechnicalSessionQuestion question, TechnicalRubricDefinition rubric, TechnicalQuestionSet set, bool includeSubQuestions = true)
        {
            var snapshot = ParseSnapshot(question.QuestionSnapshotJson);
            var answer = question.Answer;
            var dimensions = ParseDimensions(answer?.AiCriteriaDetailJson);
            return new TechnicalV2QuestionResultDto
            {
                SessionQuestionId = question.TechnicalSessionQuestionId,
                QuestionId = question.QuestionId,
                QuestionOrder = question.QuestionOrder,
                Question = snapshot.QuestionText,
                AnswerTranscript = answer?.Transcript,
                Skill = question.Skill,
                Score = answer?.FinalQuestionScore ?? 0m,
                EvaluationStatus = answer?.EvaluationStatus.ToString() ?? "NOT_STARTED",
                ParentSessionQuestionId = question.ParentQuestionId,
                Dimensions = rubric.Dimensions.Select(d =>
                {
                    var item = dimensions.FirstOrDefault(x => string.Equals(x.RubricCode, d.Code, StringComparison.OrdinalIgnoreCase));
                    var score = item?.SuggestedScore ?? 0m;
                    return new TechnicalV2DimensionResultDto
                    {
                        RubricCode = d.Code,
                        Name = d.Name,
                        Score = score,
                        Weight = d.Weight,
                        WeightedScore = Math.Round(score * d.Weight, 4),
                        Evidence = item?.Evidence ?? new(),
                        Strengths = new(),
                        Gaps = item?.MissingEvidence ?? new(),
                        MissingEvidence = item?.MissingEvidence ?? new()
                    };
                }).ToList(),
                SubQuestions = includeSubQuestions
                    ? set.Questions
                        .Where(item => item.ParentQuestionId == question.TechnicalSessionQuestionId)
                        .OrderBy(item => item.QuestionOrder)
                        .Select(item => BuildQuestionResult(item, rubric, set, false))
                        .ToList()
                    : new List<TechnicalV2QuestionResultDto>(),
                Strengths = ParseListValue(answer?.AiStrengths),
                MissingPoints = ParseListValue(answer?.AiMissingPoints)
            };
        }

        private async Task<InterviewSession?> LoadSessionAsync(int userId, int sessionId, CancellationToken cancellationToken)
        {
            return await _context.InterviewSessions
                .Include(item => item.InterviewCampaign).ThenInclude(item => item.JDExtractedProfile)
                .Include(item => item.InterviewCampaign).ThenInclude(item => item.CVExtractedProfile).ThenInclude(item => item.Skills)
                .FirstOrDefaultAsync(item => item.InterviewSessionId == sessionId && item.InterviewCampaign.UserId == userId, cancellationToken);
        }

        private Task<TechnicalQuestionSet?> LoadSetAsync(int sessionId, CancellationToken cancellationToken)
        {
            return _context.TechnicalQuestionSets
                .Include(item => item.Questions).ThenInclude(item => item.Answer)
                .FirstOrDefaultAsync(item => item.InterviewSessionId == sessionId, cancellationToken);
        }

        private static TechnicalSessionQuestion? FindCurrent(TechnicalQuestionSet set)
        {
            var active = set.Questions
                .Where(item => item.Status != TechnicalSessionQuestionStatus.Skipped)
                .OrderBy(item => item.AskedAt ?? DateTime.MaxValue)
                .ThenBy(item => item.QuestionOrder)
                .ThenBy(item => item.TechnicalSessionQuestionId)
                .ToList();
            return active.FirstOrDefault(item => item.Answer?.EvaluationStatus == TechnicalAnswerEvaluationStatus.Processing)
                ?? active.FirstOrDefault(item => item.Status == TechnicalSessionQuestionStatus.Asked && item.Answer is null)
                ?? set.Questions
                    .Where(item => item.QuestionType == TechnicalSessionQuestionType.Main
                        && item.Status == TechnicalSessionQuestionStatus.Pending
                        && item.Answer is null)
                    .OrderBy(item => item.QuestionOrder)
                    .FirstOrDefault();
        }

        private static TechnicalV2CurrentQuestionDto BuildQuestionDto(TechnicalSessionQuestion question, TechnicalQuestionSet set)
        {
            var snapshot = ParseSnapshot(question.QuestionSnapshotJson);
            var mainQuestion = question.QuestionType == TechnicalSessionQuestionType.Main
                ? question
                : set.Questions.FirstOrDefault(item => item.TechnicalSessionQuestionId == question.ParentQuestionId)
                    ?? question;
            return new TechnicalV2CurrentQuestionDto
            {
                SessionQuestionId = question.TechnicalSessionQuestionId,
                QuestionId = question.QuestionId,
                ParentSessionQuestionId = question.ParentQuestionId,
                QuestionType = question.QuestionType.ToString(),
                QuestionOrder = question.QuestionOrder,
                MainQuestionIndex = mainQuestion.QuestionOrder,
                TotalMainQuestions = set.Questions.Count(item => item.QuestionType == TechnicalSessionQuestionType.Main),
                Content = snapshot.QuestionText,
                Skill = question.Skill,
                Subskill = question.Subskill,
                Difficulty = question.DifficultySnapshot,
                TimeLimitSeconds = snapshot.TimeLimitSeconds,
                Status = question.Status.ToString(),
                EvaluationStatus = question.Answer?.EvaluationStatus.ToString() ?? "NOT_STARTED",
                AskedAt = question.AskedAt,
                AnsweredAt = question.AnsweredAt
            };
        }

        private V2Decision ResolveDecision(
            TechnicalSessionQuestion question,
            TechnicalSessionQuestion mainQuestion,
            TechnicalQuestionSet set,
            decimal currentScore,
            TechnicalRubricDefinition rubric)
        {
            var snapshot = ParseSnapshot(mainQuestion.QuestionSnapshotJson);
            var children = set.Questions
                .Where(item => item.ParentQuestionId == mainQuestion.TechnicalSessionQuestionId)
                .ToList();
            var clarificationsUsed = children.Count(item => item.QuestionType == TechnicalSessionQuestionType.Clarification);
            var followUpsUsed = children.Count(item => item.QuestionType == TechnicalSessionQuestionType.FollowUp);
            var totalSubQuestions = clarificationsUsed + followUpsUsed;

            if (totalSubQuestions >= rubric.Limits.MaxTotalSubQuestionsPerMainQuestion)
                return V2Decision.NextMain;

            // Behavioral applies the clarification recovery factor before deciding
            // whether the same main question still needs deeper probes. Technical
            // uses the configured equivalent factor from the legacy runtime.
            var effectiveBaseScore = question.QuestionType == TechnicalSessionQuestionType.FollowUp
                ? ComputeMainBaseScore(mainQuestion, set, rubric)
                : question.QuestionType == TechnicalSessionQuestionType.Clarification
                    ? _scoringService.ApplyClarificationRecovery(currentScore, _options.ClarificationRecoveryFactor, rubric)
                    : currentScore;

            if (effectiveBaseScore < 3m)
            {
                if (question.QuestionType == TechnicalSessionQuestionType.Main
                    && clarificationsUsed < rubric.Limits.MaxClarificationsPerMainQuestion
                    && !string.IsNullOrWhiteSpace(snapshot.ClarificationQuestion))
                {
                    return new V2Decision(
                        false,
                        TechnicalSessionQuestionType.Clarification,
                        "CLARIFICATION");
                }

                return V2Decision.NextMain;
            }

            if (effectiveBaseScore >= 8m)
                return V2Decision.NextMain;

            var desiredFollowUps = effectiveBaseScore < 5m ? 2 : 1;
            desiredFollowUps = Math.Min(desiredFollowUps, rubric.Limits.MaxFollowUpsPerMainQuestion);
            if (followUpsUsed < desiredFollowUps
                && followUpsUsed < rubric.Limits.MaxFollowUpsPerMainQuestion)
            {
                return new V2Decision(
                    false,
                    TechnicalSessionQuestionType.FollowUp,
                    "FOLLOW_UP");
            }

            return V2Decision.NextMain;
        }

        private async Task<TechnicalSessionQuestion?> CreateSubQuestionAsync(
            InterviewSession session,
            TechnicalQuestionSet set,
            TechnicalSessionQuestion mainQuestion,
            TechnicalSessionQuestionType questionType,
            int followUpNumber,
            CancellationToken cancellationToken)
        {
            var snapshot = ParseSnapshot(mainQuestion.QuestionSnapshotJson);
            var locked = BuildLockedMainSnapshot(session, mainQuestion, snapshot);
            var bankQuestion = await _selectionService.SelectBankSubQuestionAsync(
                locked,
                questionType == TechnicalSessionQuestionType.Clarification
                    ? TechnicalAttemptType.Clarification
                    : TechnicalAttemptType.FollowUp,
                followUpNumber,
                cancellationToken);
            if (bankQuestion is null || !bankQuestion.IsSuccess || string.IsNullOrWhiteSpace(bankQuestion.Content))
            {
                _logger.LogWarning(
                    "Technical V2 Question Bank sub-question unavailable for session {SessionId}, main question {QuestionId}. Error={ErrorCode}",
                    session.InterviewSessionId,
                    mainQuestion.TechnicalSessionQuestionId,
                    bankQuestion?.ErrorCode);
                return null;
            }

            var duplicateContent = set.Questions
                .Where(item => item.TechnicalSessionQuestionId == mainQuestion.TechnicalSessionQuestionId
                    || item.ParentQuestionId == mainQuestion.TechnicalSessionQuestionId)
                .Select(item => ParseSnapshot(item.QuestionSnapshotJson).QuestionText)
                .Any(content => string.Equals(content.Trim(), bankQuestion.Content.Trim(), StringComparison.OrdinalIgnoreCase));
            if (duplicateContent)
            {
                _logger.LogWarning(
                    "Technical V2 Question Bank returned a duplicate sub-question for session {SessionId}, main question {QuestionId}.",
                    session.InterviewSessionId,
                    mainQuestion.TechnicalSessionQuestionId);
                return null;
            }

            var childSnapshot = new QuestionSnapshot
            {
                QuestionText = bankQuestion.Content.Trim(),
                Skill = mainQuestion.Skill,
                Difficulty = mainQuestion.DifficultySnapshot,
                SuggestedAnswer = snapshot.SuggestedAnswer,
                ExpectedKeyPoints = snapshot.ExpectedKeyPoints,
                ScoringRubric = snapshot.ScoringRubric,
                TimeLimitSeconds = snapshot.TimeLimitSeconds
            };
            var child = new TechnicalSessionQuestion
            {
                TechnicalQuestionSetId = set.TechnicalQuestionSetId,
                QuestionId = bankQuestion.SourceQuestionId > 0 ? bankQuestion.SourceQuestionId : mainQuestion.QuestionId,
                QuestionOrder = set.Questions.Count == 0 ? 1 : set.Questions.Max(item => item.QuestionOrder) + 1,
                QuestionType = questionType,
                ParentQuestionId = mainQuestion.TechnicalSessionQuestionId,
                QuestionSnapshotJson = JsonSerializer.Serialize(childSnapshot, JsonOptions),
                Status = TechnicalSessionQuestionStatus.Asked,
                AskedAt = DateTime.UtcNow,
                Skill = mainQuestion.Skill,
                Subskill = mainQuestion.Subskill,
                DifficultySnapshot = mainQuestion.DifficultySnapshot,
                EvaluationObjective = mainQuestion.EvaluationObjective
            };
            set.Questions.Add(child);
            return child;
        }

        private static TechnicalLockedMainQuestionSnapshot BuildLockedMainSnapshot(
            InterviewSession session,
            TechnicalSessionQuestion mainQuestion,
            QuestionSnapshot snapshot)
        {
            var difficulty = Enum.TryParse<QuestionDifficultyEnum>(mainQuestion.DifficultySnapshot, true, out var parsedDifficulty)
                ? parsedDifficulty
                : session.Difficulty;
            return new TechnicalLockedMainQuestionSnapshot(
                mainQuestion.QuestionId,
                snapshot.QuestionText,
                snapshot.SuggestedAnswer ?? string.Empty,
                string.Join(", ", snapshot.ExpectedKeyPoints ?? new List<string>()),
                snapshot.ScoringRubric is null ? string.Empty : JsonSerializer.Serialize(snapshot.ScoringRubric, JsonOptions),
                "{}",
                "{}",
                mainQuestion.Skill ?? string.Empty,
                mainQuestion.Subskill,
                difficulty,
                TechnicalQuestionSourceType.JD,
                TechnicalEvaluationObjective.JdCoreKnowledge,
                session.InterviewCampaign.Language,
                RubricVersion,
                null,
                DateTime.UtcNow,
                snapshot.ClarificationQuestion,
                snapshot.FollowUp1,
                snapshot.FollowUp2);
        }

        private void FinalizeMainQuestion(
            TechnicalSessionQuestion mainQuestion,
            TechnicalQuestionSet set,
            TechnicalRubricDefinition rubric)
        {
            if (mainQuestion.Answer is null)
                return;

            var finalScore = ComputeMainFinalScore(mainQuestion, set, rubric);
            mainQuestion.Answer.FinalQuestionScore = finalScore;
            mainQuestion.Answer.ComputedScore = finalScore;
        }

        private decimal ComputeMainFinalScore(
            TechnicalSessionQuestion mainQuestion,
            TechnicalQuestionSet set,
            TechnicalRubricDefinition rubric)
        {
            var children = set.Questions
                .Where(item => item.ParentQuestionId == mainQuestion.TechnicalSessionQuestionId)
                .ToList();
            var clarification = children.FirstOrDefault(item => item.QuestionType == TechnicalSessionQuestionType.Clarification);
            var baseScore = clarification?.Answer is not null
                ? _scoringService.ApplyClarificationRecovery(
                    GetPersistedQuestionScore(clarification.Answer, rubric),
                    _options.ClarificationRecoveryFactor,
                    rubric)
                : GetPersistedQuestionScore(mainQuestion.Answer, rubric);
            var followUpBonus = children
                .Where(item => item.QuestionType == TechnicalSessionQuestionType.FollowUp && item.Answer is not null)
                .Select(item =>
                {
                    var dimensions = ParseDimensions(item.Answer!.AiCriteriaDetailJson);
                    var hasEvidence = dimensions.Any(dimension => dimension.Evidence?.Any(evidence => !string.IsNullOrWhiteSpace(evidence)) == true);
                    return hasEvidence
                        ? Math.Round(GetPersistedQuestionScore(item.Answer, rubric) / Math.Max(rubric.MaximumScore, 1m), rubric.RoundingPrecision, MidpointRounding.AwayFromZero)
                        : 0m;
                })
                .Sum();

            return _scoringService.Normalize(baseScore + Math.Min(2m, followUpBonus), rubric);
        }

        private decimal ComputeMainBaseScore(
            TechnicalSessionQuestion mainQuestion,
            TechnicalQuestionSet set,
            TechnicalRubricDefinition rubric)
        {
            var clarification = set.Questions.FirstOrDefault(item =>
                item.ParentQuestionId == mainQuestion.TechnicalSessionQuestionId
                && item.QuestionType == TechnicalSessionQuestionType.Clarification);
            return clarification?.Answer is null
                ? GetPersistedQuestionScore(mainQuestion.Answer, rubric)
                : _scoringService.ApplyClarificationRecovery(
                    GetPersistedQuestionScore(clarification.Answer, rubric),
                    _options.ClarificationRecoveryFactor,
                    rubric);
        }

        private decimal GetPersistedQuestionScore(TechnicalAnswer? answer, TechnicalRubricDefinition rubric)
        {
            if (answer is null)
                return rubric.MinimumScore;
            var dimensions = ParseDimensions(answer.AiCriteriaDetailJson);
            if (dimensions.Count == rubric.Dimensions.Count
                && dimensions.All(item => item.RubricCode is not null && item.SuggestedScore.HasValue))
            {
                var evaluation = new TechnicalV2EvaluationResponse
                {
                    Evaluation = new TechnicalV2EvaluationPayload
                    {
                        DimensionEvaluations = dimensions
                    }
                };
                return _scoringService.ScoreQuestionV2(evaluation, rubric).FinalOverallScore;
            }

            return answer.FinalQuestionScore ?? rubric.MinimumScore;
        }

        private static TechnicalSessionQuestion? ActivateNextMain(
            TechnicalQuestionSet set,
            TechnicalSessionQuestion completedMain)
        {
            var next = set.Questions
                .Where(item => item.QuestionType == TechnicalSessionQuestionType.Main
                    && item.Status == TechnicalSessionQuestionStatus.Pending
                    && item.Answer is null)
                .OrderBy(item => item.QuestionOrder)
                .FirstOrDefault();
            if (next is not null)
            {
                next.Status = TechnicalSessionQuestionStatus.Asked;
                next.AskedAt ??= DateTime.UtcNow;
            }
            return next;
        }

        private static TechnicalSessionQuestion ResolveMainQuestion(
            TechnicalSessionQuestion question,
            TechnicalQuestionSet set)
        {
            var current = question;
            var visited = new HashSet<int>();
            while (current.ParentQuestionId.HasValue
                && visited.Add(current.TechnicalSessionQuestionId))
            {
                var parent = set.Questions.FirstOrDefault(item => item.TechnicalSessionQuestionId == current.ParentQuestionId.Value);
                if (parent is null)
                    break;
                current = parent;
            }
            return current.QuestionType == TechnicalSessionQuestionType.Main
                ? current
                : question;
        }

        private static bool IsMainQuestionFinalized(
            TechnicalSessionQuestion mainQuestion,
            TechnicalQuestionSet set)
        {
            if (mainQuestion.QuestionType != TechnicalSessionQuestionType.Main
                || mainQuestion.Answer?.EvaluationStatus is not (TechnicalAnswerEvaluationStatus.Completed or TechnicalAnswerEvaluationStatus.Partial or TechnicalAnswerEvaluationStatus.Fallback))
                return false;
            return set.Questions
                .Where(item => item.ParentQuestionId == mainQuestion.TechnicalSessionQuestionId)
                .All(item => item.Answer is not null);
        }

        private static string ToApiDecision(TechnicalSessionQuestion? next) => next?.QuestionType switch
        {
            TechnicalSessionQuestionType.Clarification => "CLARIFICATION",
            TechnicalSessionQuestionType.FollowUp => "FOLLOW_UP",
            _ => next is null ? "COMPLETE" : "NEXT_QUESTION"
        };

        private sealed record V2Decision(
            bool FinalizeMainQuestion,
            TechnicalSessionQuestionType? NextQuestionType,
            string ApiDecision)
        {
            public static V2Decision NextMain { get; } = new(true, null, "NEXT_QUESTION");
            public static V2Decision Complete { get; } = new(true, null, "COMPLETE");
        }

        private TechnicalRubricDefinition GetRubric() => _rubricProvider.GetRequired(RubricVersion);

        private static TechnicalRubricPromptSnapshot ToPromptSnapshot(TechnicalRubricDefinition rubric) => new(
            rubric.MinimumScore,
            rubric.MaximumScore,
            rubric.EvidenceRequiredWhenScoreAbove,
            rubric.Dimensions.Select(item => new TechnicalRubricPromptDimension(item.Code, item.Name, item.Description, item.Weight)).ToImmutableArray(),
            rubric.Levels.Select(item => new TechnicalRubricPromptLevel(item.Code, item.Score, item.Description)).ToImmutableArray());

        private static Dictionary<string, decimal> BuildCriteriaAverages(IEnumerable<TechnicalSessionQuestion> questions)
        {
            return questions
                .SelectMany(item => ParseDimensions(item.Answer?.AiCriteriaDetailJson))
                .Where(item => !string.IsNullOrWhiteSpace(item.RubricCode) && item.SuggestedScore.HasValue)
                .GroupBy(item => item.RubricCode!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Average(item => item.SuggestedScore!.Value), StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, decimal> BuildSkillScores(IEnumerable<TechnicalSessionQuestion> questions)
        {
            return questions
                .Where(item => item.Answer?.FinalQuestionScore.HasValue == true && !string.IsNullOrWhiteSpace(item.Skill))
                .GroupBy(item => item.Skill!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => Math.Round(group.Average(item => item.Answer!.FinalQuestionScore!.Value), 2, MidpointRounding.AwayFromZero),
                    StringComparer.OrdinalIgnoreCase);
        }

        private static List<TechnicalV2DimensionEvaluation> ParseDimensions(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try { return JsonSerializer.Deserialize<List<TechnicalV2DimensionEvaluation>>(json, JsonOptions) ?? new(); }
            catch (JsonException) { return new(); }
        }

        private static QuestionSnapshot ParseSnapshot(string json)
        {
            try { return JsonSerializer.Deserialize<QuestionSnapshot>(json, JsonOptions) ?? new(); }
            catch (JsonException) { return new(); }
        }

        private static List<string> ParseList(string? json, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try { using var document = JsonDocument.Parse(json); return document.RootElement.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array ? property.EnumerateArray().Select(item => item.GetString() ?? string.Empty).Where(item => item.Length > 0).ToList() : new(); }
            catch (JsonException) { return new(); }
        }

        private static string ParseString(string json, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(json)) return string.Empty;
            try { using var document = JsonDocument.Parse(json); return document.RootElement.TryGetProperty(propertyName, out var property) ? property.GetString() ?? string.Empty : string.Empty; }
            catch (JsonException) { return string.Empty; }
        }

        private static List<string> ParseListValue(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try { return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new(); }
            catch (JsonException) { return new(); }
        }

        private static List<string> CleanSkills(IEnumerable<string>? skills) => (skills ?? Array.Empty<string>()).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(50).ToList();

        private static List<string> ParseList(string? json) => string.IsNullOrWhiteSpace(json) ? new() : ParseListValue(json);

        private static TechnicalV2OperationResult<T> Failure<T>(TechnicalV2OperationStatus status, string code, string message) => TechnicalV2OperationResult<T>.Failure(status, code, message);

        private static TechnicalV2OperationResult<T>? ValidateSession<T>(InterviewSession? session)
        {
            if (session is null) return Failure<T>(TechnicalV2OperationStatus.NotFound, "SESSION_NOT_FOUND", "Interview session not found.");
            if (session.InterviewRoundType != InterviewRoundType.Technical) return Failure<T>(TechnicalV2OperationStatus.BadRequest, "WRONG_ROUND_TYPE", "Session is not a technical interview round.");
            if (!string.Equals(session.TechnicalRuntimeVersion, RuntimeVersion, StringComparison.OrdinalIgnoreCase)) return Failure<T>(TechnicalV2OperationStatus.Conflict, "LEGACY_SESSION", "This session belongs to the legacy Technical runtime and is read-only.");
            return null;
        }

        private static async Task<IDisposable> EnterAsync(int sessionId, CancellationToken cancellationToken)
        {
            var gate = SessionGates.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            return new GateLease(gate);
        }

        private sealed class GateLease : IDisposable
        {
            private readonly SemaphoreSlim _inner;
            public GateLease(SemaphoreSlim inner) => _inner = inner;
            public void Dispose() => _inner.Release();
        }

        private sealed class QuestionSnapshot
        {
            public string QuestionText { get; set; } = string.Empty;
            public string? Skill { get; set; }
            public string? Difficulty { get; set; }
            public string? SuggestedAnswer { get; set; }
            public List<string>? ExpectedKeyPoints { get; set; }
            public Dictionary<string, string>? ScoringRubric { get; set; }
            public string? ClarificationQuestion { get; set; }
            public string? FollowUp1 { get; set; }
            public string? FollowUp2 { get; set; }
            public int TimeLimitSeconds { get; set; } = 120;

            public static QuestionSnapshot FromQuestion(Question question) => new()
            {
                QuestionText = question.QuestionContent,
                Skill = question.Skill,
                Difficulty = question.Difficulty.ToString(),
                SuggestedAnswer = question.SuggestedAnswer,
                ExpectedKeyPoints = string.IsNullOrWhiteSpace(question.ExpectedKeyPoints) ? null : question.ExpectedKeyPoints.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(),
                ScoringRubric = ParseRubric(question.ScoringRubric),
                ClarificationQuestion = question.ClarificationQuestion,
                FollowUp1 = question.FollowUp1,
                FollowUp2 = question.FollowUp2,
                TimeLimitSeconds = question.TimeLimitSeconds ?? 120
            };

            private static Dictionary<string, string>? ParseRubric(string? json)
            {
                if (string.IsNullOrWhiteSpace(json)) return null;
                try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions); }
                catch (JsonException) { return null; }
            }
        }

    }
}
