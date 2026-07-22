using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.GeminiAiParsingService;
using ai_speis_be.Services.InterviewSessionService;
using ai_speis_be.BehaviouralInterviews.AI;
using ai_speis_be.BehaviouralInterviews.Configuration;
using ai_speis_be.BehaviouralInterviews.DTOs;
using ai_speis_be.BehaviouralInterviews.Rubrics;
using ai_speis_be.BehaviouralInterviews.Scoring;
using ai_speis_be.BehaviouralInterviews.Selection;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.BehaviouralInterviews.Orchestration
{
    public sealed class BehaviouralInterviewOrchestrator : IBehaviouralInterviewOrchestrator
    {
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> SessionGates = new();
        private const string RubricVersion = "behavioural-rubric-v2";
        private const decimal LowSttConfidenceThreshold = 0.70m;
        private const int ShortTranscriptWordCount = 50;
        // Evaluation Framework Level 1 (thang 0–10) — công thức gộp điểm nằm ở BehaviouralRubricScoringService
        private const decimal ClarificationWeight = 0.75m;      // Điểm gốc sau clarification = 75% × điểm clarification
        private const int MinimumCountedQuestionsPerRound = 5;  // Dưới 5 câu (main + FU, không tính clarification) → thêm 1 FU câu cuối
        private const string MainQuestionHint = "Hãy kể rõ tình huống, bạn đã làm gì và kết quả ra sao";

        private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly ApplicationDbContext _context;
        private readonly IBehaviouralQuestionSelectionService _selectionService;
        private readonly IBehaviouralRubricProvider _rubricProvider;
        private readonly IBehaviouralRubricScoringService _scoringService;
        private readonly IBehaviouralInterviewAIProviderResolver _providerResolver;
        private readonly IBehaviouralFollowUpDecisionEngine _decisionEngine;
        private readonly Validation.IBehaviouralAIResponseValidator _validator;
        private readonly IGeminiAiParsingService _aiParsingService;
        private readonly IInterviewSessionService _sessionLifecycleService;
        private readonly BehaviouralInterviewOptions _options;
        private readonly ILogger<BehaviouralInterviewOrchestrator> _logger;

        public BehaviouralInterviewOrchestrator(
            ApplicationDbContext context,
            IBehaviouralQuestionSelectionService selectionService,
            IBehaviouralRubricProvider rubricProvider,
            IBehaviouralRubricScoringService scoringService,
            IBehaviouralInterviewAIProviderResolver providerResolver,
            IBehaviouralFollowUpDecisionEngine decisionEngine,
            Validation.IBehaviouralAIResponseValidator validator,
            IGeminiAiParsingService aiParsingService,
            IInterviewSessionService sessionLifecycleService,
            BehaviouralInterviewOptions options,
            ILogger<BehaviouralInterviewOrchestrator> logger)
        {
            _context = context;
            _selectionService = selectionService;
            _rubricProvider = rubricProvider;
            _scoringService = scoringService;
            _providerResolver = providerResolver;
            _decisionEngine = decisionEngine;
            _validator = validator;
            _aiParsingService = aiParsingService;
            _sessionLifecycleService = sessionLifecycleService;
            _options = options;
            _logger = logger;
        }

        public async Task<BehaviouralOperationResult<BehaviouralInterviewSessionDto>> InitializeAsync(
            int userId,
            InitializeBehaviouralInterviewRequest request,
            CancellationToken cancellationToken = default)
        {
            var sessionGate = SessionGates.GetOrAdd(request.InterviewSessionId, _ => new SemaphoreSlim(1, 1));
            await sessionGate.WaitAsync(cancellationToken);
            try
            {
            var session = await LoadSessionAsync(userId, request.InterviewSessionId, cancellationToken);
            if (session is null)
            {
                return Failure<BehaviouralInterviewSessionDto>(
                    BehaviouralOperationStatus.NotFound, "SESSION_NOT_FOUND", "Interview session not found.");
            }

            if (session.InterviewRoundType != InterviewRoundType.Behavior)
            {
                return Failure<BehaviouralInterviewSessionDto>(
                    BehaviouralOperationStatus.BadRequest, "WRONG_ROUND_TYPE", "Session is not a behavioural interview round.");
            }

            var existingSet = await LoadCanonicalQuestionSetAsync(
                request.InterviewSessionId,
                cancellationToken);

            if (existingSet is not null)
            {
                return BehaviouralOperationResult<BehaviouralInterviewSessionDto>.Ok(
                    BuildSessionDto(
                        session,
                        existingSet,
                        existingSet.BehaviourSessionQuestion.ToList(),
                        roundResult: null));
            }

            var questionSet = CreateQuestionSet(session, request.RequiredSkills);
            await _context.BehaviourQuestionSets.AddAsync(questionSet, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return BehaviouralOperationResult<BehaviouralInterviewSessionDto>.Created(
                BuildSessionDto(session, questionSet, questions: Array.Empty<BehaviourSessionQuestion>(), roundResult: null));
            }
            finally
            {
                sessionGate.Release();
            }
        }

        public async Task<BehaviouralOperationResult<BehaviouralCurrentQuestionDto>> StartAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken = default)
        {
            var sessionGate = SessionGates.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
            await sessionGate.WaitAsync(cancellationToken);
            try
            {
            var session = await LoadSessionAsync(userId, sessionId, cancellationToken);
            if (session is null)
            {
                return Failure<BehaviouralCurrentQuestionDto>(
                    BehaviouralOperationStatus.NotFound, "SESSION_NOT_FOUND", "Interview session not found.");
            }

            if (session.InterviewRoundType != InterviewRoundType.Behavior)
            {
                return Failure<BehaviouralCurrentQuestionDto>(
                    BehaviouralOperationStatus.BadRequest, "WRONG_ROUND_TYPE", "Session is not a behavioural interview round.");
            }

            if (session.Status == InterviewSessionStatus.Pending
                || session.Status == InterviewSessionStatus.Active)
            {
                var lifecycleResult = await _sessionLifecycleService.StartSessionAsync(userId, sessionId);
                if (!lifecycleResult.Success)
                {
                    return Failure<BehaviouralCurrentQuestionDto>(
                        BehaviouralOperationStatus.Conflict,
                        "SESSION_START_REJECTED",
                        lifecycleResult.ErrorMessage ?? "The behavioural session cannot be started.");
                }
            }
            else if (session.Status != InterviewSessionStatus.Completed)
            {
                return Failure<BehaviouralCurrentQuestionDto>(
                    BehaviouralOperationStatus.Conflict,
                    "INVALID_SESSION_STATUS",
                    "Only a pending or active session can start a behavioural interview.");
            }

            var questionSet = await LoadCanonicalQuestionSetAsync(sessionId, cancellationToken);

            if (questionSet is null)
            {
                questionSet = CreateQuestionSet(session, requiredSkills: null);
                await _context.BehaviourQuestionSets.AddAsync(questionSet, cancellationToken);
            }
            else if (questionSet.Status == BehaviourQuestionSetStatus.Completed)
            {
                return Failure<BehaviouralCurrentQuestionDto>(
                    BehaviouralOperationStatus.Conflict, "ROUND_COMPLETED", "Behavioural round is already completed.");
            }

            // Đã lock rồi → idempotent: refresh không chọn lại (brief §9), trả câu hiện tại
            if (questionSet.BehaviourSessionQuestion.Count > 0)
            {
                var current = FindCurrentQuestion(questionSet.BehaviourSessionQuestion);
                if (current is null)
                {
                    return Failure<BehaviouralCurrentQuestionDto>(
                        BehaviouralOperationStatus.Conflict, "ALL_QUESTIONS_ANSWERED", "All questions are answered. Call complete.");
                }

                if (current.Status == BehaviourQuestionStatus.Pending)
                {
                    current.Status = BehaviourQuestionStatus.Asked;
                    current.AskedAt ??= DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);
                }

                return BehaviouralOperationResult<BehaviouralCurrentQuestionDto>.Ok(
                    BuildQuestionDto(current, questionSet));
            }

            // Match Score quyết định phân bổ nguồn câu hỏi CV/JD — tính lazy trước khi chọn câu
            await EnsureCvJdMatchScoreAsync(session.InterviewCampaign, cancellationToken);

            var selectionContext = BuildSelectionContext(session, questionSet);
            var selection = await _selectionService.SelectAsync(selectionContext, cancellationToken);
            if (selection.Questions.Count == 0)
            {
                return Failure<BehaviouralCurrentQuestionDto>(
                    BehaviouralOperationStatus.ExternalFailure,
                    selection.ErrorCode ?? "SELECTION_FAILED",
                    "Could not build a behavioural question set for this session.");
            }

            questionSet.SelectionSource = selection.FallbackUsed
                ? BehaviourSelectionSource.RuleBasedFallback
                : BehaviourSelectionSource.ExternalAi;
            questionSet.QuestionCount = selection.Questions.Count;
            questionSet.CoveredSkillsJson = JsonSerializer.Serialize(selection.CoveredSkills, SnapshotJsonOptions);

            var now = DateTime.UtcNow;
            foreach (var selected in selection.Questions)
            {
                var sessionQuestion = new BehaviourSessionQuestion
                {
                    BehaviourQuestionSet = questionSet,
                    QuestionId = selected.Question.QuestionId,
                    QuestionOrder = selected.Order,
                    QuestionType = BehaviourQuestionType.Main,
                    QuestionSnapshotJson = JsonSerializer.Serialize(
                        BehaviourQuestionSnapshot.FromQuestion(selected.Question), SnapshotJsonOptions),
                    SelectionReason = selected.SelectionReason,
                    EvaluationGoal = selected.EvaluationGoal,
                    Status = selected.Order == 1 ? BehaviourQuestionStatus.Asked : BehaviourQuestionStatus.Pending,
                    AskedAt = selected.Order == 1 ? now : null
                };
                questionSet.BehaviourSessionQuestion.Add(sessionQuestion);
            }

            await _context.SaveChangesAsync(cancellationToken);

            var firstQuestion = questionSet.BehaviourSessionQuestion
                .First(q => q.QuestionOrder == 1 && q.QuestionType == BehaviourQuestionType.Main);
            return BehaviouralOperationResult<BehaviouralCurrentQuestionDto>.Created(
                BuildQuestionDto(firstQuestion, questionSet));
            }
            finally
            {
                sessionGate.Release();
            }
        }

        public async Task<BehaviouralOperationResult<BehaviouralInterviewSessionDto>> GetSessionAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken = default)
        {
            var session = await LoadSessionAsync(userId, sessionId, cancellationToken);
            if (session is null)
            {
                return Failure<BehaviouralInterviewSessionDto>(
                    BehaviouralOperationStatus.NotFound, "SESSION_NOT_FOUND", "Interview session not found.");
            }

            var questionSet = await LoadCanonicalQuestionSetAsync(sessionId, cancellationToken);

            if (questionSet is null)
            {
                return Failure<BehaviouralInterviewSessionDto>(
                    BehaviouralOperationStatus.NotFound, "NOT_INITIALIZED", "Behavioural interview is not initialized.");
            }

            var roundResult = await _context.BehaviourRoundResults
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.InterviewSessionId == sessionId, cancellationToken);

            return BehaviouralOperationResult<BehaviouralInterviewSessionDto>.Ok(
                BuildSessionDto(session, questionSet, questionSet.BehaviourSessionQuestion.ToList(), roundResult));
        }

        public async Task<BehaviouralOperationResult<BehaviouralCurrentQuestionDto>> GetCurrentQuestionAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken = default)
        {
            var session = await LoadSessionAsync(userId, sessionId, cancellationToken);
            if (session is null)
            {
                return Failure<BehaviouralCurrentQuestionDto>(
                    BehaviouralOperationStatus.NotFound, "SESSION_NOT_FOUND", "Interview session not found.");
            }

            var questionSet = await LoadCanonicalQuestionSetAsync(sessionId, cancellationToken);

            if (questionSet is null || questionSet.BehaviourSessionQuestion.Count == 0)
            {
                return Failure<BehaviouralCurrentQuestionDto>(
                    BehaviouralOperationStatus.NotFound, "NOT_STARTED", "Behavioural interview has not started.");
            }

            var current = FindCurrentQuestion(questionSet.BehaviourSessionQuestion);
            if (current is null)
            {
                return Failure<BehaviouralCurrentQuestionDto>(
                    BehaviouralOperationStatus.Conflict, "ALL_QUESTIONS_ANSWERED", "All questions are answered. Call complete.");
            }

            return BehaviouralOperationResult<BehaviouralCurrentQuestionDto>.Ok(
                BuildQuestionDto(current, questionSet));
        }

        public async Task<BehaviouralOperationResult<BehaviouralSubmitAnswerResponseDto>> SubmitAnswerAsync(
            int userId,
            int sessionId,
            SubmitBehaviouralAnswerRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            var session = await LoadSessionAsync(userId, sessionId, cancellationToken);
            if (session is null)
            {
                return Failure<BehaviouralSubmitAnswerResponseDto>(
                    BehaviouralOperationStatus.NotFound, "SESSION_NOT_FOUND", "Interview session not found.");
            }

            var questionSet = await LoadCanonicalQuestionSetAsync(sessionId, cancellationToken);

            if (questionSet is null || questionSet.BehaviourSessionQuestion.Count == 0)
            {
                return Failure<BehaviouralSubmitAnswerResponseDto>(
                    BehaviouralOperationStatus.NotFound, "NOT_STARTED", "Behavioural interview has not started.");
            }

            if (questionSet.Status == BehaviourQuestionSetStatus.Completed)
            {
                return Failure<BehaviouralSubmitAnswerResponseDto>(
                    BehaviouralOperationStatus.Conflict, "ROUND_COMPLETED", "Behavioural round is already completed.");
            }

            var sessionQuestion = questionSet.BehaviourSessionQuestion
                .FirstOrDefault(q => q.BehaviourSessionQuestionId == request.SessionQuestionId);
            if (sessionQuestion is null)
            {
                return Failure<BehaviouralSubmitAnswerResponseDto>(
                    BehaviouralOperationStatus.NotFound, "QUESTION_NOT_FOUND", "Session question not found.");
            }

            if (sessionQuestion.Status != BehaviourQuestionStatus.Asked)
            {
                return Failure<BehaviouralSubmitAnswerResponseDto>(
                    BehaviouralOperationStatus.Conflict, "QUESTION_NOT_ACTIVE", "This question is not awaiting an answer.");
            }

            if (sessionQuestion.BehaviourAnswerAnswer is not null)
            {
                return Failure<BehaviouralSubmitAnswerResponseDto>(
                    BehaviouralOperationStatus.Conflict, "ALREADY_ANSWERED", "This question already has an answer.");
            }

            var mainQuestion = ResolveMainQuestion(sessionQuestion, questionSet.BehaviourSessionQuestion);
            var mainSnapshot = ParseSnapshot(mainQuestion.QuestionSnapshotJson);
            var chain = GetQuestionChain(mainQuestion, questionSet.BehaviourSessionQuestion);
            var clarificationsUsed = chain.Count(q => q.QuestionType == BehaviourQuestionType.Clarification);
            var followUpsUsed = chain.Count(q =>
                q.QuestionType is BehaviourQuestionType.FollowUp1 or BehaviourQuestionType.FollowUp2);

            var rubric = _rubricProvider.GetRequired(RubricVersion);
            var now = DateTime.UtcNow;

            var answer = new BehaviourAnswer
            {
                BehaviourSessionQuestion = sessionQuestion,
                BehaviourSessionQuestionId = sessionQuestion.BehaviourSessionQuestionId,
                Transcript = request.Transcript,
                SttConfidence = request.SttConfidence,
                CreatedAt = now
            };

            BehaviouralDecisionOutcome outcome;
            string evaluationStatus = "COMPLETED";

            // Rule chặn trước AI (brief §21): audio kém + trả lời quá ngắn → clarification, không gọi AI
            var wordCount = request.Transcript.Split(
                (char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            var lowQualityInput = request.SttConfidence is not null
                && request.SttConfidence < LowSttConfidenceThreshold
                && wordCount < ShortTranscriptWordCount;

            if (lowQualityInput
                && clarificationsUsed < rubric.Limits.MaxClarificationsPerMainQuestion
                && !string.IsNullOrWhiteSpace(mainSnapshot.ClarificationQuestion))
            {
                outcome = new BehaviouralDecisionOutcome(
                    BehaviourResolvedAction.Clarification, false, BehaviourQuestionType.Clarification);
                answer.AiAnswerQuality = BehaviourAnswerQuality.Insufficient;
                evaluationStatus = "SKIPPED_LOW_STT";
            }
            else
            {
                var evaluationRequest = BuildEvaluationRequest(
                    session, rubric, mainQuestion, mainSnapshot, chain, sessionQuestion, request,
                    clarificationsUsed, followUpsUsed);

                var provider = _providerResolver.Resolve("external");
                var aiResult = await provider.EvaluateAnswerAsync(evaluationRequest, cancellationToken);
                if (!aiResult.Success || aiResult.Data is null)
                {
                    // Retry 1 lần (brief §21)
                    aiResult = await provider.EvaluateAnswerAsync(evaluationRequest, cancellationToken);
                }

                if (aiResult.Success && aiResult.Data is not null)
                {
                    var validation = _validator.ValidateEvaluation(
                        aiResult.Data, rubric, evaluationRequest.AnswerContext);

                    ApplyEvaluationToAnswer(answer, aiResult.Data, rubric, _scoringService);

                    if (!validation.IsValid)
                    {
                        _logger.LogWarning(
                            "Behavioural evaluation invalid ({ErrorCode}) for session {SessionId}, question {QuestionId}.",
                            validation.ErrorCode, sessionId, sessionQuestion.BehaviourSessionQuestionId);
                    }

                    // Ngưỡng Level 1 chạy trên điểm hiệu lực của câu chính:
                    // main chưa clarify → điểm main; đã clarify → 75% × điểm clarification.
                    var effectiveBaseScore = ComputeEffectiveBaseScore(chain, sessionQuestion, answer);

                    outcome = _decisionEngine.Resolve(
                        effectiveBaseScore,
                        clarificationsUsed,
                        followUpsUsed,
                        !string.IsNullOrWhiteSpace(mainSnapshot.ClarificationQuestion),
                        !string.IsNullOrWhiteSpace(mainSnapshot.FollowUp1),
                        !string.IsNullOrWhiteSpace(mainSnapshot.FollowUp2),
                        rubric.Limits);
                }
                else
                {
                    // AI fail sau retry → không treo phỏng vấn, đi tiếp và đánh dấu pending (brief §21)
                    _logger.LogError(
                        "Behavioural evaluation failed ({ErrorCode}) for session {SessionId}; marking PENDING_EVALUATION.",
                        aiResult.ErrorCode, sessionId);
                    evaluationStatus = "PENDING_EVALUATION";
                    outcome = new BehaviouralDecisionOutcome(BehaviourResolvedAction.NextMainQuestion, true, null);
                }
            }

            sessionQuestion.Status = BehaviourQuestionStatus.Answered;
            sessionQuestion.AnsweredAt = now;

            // Framework Level 1 (quy tắc bổ sung): nếu tổng số câu của buổi (main + follow-up,
            // không tính clarification) vẫn dưới 5 khi chốt câu chính cuối cùng
            // → sinh thêm follow-up cho câu cuối để tăng độ tin cậy.
            if (outcome.FinalizeMainQuestion)
            {
                var forcedOutcome = TryForceReliabilityFollowUp(
                    questionSet, mainSnapshot, followUpsUsed, clarificationsUsed, rubric.Limits);
                if (forcedOutcome is not null)
                {
                    outcome = forcedOutcome;
                }
            }

            answer.ResolvedAction = outcome.Decision;
            await _context.BehaviourAnswers.AddAsync(answer, cancellationToken);

            BehaviouralCurrentQuestionDto? nextQuestionDto = null;
            var sessionStatus = questionSet.Status.ToString();

            if (!outcome.FinalizeMainQuestion && outcome.NextAttemptType is not null)
            {
                var subQuestion = CreateSubQuestion(
                    questionSet, mainQuestion, mainSnapshot, outcome.NextAttemptType.Value, now);
                questionSet.BehaviourSessionQuestion.Add(subQuestion);
                await _context.SaveChangesAsync(cancellationToken);
                nextQuestionDto = BuildQuestionDto(subQuestion, questionSet);
            }
            else
            {
                var nextMain = questionSet.BehaviourSessionQuestion
                    .Where(q => q.QuestionType == BehaviourQuestionType.Main
                        && q.Status == BehaviourQuestionStatus.Pending)
                    .OrderBy(q => q.QuestionOrder)
                    .FirstOrDefault();

                if (nextMain is not null)
                {
                    nextMain.Status = BehaviourQuestionStatus.Asked;
                    nextMain.AskedAt = now;
                    await _context.SaveChangesAsync(cancellationToken);
                    nextQuestionDto = BuildQuestionDto(nextMain, questionSet);
                }
                else
                {
                    // Hết câu — FE gọi POST complete để chốt điểm
                    await _context.SaveChangesAsync(cancellationToken);
                    sessionStatus = "ReadyToComplete";
                }
            }

            return BehaviouralOperationResult<BehaviouralSubmitAnswerResponseDto>.Ok(
                new BehaviouralSubmitAnswerResponseDto
                {
                    SessionQuestionId = sessionQuestion.BehaviourSessionQuestionId,
                    Evaluation = new BehaviouralEvaluationDecisionDto
                    {
                        Decision = outcome.Decision.ToString(),
                        AnswerQuality = answer.AiAnswerQuality?.ToString(),
                        EvaluationStatus = evaluationStatus
                    },
                    NextQuestion = nextQuestionDto,
                    SessionStatus = sessionStatus
                });
        }

        public async Task<BehaviouralOperationResult<BehaviouralInterviewResultDto>> CompleteAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken = default)
        {
            var session = await LoadSessionAsync(userId, sessionId, cancellationToken);
            if (session is null)
            {
                return Failure<BehaviouralInterviewResultDto>(
                    BehaviouralOperationStatus.NotFound, "SESSION_NOT_FOUND", "Interview session not found.");
            }

            var questionSet = await LoadCanonicalQuestionSetAsync(sessionId, cancellationToken);

            if (questionSet is null || questionSet.BehaviourSessionQuestion.Count == 0)
            {
                return Failure<BehaviouralInterviewResultDto>(
                    BehaviouralOperationStatus.Conflict,
                    "NOT_STARTED",
                    "Behavioural interview must be started before it can be completed.");
            }

            var existingResult = await _context.BehaviourRoundResults
                .FirstOrDefaultAsync(r => r.InterviewSessionId == sessionId, cancellationToken);
            if (existingResult is not null)
            {
                var lifecycleError = await EnsureLifecycleCompletionAsync(userId, session);
                if (lifecycleError is not null)
                {
                    return Failure<BehaviouralInterviewResultDto>(
                        BehaviouralOperationStatus.Conflict,
                        "ROUND_LIFECYCLE_TRANSITION_FAILED",
                        lifecycleError);
                }
                return BehaviouralOperationResult<BehaviouralInterviewResultDto>.Ok(
                    BuildResultDto(sessionId, questionSet, existingResult));
            }

            var rubric = _rubricProvider.GetRequired(RubricVersion);
            var mainQuestions = questionSet.BehaviourSessionQuestion
                .Where(q => q.QuestionType == BehaviourQuestionType.Main)
                .OrderBy(q => q.QuestionOrder)
                .ToList();

            // Điểm từng câu chính: main×0.7 + followUpAvg×0.3, cap main+1.5 (brief §14)
            var questionScores = new List<(BehaviourSessionQuestion Question, decimal Score)>();
            var skillScores = new Dictionary<string, List<decimal>>(StringComparer.OrdinalIgnoreCase);

            foreach (var mainQuestion in mainQuestions)
            {
                var score = ComputeMainQuestionScore(mainQuestion, questionSet.BehaviourSessionQuestion);
                questionScores.Add((mainQuestion, score));

                var skill = ParseSnapshot(mainQuestion.QuestionSnapshotJson).Skill;
                if (!string.IsNullOrWhiteSpace(skill))
                {
                    if (!skillScores.TryGetValue(skill, out var list))
                    {
                        skillScores[skill] = list = new List<decimal>();
                    }
                    list.Add(score);
                }
            }

            var overallScore = _scoringService.ScoreSession(questionScores.Select(item => item.Score), rubric);
            var performanceBand = rubric.GetPerformanceBand(overallScore);

            var skillAverages = skillScores.ToDictionary(
                pair => pair.Key,
                pair => Math.Round(pair.Value.Average(), 2, MidpointRounding.AwayFromZero));

            var criteriaAverages = ComputeCriteriaAverages(questionSet.BehaviourSessionQuestion);

            var now = DateTime.UtcNow;
            var roundResult = new BehaviourRoundResult
            {
                InterviewSessionId = sessionId,
                OverallScore = overallScore,
                SkillScoresJson = JsonSerializer.Serialize(skillAverages, SnapshotJsonOptions),
                CriteriaAveragesJson = JsonSerializer.Serialize(criteriaAverages, SnapshotJsonOptions),
                CompletedAt = now,
                CreatedAt = now
            };

            // AI chỉ tổng hợp narrative — điểm đã do backend tính xong (brief §15)
            var summaryRequest = new BehaviouralAIFinalSummaryRequest
            {
                RubricVersion = rubric.Version,
                JobRole = session.InterviewCampaign.JDExtractedProfile?.RoleTarget
                    ?? session.InterviewCampaign.JDExtractedProfile?.JobTitle ?? string.Empty,
                ExperienceLevel = session.InterviewCampaign.JDExtractedProfile?.ExperienceLevel ?? string.Empty,
                Language = session.InterviewCampaign.Language,
                OverallScore = overallScore,
                PerformanceBand = performanceBand.Code,
                SkillScores = skillAverages,
                CriteriaAverages = criteriaAverages,
                MainQuestionResults = questionScores.Select(item => (object)new
                {
                    question = ParseSnapshot(item.Question.QuestionSnapshotJson).QuestionText,
                    skill = ParseSnapshot(item.Question.QuestionSnapshotJson).Skill,
                    score = item.Score,
                    strengths = item.Question.BehaviourAnswerAnswer?.AiStrengths,
                    missingPoints = item.Question.BehaviourAnswerAnswer?.AiMissingPoints
                }).ToList()
            };

            var provider = _providerResolver.Resolve("external");
            var summaryResult = await provider.GenerateFinalSummaryAsync(summaryRequest, cancellationToken);
            if (summaryResult.Success && summaryResult.Data is not null)
            {
                roundResult.AiExecutiveSummary = summaryResult.Data.ExecutiveSummary;
                roundResult.AiStrengths = JsonSerializer.Serialize(summaryResult.Data.CompetencyStrengths, SnapshotJsonOptions);
                roundResult.AiGaps = JsonSerializer.Serialize(summaryResult.Data.CompetencyGaps, SnapshotJsonOptions);
                roundResult.AiLevelAssessment = summaryResult.Data.LevelAssessment;
                roundResult.AiRecommendations = JsonSerializer.Serialize(summaryResult.Data.TopRecommendations, SnapshotJsonOptions);
            }
            else
            {
                _logger.LogWarning(
                    "Behavioural summary failed ({ErrorCode}) for session {SessionId}; storing scores without narrative.",
                    summaryResult.ErrorCode, sessionId);
            }

            questionSet.Status = BehaviourQuestionSetStatus.Completed;
            session.UpdatedAt = now;

            await _context.BehaviourRoundResults.AddAsync(roundResult, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var transitionError = await EnsureLifecycleCompletionAsync(userId, session);
            if (transitionError is not null)
            {
                return Failure<BehaviouralInterviewResultDto>(
                    BehaviouralOperationStatus.Conflict,
                    "ROUND_LIFECYCLE_TRANSITION_FAILED",
                    transitionError);
            }

            return BehaviouralOperationResult<BehaviouralInterviewResultDto>.Ok(
                BuildResultDto(sessionId, questionSet, roundResult));
        }

        public async Task<BehaviouralOperationResult<BehaviouralInterviewResultDto>> GetResultAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken = default)
        {
            var session = await LoadSessionAsync(userId, sessionId, cancellationToken);
            if (session is null)
            {
                return Failure<BehaviouralInterviewResultDto>(
                    BehaviouralOperationStatus.NotFound, "SESSION_NOT_FOUND", "Interview session not found.");
            }

            var roundResult = await _context.BehaviourRoundResults
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.InterviewSessionId == sessionId, cancellationToken);
            if (roundResult is null)
            {
                return Failure<BehaviouralInterviewResultDto>(
                    BehaviouralOperationStatus.NotFound, "RESULT_NOT_READY", "Behavioural round result is not available yet.");
            }

            var lifecycleError = await EnsureLifecycleCompletionAsync(userId, session);
            if (lifecycleError is not null)
            {
                return Failure<BehaviouralInterviewResultDto>(
                    BehaviouralOperationStatus.Conflict,
                    "ROUND_LIFECYCLE_TRANSITION_FAILED",
                    lifecycleError);
            }

            var questionSet = await LoadCanonicalQuestionSetAsync(
                sessionId,
                cancellationToken,
                asNoTracking: true);

            if (questionSet is null)
            {
                return Failure<BehaviouralInterviewResultDto>(
                    BehaviouralOperationStatus.NotFound, "NOT_STARTED", "Behavioural interview has not started.");
            }

            return BehaviouralOperationResult<BehaviouralInterviewResultDto>.Ok(
                BuildResultDto(sessionId, questionSet, roundResult));
        }

        // ----- helpers -----

        private Task<InterviewSession?> LoadSessionAsync(int userId, int sessionId, CancellationToken cancellationToken)
        {
            return _context.InterviewSessions
                .Include(s => s.InterviewCampaign)
                    .ThenInclude(c => c.CVExtractedProfile)
                        .ThenInclude(p => p.Skills)
                .Include(s => s.InterviewCampaign)
                    .ThenInclude(c => c.CVExtractedProfile)
                        .ThenInclude(p => p.Projects)
                .Include(s => s.InterviewCampaign)
                    .ThenInclude(c => c.JDExtractedProfile)
                .FirstOrDefaultAsync(
                    s => s.InterviewSessionId == sessionId
                        && !s.IsDeleted
                        && s.InterviewCampaign.UserId == userId,
                    cancellationToken);
        }

        private Task<BehaviourQuestionSet?> LoadCanonicalQuestionSetAsync(
            int sessionId,
            CancellationToken cancellationToken,
            bool asNoTracking = false)
        {
            IQueryable<BehaviourQuestionSet> query = _context.BehaviourQuestionSets
                .Include(set => set.BehaviourSessionQuestion)
                .ThenInclude(question => question.BehaviourAnswerAnswer);

            if (asNoTracking)
            {
                query = query.AsNoTracking();
            }

            // Older builds could create more than one set when initialize/start requests
            // overlapped. Always select the set containing the most interview progress so
            // start, state and complete cannot observe different rows for the same session.
            return query
                .Where(set => set.InterviewSessionId == sessionId)
                .OrderByDescending(set => set.BehaviourSessionQuestion.Count(question =>
                    question.BehaviourAnswerAnswer != null))
                .ThenByDescending(set => set.BehaviourSessionQuestion.Count)
                .ThenBy(set => set.BehaviourQuestionSetId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        /// <summary>
        /// Fast Check CV-JD Match Score (0–100) — chỉ dùng phân bổ nguồn câu hỏi, không chấm ứng viên.
        /// Tính lazy một lần cho campaign; AI fail → để null (selection coi như band giữa).
        /// </summary>
        private async Task EnsureCvJdMatchScoreAsync(InterviewCampaign campaign, CancellationToken cancellationToken)
        {
            if (campaign.CvJdMatchScore is not null)
            {
                return;
            }

            var cvProfile = campaign.CVExtractedProfile;
            var jdProfile = campaign.JDExtractedProfile;
            if (cvProfile is null || jdProfile is null)
            {
                return;
            }

            var cvJson = JsonSerializer.Serialize(new
            {
                cvProfile.RoleTarget,
                cvProfile.OverallAssessment,
                cvProfile.Strengths,
                cvProfile.Weaknesses,
                cvProfile.Experience,
                cvProfile.Education,
                Skills = cvProfile.Skills.Select(s => s.SkillName).ToList(),
                Projects = cvProfile.Projects.Select(p => p.ProjectSummary).ToList()
            });

            var jdJson = JsonSerializer.Serialize(new
            {
                jdProfile.JobTitle,
                jdProfile.ExperienceLevel,
                jdProfile.RequiredSkills,
                jdProfile.NiceToHaveSkills,
                jdProfile.Responsibilities,
                jdProfile.CompanyCharacteristics
            });

            var (success, result, _, error) = await _aiParsingService.EvaluateCvAgainstJdAsync(cvJson, jdJson);
            if (!success || result is null)
            {
                _logger.LogWarning(
                    "CV-JD match score unavailable for campaign {CampaignId} ({Error}); selection uses balanced split.",
                    campaign.InterviewCampaignId, error);
                return;
            }

            campaign.CvJdMatchScore = Math.Clamp(result.MatchScore, 0, 100);
            campaign.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        private static BehaviourQuestionSet CreateQuestionSet(InterviewSession session, List<string>? requiredSkills)
        {
            return new BehaviourQuestionSet
            {
                InterviewSessionId = session.InterviewSessionId,
                SelectionSource = BehaviourSelectionSource.ExternalAi,
                QuestionCount = session.QuestionCount > 0 ? session.QuestionCount : 3,
                ConstraintsJson = JsonSerializer.Serialize(new
                {
                    RequiredSkills = requiredSkills ?? new List<string>(),
                    Language = session.InterviewCampaign.Language
                }, SnapshotJsonOptions),
                Status = BehaviourQuestionSetStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
        }

        private BehaviouralSelectionContext BuildSelectionContext(InterviewSession session, BehaviourQuestionSet questionSet)
        {
            var campaign = session.InterviewCampaign;
            var jdProfile = campaign.JDExtractedProfile;
            var cvProfile = campaign.CVExtractedProfile;

            var requiredSkills = new List<string>();
            if (!string.IsNullOrWhiteSpace(questionSet.ConstraintsJson))
            {
                try
                {
                    var constraints = JsonSerializer.Deserialize<StoredConstraints>(
                        questionSet.ConstraintsJson, SnapshotJsonOptions);
                    requiredSkills = constraints?.RequiredSkills ?? new List<string>();
                }
                catch (JsonException)
                {
                    _logger.LogWarning("Invalid ConstraintsJson on question set {SetId}.", questionSet.BehaviourQuestionSetId);
                }
            }

            var cvHighlights = new List<string>();
            if (!string.IsNullOrWhiteSpace(cvProfile?.Strengths))
            {
                cvHighlights.Add(cvProfile.Strengths);
            }
            if (!string.IsNullOrWhiteSpace(cvProfile?.OverallAssessment))
            {
                cvHighlights.Add(cvProfile.OverallAssessment);
            }

            return new BehaviouralSelectionContext
            {
                Language = campaign.Language,
                JobRole = jdProfile?.RoleTarget ?? jdProfile?.JobTitle ?? cvProfile?.RoleTarget ?? string.Empty,
                ExperienceLevel = jdProfile?.ExperienceLevel ?? string.Empty,
                Difficulty = session.Difficulty,
                NumberOfQuestions = questionSet.QuestionCount,
                RequiredSkills = requiredSkills,
                NiceToHaveSkills = ParseJsonStringList(jdProfile?.NiceToHaveSkills),
                CvHighlights = cvHighlights,
                CvSkills = cvProfile?.Skills
                
                    .Select(skill => skill.SkillName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList() ?? new List<string>(),
                CvJdMatchScore = campaign.CvJdMatchScore
            };
        }

        private static List<string> ParseJsonStringList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<string>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch (JsonException)
            {
                return new List<string>();
            }
        }

        private static BehaviourSessionQuestion? FindCurrentQuestion(ICollection<BehaviourSessionQuestion> questions)
        {
            return questions
                .Where(q => q.Status == BehaviourQuestionStatus.Asked)
                .OrderByDescending(q => q.AskedAt)
                .FirstOrDefault()
                ?? questions
                    .Where(q => q.QuestionType == BehaviourQuestionType.Main
                        && q.Status == BehaviourQuestionStatus.Pending)
                    .OrderBy(q => q.QuestionOrder)
                    .FirstOrDefault();
        }

        private static BehaviourSessionQuestion ResolveMainQuestion(
            BehaviourSessionQuestion question,
            ICollection<BehaviourSessionQuestion> questions)
        {
            if (question.QuestionType == BehaviourQuestionType.Main)
            {
                return question;
            }

            var parent = questions.FirstOrDefault(q => q.BehaviourSessionQuestionId == question.ParentQuestionId);
            return parent is null
                ? question
                : ResolveMainQuestion(parent, questions);
        }

        /// <summary>Câu chính + toàn bộ câu phụ của nó, theo thứ tự hỏi.</summary>
        private static List<BehaviourSessionQuestion> GetQuestionChain(
            BehaviourSessionQuestion mainQuestion,
            ICollection<BehaviourSessionQuestion> questions)
        {
            return questions
                .Where(q => q.BehaviourSessionQuestionId == mainQuestion.BehaviourSessionQuestionId
                    || q.ParentQuestionId == mainQuestion.BehaviourSessionQuestionId)
                .OrderBy(q => q.AskedAt ?? DateTime.MaxValue)
                .ToList();
        }

        private BehaviouralAIEvaluationRequest BuildEvaluationRequest(
            InterviewSession session,
            BehaviouralRubricDefinition rubric,
            BehaviourSessionQuestion mainQuestion,
            BehaviourQuestionSnapshot mainSnapshot,
            List<BehaviourSessionQuestion> chain,
            BehaviourSessionQuestion currentQuestion,
            SubmitBehaviouralAnswerRequest request,
            int clarificationsUsed,
            int followUpsUsed)
        {
            var answerContext = new List<BehaviouralAnswerContext>();
            foreach (var chainQuestion in chain)
            {
                var text = chainQuestion.QuestionType == BehaviourQuestionType.Main
                    ? mainSnapshot.QuestionText
                    : GetSubQuestionText(mainSnapshot, chainQuestion.QuestionType) ?? string.Empty;

                var chainAnswer = chainQuestion.BehaviourSessionQuestionId == currentQuestion.BehaviourSessionQuestionId
                    ? request.Transcript
                    : chainQuestion.BehaviourAnswerAnswer?.Transcript;

                if (chainAnswer is not null)
                {
                    answerContext.Add(new BehaviouralAnswerContext(
                        chainQuestion.QuestionType.ToString(), text, chainAnswer));
                }
            }

            return new BehaviouralAIEvaluationRequest
            {
                RubricVersion = rubric.Version,
                Rubric = new
                {
                    dimensions = rubric.Dimensions.Select(d => new { d.Code, d.Name, d.Description, d.Weight }),
                    levels = rubric.Levels.Select(l => new { l.Code, l.Score, l.Description })
                },
                JobRole = session.InterviewCampaign.JDExtractedProfile?.RoleTarget
                    ?? session.InterviewCampaign.JDExtractedProfile?.JobTitle ?? string.Empty,
                ExperienceLevel = session.InterviewCampaign.JDExtractedProfile?.ExperienceLevel ?? string.Empty,
                Language = session.InterviewCampaign.Language,
                Skill = mainSnapshot.Skill ?? string.Empty,
                MainQuestion = mainSnapshot.QuestionText,
                ExpectedKeyPoints = mainSnapshot.ExpectedKeyPoints ?? new List<string>(),
                QuestionScoringRubric = mainSnapshot.ScoringRubric ?? new Dictionary<string, string>(),
                AnswerContext = answerContext,
                AnswerDurationSeconds = request.AnswerDurationSeconds,
                ClarificationsUsed = clarificationsUsed,
                FollowUpsUsed = followUpsUsed
            };
        }

        private static void ApplyEvaluationToAnswer(
            BehaviourAnswer answer,
            BehaviouralAIEvaluationResponse evaluation,
            BehaviouralRubricDefinition rubric,
            IBehaviouralRubricScoringService scoringService)
        {
            var byCode = evaluation.DimensionEvaluations.ToDictionary(
                item => item.RubricCode, StringComparer.OrdinalIgnoreCase);

            answer.AiSituationScore = GetScore(byCode, "SITUATION_TASK");
            answer.AiActionScore = GetScore(byCode, "ACTION");
            answer.AiResultScore = GetScore(byCode, "RESULT");
            answer.AiCompetencyScore = GetScore(byCode, "COMPETENCY");
            answer.AiCommunicationScore = GetScore(byCode, "COMMUNICATION");
            answer.AiCriteriaDetailJson = JsonSerializer.Serialize(
                evaluation.DimensionEvaluations, SnapshotJsonOptions);
            answer.AiOverallRubricScore = evaluation.OverallRubricScore;
            answer.AiStrengths = JsonSerializer.Serialize(evaluation.Strengths, SnapshotJsonOptions);
            answer.AiMissingPoints = JsonSerializer.Serialize(evaluation.MissingPoints, SnapshotJsonOptions);

            if (Enum.TryParse<BehaviourAnswerQuality>(
                evaluation.AnswerQuality?.Trim().Replace("_", string.Empty), true, out var quality))
            {
                answer.AiAnswerQuality = quality;
            }

            var normalizedDecision = evaluation.Decision?.Trim().Replace("_", string.Empty) ?? string.Empty;
            if (Enum.TryParse<BehaviourResolvedAction>(normalizedDecision, true, out var recommended))
            {
                answer.AiRecommendedAction = recommended;
            }

            // Điểm chính thức do backend tính theo công thức 5 tiêu chí cố định (brief §14)
            answer.ComputedScore = scoringService.ScoreQuestion(evaluation, rubric).FinalOverallScore;
        }

        private static decimal? GetScore(
            IReadOnlyDictionary<string, BehaviouralAIDimensionEvaluation> byCode,
            string code)
        {
            return byCode.TryGetValue(code, out var dimension) ? dimension.SuggestedScore : null;
        }

        private static BehaviourSessionQuestion CreateSubQuestion(
            BehaviourQuestionSet questionSet,
            BehaviourSessionQuestion mainQuestion,
            BehaviourQuestionSnapshot mainSnapshot,
            BehaviourQuestionType type,
            DateTime now)
        {
            var text = GetSubQuestionText(mainSnapshot, type)
                ?? throw new InvalidOperationException($"Snapshot has no pre-written question for {type}.");

            var snapshot = new BehaviourQuestionSnapshot
            {
                QuestionText = text,
                Skill = mainSnapshot.Skill,
                Difficulty = mainSnapshot.Difficulty,
                TimeLimitSeconds = mainSnapshot.TimeLimitSeconds
            };

            return new BehaviourSessionQuestion
            {
                BehaviourQuestionSet = questionSet,
                QuestionId = mainQuestion.QuestionId,
                QuestionOrder = mainQuestion.QuestionOrder,
                QuestionType = type,
                ParentQuestionId = mainQuestion.BehaviourSessionQuestionId,
                QuestionSnapshotJson = JsonSerializer.Serialize(snapshot, SnapshotJsonOptions),
                Status = BehaviourQuestionStatus.Asked,
                AskedAt = now
            };
        }

        private static string? GetSubQuestionText(BehaviourQuestionSnapshot snapshot, BehaviourQuestionType type)
        {
            return type switch
            {
                BehaviourQuestionType.Clarification => snapshot.ClarificationQuestion,
                BehaviourQuestionType.FollowUp1 => snapshot.FollowUp1,
                BehaviourQuestionType.FollowUp2 => snapshot.FollowUp2,
                _ => null
            };
        }

        /// <summary>
        /// Điểm câu chính theo Evaluation Framework Level 1 — công thức gộp nằm ở
        /// <see cref="IBehaviouralRubricScoringService.CombineMainQuestionScore"/>.
        /// </summary>
        private decimal ComputeMainQuestionScore(
            BehaviourSessionQuestion mainQuestion,
            ICollection<BehaviourSessionQuestion> questions)
        {
            var chain = GetQuestionChain(mainQuestion, questions);

            var mainScore = chain
                .Where(q => q.QuestionType == BehaviourQuestionType.Main)
                .Select(q => q.BehaviourAnswerAnswer?.ComputedScore)
                .FirstOrDefault(score => score is not null);

            var clarificationScore = chain
                .Where(q => q.QuestionType == BehaviourQuestionType.Clarification)
                .Select(q => q.BehaviourAnswerAnswer?.ComputedScore)
                .FirstOrDefault(score => score is not null);

            var followUpScores = chain
                .Where(q => q.QuestionType is BehaviourQuestionType.FollowUp1 or BehaviourQuestionType.FollowUp2)
                .Select(q => q.BehaviourAnswerAnswer?.ComputedScore)
                .Where(score => score is not null)
                .Select(score => score!.Value)
                .ToList();

            return _scoringService.CombineMainQuestionScore(mainScore, clarificationScore, followUpScores);
        }

        /// <summary>
        /// Điểm gốc dùng cho ngưỡng Level 1: đã có clarification → 75% × điểm clarification,
        /// chưa có → điểm câu main. Câu đang nộp chưa lưu DB nên lấy từ <paramref name="currentAnswer"/>.
        /// </summary>
        private static decimal? ComputeEffectiveBaseScore(
            List<BehaviourSessionQuestion> chain,
            BehaviourSessionQuestion currentQuestion,
            BehaviourAnswer currentAnswer)
        {
            decimal? ScoreOf(BehaviourSessionQuestion question) =>
                question.BehaviourSessionQuestionId == currentQuestion.BehaviourSessionQuestionId
                    ? currentAnswer.ComputedScore
                    : question.BehaviourAnswerAnswer?.ComputedScore;

            var clarificationScore = chain
                .Where(q => q.QuestionType == BehaviourQuestionType.Clarification)
                .Select(ScoreOf)
                .FirstOrDefault(score => score is not null);
            if (clarificationScore is not null)
            {
                return ClarificationWeight * clarificationScore.Value;
            }

            return chain
                .Where(q => q.QuestionType == BehaviourQuestionType.Main)
                .Select(ScoreOf)
                .FirstOrDefault(score => score is not null);
        }

        /// <summary>
        /// Buổi phỏng vấn phải có tối thiểu 5 câu (main + follow-up, không tính clarification).
        /// Khi chốt câu chính cuối cùng mà chưa đủ → ép sinh thêm follow-up từ snapshot.
        /// </summary>
        private static BehaviouralDecisionOutcome? TryForceReliabilityFollowUp(
            BehaviourQuestionSet questionSet,
            BehaviourQuestionSnapshot mainSnapshot,
            int followUpsUsed,
            int clarificationsUsed,
            BehaviouralQuestionLimits limits)
        {
            var hasPendingMain = questionSet.BehaviourSessionQuestion.Any(q =>
                q.QuestionType == BehaviourQuestionType.Main
                && q.Status == BehaviourQuestionStatus.Pending);
            if (hasPendingMain)
            {
                return null;
            }

            var countedQuestions = questionSet.BehaviourSessionQuestion
                .Count(q => q.QuestionType != BehaviourQuestionType.Clarification);
            if (countedQuestions >= MinimumCountedQuestionsPerRound)
            {
                return null;
            }

            if (clarificationsUsed + followUpsUsed >= limits.MaxTotalSubQuestionsPerMainQuestion
                || followUpsUsed >= limits.MaxFollowUpsPerMainQuestion)
            {
                return null;
            }

            if (followUpsUsed == 0 && !string.IsNullOrWhiteSpace(mainSnapshot.FollowUp1))
            {
                return new BehaviouralDecisionOutcome(
                    BehaviourResolvedAction.FollowUp1, false, BehaviourQuestionType.FollowUp1);
            }

            if (followUpsUsed == 1 && !string.IsNullOrWhiteSpace(mainSnapshot.FollowUp2))
            {
                return new BehaviouralDecisionOutcome(
                    BehaviourResolvedAction.FollowUp2, false, BehaviourQuestionType.FollowUp2);
            }

            return null;
        }

        private static Dictionary<string, decimal> ComputeCriteriaAverages(
            ICollection<BehaviourSessionQuestion> questions)
        {
            var answers = questions
                .Select(q => q.BehaviourAnswerAnswer)
                .Where(a => a is not null && a.AiSituationScore is not null)
                .Select(a => a!)
                .ToList();

            if (answers.Count == 0)
            {
                return new Dictionary<string, decimal>();
            }

            decimal Avg(Func<BehaviourAnswer, decimal?> selector) => Math.Round(
                answers.Select(selector).Where(v => v is not null).Select(v => v!.Value).DefaultIfEmpty(0m).Average(),
                2, MidpointRounding.AwayFromZero);

            return new Dictionary<string, decimal>
            {
                ["situation"] = Avg(a => a.AiSituationScore),
                ["action"] = Avg(a => a.AiActionScore),
                ["result"] = Avg(a => a.AiResultScore),
                ["competency"] = Avg(a => a.AiCompetencyScore),
                ["communication"] = Avg(a => a.AiCommunicationScore)
            };
        }

        private BehaviouralCurrentQuestionDto BuildQuestionDto(
            BehaviourSessionQuestion question,
            BehaviourQuestionSet questionSet)
        {
            var snapshot = ParseSnapshot(question.QuestionSnapshotJson);
            var totalMain = questionSet.BehaviourSessionQuestion
                .Count(q => q.QuestionType == BehaviourQuestionType.Main);

            // FE chỉ nhận nội dung câu hỏi — không rubric, không key points, không follow-up (brief §10)
            return new BehaviouralCurrentQuestionDto
            {
                SessionQuestionId = question.BehaviourSessionQuestionId,
                QuestionId = question.QuestionId,
                QuestionType = question.QuestionType.ToString(),
                ParentSessionQuestionId = question.ParentQuestionId,
                Content = snapshot.QuestionText,
                Skill = snapshot.Skill,
                Difficulty = snapshot.Difficulty,
                TimeLimitSeconds = snapshot.TimeLimitSeconds ?? 120,
                Hint = question.QuestionType == BehaviourQuestionType.Main ? MainQuestionHint : null,
                MainQuestionIndex = question.QuestionOrder,
                TotalMainQuestions = totalMain > 0 ? totalMain : questionSet.QuestionCount,
                SessionStatus = questionSet.Status.ToString()
            };
        }

        private BehaviouralInterviewSessionDto BuildSessionDto(
            InterviewSession session,
            BehaviourQuestionSet questionSet,
            IReadOnlyCollection<BehaviourSessionQuestion> questions,
            BehaviourRoundResult? roundResult)
        {
            var rubric = _rubricProvider.GetRequired(RubricVersion);
            var jdProfile = session.InterviewCampaign.JDExtractedProfile;
            var mainQuestions = questions
                .Where(q => q.QuestionType == BehaviourQuestionType.Main)
                .ToList();

            return new BehaviouralInterviewSessionDto
            {
                SessionId = session.InterviewSessionId,
                JobRole = jdProfile?.RoleTarget ?? jdProfile?.JobTitle ?? string.Empty,
                ExperienceLevel = jdProfile?.ExperienceLevel ?? string.Empty,
                Language = session.InterviewCampaign.Language,
                RequiredSkills = ParseConstraintSkills(questionSet.ConstraintsJson),
                TargetMainQuestionCount = questionSet.QuestionCount,
                CompletedMainQuestionCount = mainQuestions.Count(q => q.Status == BehaviourQuestionStatus.Answered),
                Status = questionSet.Status.ToString(),
                AiProvider = "external",
                RubricVersion = rubric.Version,
                ScoringPolicyVersion = rubric.ScoringPolicyVersion,
                StartedAt = mainQuestions
                    .Where(q => q.AskedAt is not null)
                    .Select(q => q.AskedAt)
                    .OrderBy(askedAt => askedAt)
                    .FirstOrDefault(),
                CompletedAt = roundResult?.CompletedAt,
                FinalScore = roundResult?.OverallScore,
                PerformanceBand = roundResult?.OverallScore is not null
                    ? rubric.GetPerformanceBand(roundResult.OverallScore.Value).Code
                    : null,
                Transcript = BuildTranscript(questions)
            };
        }

        private static List<BehaviouralTranscriptEntryDto> BuildTranscript(
            IEnumerable<BehaviourSessionQuestion> questions)
        {
            return questions
                .Where(question => question.AskedAt.HasValue)
                .OrderBy(question => question.AskedAt)
                .ThenBy(question => question.BehaviourSessionQuestionId)
                .SelectMany(question =>
                {
                    var snapshot = ParseSnapshot(question.QuestionSnapshotJson);
                    var entries = new List<BehaviouralTranscriptEntryDto>
                    {
                        new()
                        {
                            Id = $"question-{question.BehaviourSessionQuestionId}",
                            SessionQuestionId = question.BehaviourSessionQuestionId,
                            Role = "INTERVIEWER",
                            Content = snapshot.QuestionText,
                            QuestionType = question.QuestionType.ToString(),
                            Status = question.BehaviourAnswerAnswer is null ? "CURRENT" : "FINAL",
                            CreatedAt = question.AskedAt!.Value
                        }
                    };

                    if (question.BehaviourAnswerAnswer is not null)
                    {
                        entries.Add(new BehaviouralTranscriptEntryDto
                        {
                            Id = $"answer-{question.BehaviourSessionQuestionId}",
                            SessionQuestionId = question.BehaviourSessionQuestionId,
                            Role = "CANDIDATE",
                            Content = question.BehaviourAnswerAnswer.Transcript,
                            QuestionType = question.QuestionType.ToString(),
                            Status = "FINAL",
                            CreatedAt = question.BehaviourAnswerAnswer.CreatedAt
                        });
                    }

                    return entries;
                })
                .ToList();
        }

        private async Task<string?> EnsureLifecycleCompletionAsync(int userId, InterviewSession session)
        {
            if (session.Status == InterviewSessionStatus.Completed
                && session.InterviewCampaign.Status is InterviewCampaignStatus.Completed
                    or InterviewCampaignStatus.Cancelled
                    or InterviewCampaignStatus.Expired)
            {
                return null;
            }

            if (session.Status != InterviewSessionStatus.Active
                && session.Status != InterviewSessionStatus.Completed)
            {
                return $"Interview session is in '{session.Status}' state and cannot transition to the next round.";
            }

            var lifecycleResult = await _sessionLifecycleService.CompleteSessionAsync(
                userId,
                session.InterviewSessionId);
            if (lifecycleResult.Success)
            {
                return null;
            }

            _logger.LogWarning(
                "Behavioural session {SessionId} was scored but lifecycle completion failed: {Error}",
                session.InterviewSessionId,
                lifecycleResult.ErrorMessage);
            return lifecycleResult.ErrorMessage ?? "Could not activate the next interview round.";
        }

        private List<string> ParseConstraintSkills(string? constraintsJson)
        {
            if (string.IsNullOrWhiteSpace(constraintsJson))
            {
                return new List<string>();
            }

            try
            {
                var constraints = JsonSerializer.Deserialize<StoredConstraints>(constraintsJson, SnapshotJsonOptions);
                return constraints?.RequiredSkills ?? new List<string>();
            }
            catch (JsonException)
            {
                return new List<string>();
            }
        }

        private BehaviouralInterviewResultDto BuildResultDto(
            int sessionId,
            BehaviourQuestionSet questionSet,
            BehaviourRoundResult roundResult)
        {
            var rubric = _rubricProvider.GetRequired(RubricVersion);
            var overall = roundResult.OverallScore ?? 0m;

            var mainQuestions = questionSet.BehaviourSessionQuestion
                .Where(q => q.QuestionType == BehaviourQuestionType.Main)
                .OrderBy(q => q.QuestionOrder)
                .Select(mainQuestion =>
                {
                    var snapshot = ParseSnapshot(mainQuestion.QuestionSnapshotJson);
                    var answer = mainQuestion.BehaviourAnswerAnswer;
                    return new BehaviouralMainQuestionResultDto
                    {
                        SessionQuestionId = mainQuestion.BehaviourSessionQuestionId,
                        QuestionId = mainQuestion.QuestionId,
                        MainQuestionIndex = mainQuestion.QuestionOrder,
                        Question = snapshot.QuestionText,
                        Skill = snapshot.Skill ?? string.Empty,
                        Score = ComputeMainQuestionScore(mainQuestion, questionSet.BehaviourSessionQuestion),
                        Dimensions = BuildDimensionResults(answer, rubric),
                        Strengths = ParseJsonStringList(answer?.AiStrengths),
                        MissingPoints = ParseJsonStringList(answer?.AiMissingPoints)
                    };
                })
                .ToList();

            return new BehaviouralInterviewResultDto
            {
                SessionId = sessionId,
                RubricVersion = rubric.Version,
                ScoringPolicyVersion = rubric.ScoringPolicyVersion,
                OverallScore = overall,
                PerformanceBand = rubric.GetPerformanceBand(overall).Code,
                MainQuestions = mainQuestions,
                Summary = new BehaviouralFinalSummaryDto
                {
                    ExecutiveSummary = roundResult.AiExecutiveSummary ?? string.Empty,
                    CompetencyStrengths = ParseJsonStringList(roundResult.AiStrengths),
                    CompetencyGaps = ParseJsonStringList(roundResult.AiGaps),
                    LevelAssessment = roundResult.AiLevelAssessment ?? string.Empty,
                    TopRecommendations = ParseJsonStringList(roundResult.AiRecommendations)
                }
            };
        }

        private static List<BehaviouralDimensionResultDto> BuildDimensionResults(
            BehaviourAnswer? answer,
            BehaviouralRubricDefinition rubric)
        {
            if (answer?.AiCriteriaDetailJson is null)
            {
                return new List<BehaviouralDimensionResultDto>();
            }

            List<BehaviouralAIDimensionEvaluation>? dimensions;
            try
            {
                dimensions = JsonSerializer.Deserialize<List<BehaviouralAIDimensionEvaluation>>(
                    answer.AiCriteriaDetailJson, SnapshotJsonOptions);
            }
            catch (JsonException)
            {
                return new List<BehaviouralDimensionResultDto>();
            }

            if (dimensions is null)
            {
                return new List<BehaviouralDimensionResultDto>();
            }

            var byCode = dimensions.ToDictionary(d => d.RubricCode, StringComparer.OrdinalIgnoreCase);
            return rubric.Dimensions.Select(dimension =>
            {
                var evaluated = byCode.TryGetValue(dimension.Code, out var value) ? value : null;
                var score = evaluated?.SuggestedScore ?? 0m;
                return new BehaviouralDimensionResultDto
                {
                    RubricCode = dimension.Code,
                    Name = dimension.Name,
                    Score = score,
                    Weight = dimension.Weight,
                    WeightedScore = Math.Round(score * dimension.Weight, 4, MidpointRounding.AwayFromZero),
                    Level = rubric.GetLevelCode(score),
                    Evidence = evaluated?.Evidence ?? new List<string>(),
                    MissingEvidence = evaluated?.MissingEvidence ?? new List<string>(),
                    ReasonSummary = evaluated?.ReasonSummary ?? string.Empty
                };
            }).ToList();
        }

        private static BehaviourQuestionSnapshot ParseSnapshot(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<BehaviourQuestionSnapshot>(json, SnapshotJsonOptions)
                    ?? new BehaviourQuestionSnapshot();
            }
            catch (JsonException)
            {
                return new BehaviourQuestionSnapshot();
            }
        }

        private static BehaviouralOperationResult<T> Failure<T>(
            BehaviouralOperationStatus status, string errorCode, string message)
        {
            return BehaviouralOperationResult<T>.Failure(status, errorCode, message);
        }

        private sealed class StoredConstraints
        {
            public List<string>? RequiredSkills { get; set; }
            public string? Language { get; set; }
        }

        /// <summary>
        /// Full snapshot theo brief §9 — sau khi lock, thay đổi ở question bank không ảnh hưởng session.
        /// </summary>
        private sealed class BehaviourQuestionSnapshot
        {
            public string QuestionText { get; set; } = string.Empty;
            public string? Skill { get; set; }
            public string? Difficulty { get; set; }
            public string? ExperienceLevel { get; set; }
            public int? TimeLimitSeconds { get; set; }
            public List<string>? ExpectedKeyPoints { get; set; }
            public Dictionary<string, string>? ScoringRubric { get; set; }
            public string? ClarificationQuestion { get; set; }
            public string? FollowUp1 { get; set; }
            public string? FollowUp2 { get; set; }

            public static BehaviourQuestionSnapshot FromQuestion(Question question)
            {
                Dictionary<string, string>? scoringRubric = null;
                if (!string.IsNullOrWhiteSpace(question.ScoringRubric))
                {
                    try
                    {
                        scoringRubric = JsonSerializer.Deserialize<Dictionary<string, string>>(question.ScoringRubric);
                    }
                    catch (JsonException)
                    {
                        // rubric hỏng thì bỏ qua, không chặn snapshot
                    }
                }

                return new BehaviourQuestionSnapshot
                {
                    QuestionText = question.QuestionContent,
                    Skill = question.Skill,
                    Difficulty = question.Difficulty.ToString(),
                    ExperienceLevel = question.ExperienceLevel,
                    TimeLimitSeconds = question.TimeLimitSeconds ?? 120,
                    ExpectedKeyPoints = string.IsNullOrWhiteSpace(question.ExpectedKeyPoints)
                        ? null
                        : question.ExpectedKeyPoints.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(),
                    ScoringRubric = scoringRubric,
                    ClarificationQuestion = question.ClarificationQuestion,
                    FollowUp1 = question.FollowUp1,
                    FollowUp2 = question.FollowUp2
                };
            }
        }
    }
}
