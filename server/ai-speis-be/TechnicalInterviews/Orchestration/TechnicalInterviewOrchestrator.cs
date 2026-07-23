using System.Text.Json;
using System.Collections.Immutable;
using System.Collections.Concurrent;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.QuestionRepo;
using ai_speis_be.Services.InterviewSessionService;
using ai_speis_be.Services.JDService;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.TechnicalInterviews.DTOs;
using ai_speis_be.TechnicalInterviews.Planning;
using ai_speis_be.TechnicalInterviews.Rubrics;
using ai_speis_be.TechnicalInterviews.Scoring;
using ai_speis_be.TechnicalInterviews.Selection;
using ai_speis_be.TechnicalInterviews.Validation;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.TechnicalInterviews.Orchestration
{
    public sealed class TechnicalInterviewOrchestrator : ITechnicalInterviewOrchestrator
    {
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> FeedbackGates = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly ApplicationDbContext _context;
        private readonly IQuestionRepoitory _questionRepository;
        private readonly ITechnicalQuestionSelectionService _selectionService;
        private readonly ITechnicalInterviewAIProviderResolver _providerResolver;
        private readonly ITechnicalRubricProvider _rubricProvider;
        private readonly ITechnicalRubricScoringService _scoringService;
        private readonly ITechnicalAnswerEvaluationProcessor _evaluationProcessor;
        private readonly ITechnicalInterviewDecisionArbiter _decisionArbiter;
        private readonly ITechnicalQuestionPlanBuilder _questionPlanBuilder;
        private readonly ITechnicalQuestionOrderRandomizer _questionOrderRandomizer;
        private readonly IJDService _jdService;
        private readonly IInterviewSessionService _sessionLifecycleService;
        private readonly TechnicalInterviewOptions _options;
        private readonly ILogger<TechnicalInterviewOrchestrator> _logger;

        public TechnicalInterviewOrchestrator(
            ApplicationDbContext context,
            IQuestionRepoitory questionRepository,
            ITechnicalQuestionSelectionService selectionService,
            ITechnicalInterviewAIProviderResolver providerResolver,
            ITechnicalRubricProvider rubricProvider,
            ITechnicalRubricScoringService scoringService,
            ITechnicalAnswerEvaluationProcessor evaluationProcessor,
            ITechnicalInterviewDecisionArbiter decisionArbiter,
            ITechnicalQuestionPlanBuilder questionPlanBuilder,
            ITechnicalQuestionOrderRandomizer questionOrderRandomizer,
            IJDService jdService,
            IInterviewSessionService sessionLifecycleService,
            TechnicalInterviewOptions options,
            ILogger<TechnicalInterviewOrchestrator> logger)
        {
            _context = context;
            _questionRepository = questionRepository;
            _selectionService = selectionService;
            _providerResolver = providerResolver;
            _rubricProvider = rubricProvider;
            _scoringService = scoringService;
            _evaluationProcessor = evaluationProcessor;
            _decisionArbiter = decisionArbiter;
            _questionPlanBuilder = questionPlanBuilder;
            _questionOrderRandomizer = questionOrderRandomizer;
            _jdService = jdService;
            _sessionLifecycleService = sessionLifecycleService;
            _options = options;
            _logger = logger;
        }

        public async Task<TechnicalOperationResult<TechnicalInterviewSessionDto>> InitializeAsync(
            int userId,
            InitializeTechnicalInterviewRequest request,
            CancellationToken cancellationToken)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var session = await GetOwnedSessionAsync(userId, request.InterviewSessionId, cancellationToken);
            if (session is null)
                return NotFound<TechnicalInterviewSessionDto>();
            if (session.InterviewRoundType != InterviewRoundType.Technical)
                return BadRequest<TechnicalInterviewSessionDto>("NOT_TECHNICAL_SESSION", "The session is not a Technical round.");
            if (IsLifecycleClosed(session) && session.TechnicalState != TechnicalInterviewState.Completed)
                return Conflict<TechnicalInterviewSessionDto>("SESSION_ALREADY_ENDED", "The interview session is no longer active.");
            if (session.TechnicalState.HasValue)
            {
                if (session.TechnicalState == TechnicalInterviewState.Completed)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return TechnicalOperationResult<TechnicalInterviewSessionDto>.Ok(MapSession(session));
                }
                var upgraded = await EnsureLockedPlanAsync(session, cancellationToken);
                if (!upgraded.IsSuccess)
                {
                    if (string.Equals(upgraded.ErrorCode, "LEGACY_PLAN_UPGRADE_CONFLICT", StringComparison.Ordinal))
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Conflict<TechnicalInterviewSessionDto>(upgraded.ErrorCode, upgraded.Message!);
                    }
                    await RecordLegacyUpgradeFailureAsync(session, upgraded, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return Conflict<TechnicalInterviewSessionDto>(
                        upgraded.ErrorCode ?? "LEGACY_PLAN_UPGRADE_FAILED",
                        upgraded.Message ?? "The legacy Technical session could not be upgraded safely.");
                }
                await transaction.CommitAsync(cancellationToken);
                return TechnicalOperationResult<TechnicalInterviewSessionDto>.Ok(MapSession(session));
            }

            if (session.InterviewCampaign.Status is InterviewCampaignStatus.Completed
                or InterviewCampaignStatus.Cancelled
                or InterviewCampaignStatus.Expired
                || (session.InterviewCampaign.ExpiresAt.HasValue
                    && session.InterviewCampaign.ExpiresAt.Value <= DateTime.UtcNow))
            {
                return Conflict<TechnicalInterviewSessionDto>("CAMPAIGN_NOT_ACTIVE", "The interview campaign is no longer available.");
            }
            if (session.InterviewCampaign.InterviewSessions.Any(item =>
                item.InterviewSessionId != session.InterviewSessionId
                && item.Status == InterviewSessionStatus.Active))
            {
                return Conflict<TechnicalInterviewSessionDto>("ANOTHER_ROUND_ACTIVE", "Another interview round is already active.");
            }
            if (!session.InterviewCampaign.CVExtractedProfile.IsConfirmed
                || !IsJdReadyForInterview(session.InterviewCampaign.JDExtractedProfile))
            {
                return Conflict<TechnicalInterviewSessionDto>(
                    "CV_JD_NOT_READY",
                    "The CV must be confirmed and the JD must be parsed or confirmed before Technical initialization.");
            }

            var jd = session.InterviewCampaign.JDExtractedProfile;
            var roleTargets = TechnicalQuestionMetadata.ResolveRoleAliases(jd.RoleTarget, jd.JobTitle);
            if (roleTargets.Count == 0)
                return BadRequest<TechnicalInterviewSessionDto>("UNSUPPORTED_JOB_ROLE", "The JD role cannot be mapped to the Technical Question Bank.");

            var language = session.InterviewCampaign.Language.Trim().ToLowerInvariant();
            var availableSkills = session.InterviewCampaign.Mode == InterviewMode.Practice
                ? (await _questionRepository.GetTechnicalCandidatesAsync(
                        new TechnicalQuestionCandidateQuery
                        {
                            Language = language,
                            RoleTargets = roleTargets,
                            // Experience labels are advisory. Practice configuration
                            // requires an exact difficulty, so build the plan only from
                            // skills that really have a candidate at that difficulty.
                            ExperienceLevels = Array.Empty<string>(),
                            Difficulty = session.Difficulty,
                            MaximumResults = 500
                        },
                        cancellationToken))
                    .Where(question => !string.IsNullOrWhiteSpace(question.Skill))
                    .Select(question => question.Skill!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(skill => skill, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : await _questionRepository.GetTechnicalSkillsAsync(
                    language,
                    roleTargets,
                    cancellationToken);
            if (availableSkills.Count == 0)
            {
                return BadRequest<TechnicalInterviewSessionDto>(
                    "NO_TECHNICAL_CANDIDATE",
                    session.InterviewCampaign.Mode == InterviewMode.Practice
                        ? $"No active Technical question matches language '{language}', role '{roleTargets[0]}' and difficulty '{session.Difficulty}'."
                        : "No active Technical question matches the session language and role.");
            }

            var selectedSkillsResult = ResolveSelectedSkills(session, request.SelectedSkills, availableSkills);
            if (selectedSkillsResult.Count == 0)
                return BadRequest<TechnicalInterviewSessionDto>("INVALID_SELECTED_SKILLS", "Selected skills do not match the Technical Question Bank.");

            var useAdaptiveFramework = session.InterviewCampaign.Mode != InterviewMode.Practice;
            var rubric = _rubricProvider.GetRequired(
                useAdaptiveFramework ? _options.RubricVersion : _options.PracticeRubricVersion);

            TechnicalQuestionPlan plan;
            if (useAdaptiveFramework)
            {
                var matchScore = session.InterviewCampaign.CvJdMatchScore;
                if (!matchScore.HasValue)
                {
                    var matchResult = await _jdService.MatchCvToJdAsync(
                        userId,
                        jd.JDFileId,
                        session.InterviewCampaign.CVExtractedProfile.CVFileId);
                    if (matchResult is null || !matchResult.Success)
                    {
                        return ExternalFailure<TechnicalInterviewSessionDto>(
                            "CV_JD_MATCH_UNAVAILABLE",
                            matchResult?.ErrorMessage ?? "CV-JD Match Score could not be calculated for the Technical Question Plan.");
                    }
                    matchScore = matchResult.MatchScore;
                    session.InterviewCampaign.CvJdMatchScore = matchScore;
                }

                var planResult = _questionPlanBuilder.Build(new TechnicalQuestionPlanRequest(
                    matchScore.Value,
                    session.InterviewCampaign.CVExtractedProfile.Skills.Select(item => item.SkillName).ToList(),
                    TechnicalQuestionMetadata.ParseStringArray(jd.RequiredSkills),
                    TechnicalQuestionMetadata.ParseStringArray(jd.NiceToHaveSkills),
                    selectedSkillsResult,
                    _options.QuestionPlanVersion));
                if (!planResult.IsSuccess)
                {
                    return BadRequest<TechnicalInterviewSessionDto>(
                        planResult.ErrorCode ?? "QUESTION_PLAN_FAILED",
                        planResult.Message ?? "Technical Question Plan could not be created.");
                }

                plan = planResult.Plan!;
                session.QuestionCount = _options.StandardMainQuestionCount;
                session.TechnicalMatchScoreSnapshot = plan.MatchScore;
                session.TechnicalMatchBand = plan.MatchBand;
                session.TechnicalPlannedCvQuestionCount = plan.PlannedCvQuestionCount;
                session.TechnicalPlannedJdQuestionCount = plan.PlannedJdQuestionCount;
                session.TechnicalQuestionPlanVersion = plan.Version;
                session.TechnicalAdaptiveRuleVersion = _options.AdaptiveRuleVersion;
                session.TechnicalBonusCalculationVersion = _options.BonusCalculationVersion;
            }
            else
            {
                plan = BuildPracticePlan(session, selectedSkillsResult);
                session.TechnicalQuestionPlanVersion = plan.Version;
                session.TechnicalAdaptiveRuleVersion = _options.AdaptiveRuleVersion;
                session.TechnicalBonusCalculationVersion = _options.BonusCalculationVersion;
            }
            session.TechnicalState = TechnicalInterviewState.Created;
            session.TechnicalAiProvider = _options.Provider;
            session.TechnicalAiModel = _options.Model;
            session.TechnicalRubricVersion = rubric.Version;
            session.TechnicalScoringPolicyVersion = rubric.ScoringPolicyVersion;
            session.TechnicalJobRole = roleTargets[0];
            session.TechnicalExperienceLevel = jd.ExperienceLevel ?? string.Empty;
            session.TechnicalLanguage = language;
            session.TechnicalSelectedSkillsJson = JsonSerializer.Serialize(selectedSkillsResult, JsonOptions);
            session.TechnicalCompletedMainQuestionCount = 0;
            plan = plan with
            {
                SelectionContextKey = BuildSelectionContextKey(session, plan, selectedSkillsResult)
            };

            var lockResult = await LockQuestionPlanAsync(
                session,
                plan,
                rubric,
                preserveLegacyAttempts: false,
                cancellationToken);
            if (!lockResult.IsSuccess)
            {
                return Conflict<TechnicalInterviewSessionDto>(
                    lockResult.ErrorCode ?? "QUESTION_PLAN_LOCK_FAILED",
                    lockResult.Message ?? "All main questions could not be locked atomically.");
            }
            var previousOrders = await GetPreviousQuestionOrdersAsync(
                userId,
                session,
                lockResult.Plan!,
                cancellationToken);
            var randomizedPlan = _questionOrderRandomizer.Randomize(
                lockResult.Plan!,
                previousOrders);
            session.TechnicalQuestionPlanJson = TechnicalQuestionPlanSerializer.Serialize(randomizedPlan);
            session.TechnicalConcurrencyVersion++;
            session.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return TechnicalOperationResult<TechnicalInterviewSessionDto>.Created(MapSession(session));
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                var current = await GetOwnedSessionAsync(userId, request.InterviewSessionId, cancellationToken);
                if (current is not null && GetQuestionPlan(current)?.Slots.All(slot => slot.IsLocked) == true)
                    return TechnicalOperationResult<TechnicalInterviewSessionDto>.Ok(MapSession(current));
                return Conflict<TechnicalInterviewSessionDto>("SESSION_CONCURRENCY_CONFLICT", "The session changed concurrently.");
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                var current = await GetOwnedSessionAsync(userId, request.InterviewSessionId, cancellationToken);
                if (current is not null && GetQuestionPlan(current)?.Slots.All(slot => slot.IsLocked) == true)
                    return TechnicalOperationResult<TechnicalInterviewSessionDto>.Ok(MapSession(current));
                return Conflict<TechnicalInterviewSessionDto>("INITIALIZE_CONCURRENCY_CONFLICT", "Another request initialized the session concurrently.");
            }
        }

        public async Task<TechnicalOperationResult<TechnicalCurrentQuestionDto>> StartAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken)
        {
            var session = await GetOwnedSessionAsync(userId, sessionId, cancellationToken);
            if (session is null)
                return NotFound<TechnicalCurrentQuestionDto>();
            if (!session.TechnicalState.HasValue)
                return BadRequest<TechnicalCurrentQuestionDto>("TECHNICAL_SESSION_NOT_INITIALIZED", "Initialize the Technical session first.");
            if (session.TechnicalState == TechnicalInterviewState.Completed)
                return Conflict<TechnicalCurrentQuestionDto>("SESSION_COMPLETED", "The Technical session is already completed.");
            if (IsLifecycleClosed(session))
                return Conflict<TechnicalCurrentQuestionDto>("SESSION_ALREADY_ENDED", "The interview session was ended outside the Technical Interview flow.");

            var lockedPlan = await EnsureLockedPlanAsync(session, cancellationToken);
            if (!lockedPlan.IsSuccess)
            {
                await RecordLegacyUpgradeFailureAsync(session, lockedPlan, cancellationToken);
                return Conflict<TechnicalCurrentQuestionDto>(lockedPlan.ErrorCode!, lockedPlan.Message!);
            }

            var existingCurrent = GetReadyAttempt(session);
            if (existingCurrent is not null)
                return TechnicalOperationResult<TechnicalCurrentQuestionDto>.Ok(MapCurrentQuestion(session, existingCurrent));
            if (session.TechnicalState == TechnicalInterviewState.Evaluating)
                return Conflict<TechnicalCurrentQuestionDto>("ANSWER_PROCESSING", "The current answer is already being evaluated.");

            if (session.Status == InterviewSessionStatus.Pending)
            {
                var lifecycleResult = await _sessionLifecycleService.StartSessionAsync(userId, sessionId);
                if (!lifecycleResult.Success)
                    return Conflict<TechnicalCurrentQuestionDto>("SESSION_START_REJECTED", lifecycleResult.ErrorMessage ?? "The session cannot be started.");
            }
            else if (session.Status != InterviewSessionStatus.Active)
            {
                return Conflict<TechnicalCurrentQuestionDto>("INVALID_SESSION_STATUS", "Only a pending or active session can start Technical Interview.");
            }

            session.TechnicalStartedAt ??= DateTime.UtcNow;
            return await ActivateLockedMainQuestionAsync(session, cancellationToken);
        }

        public async Task<TechnicalOperationResult<TechnicalInterviewSessionDto>> GetSessionAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken)
        {
            var session = await GetOwnedSessionAsync(userId, sessionId, cancellationToken);
            if (session is null)
                return NotFound<TechnicalInterviewSessionDto>();
            if (IsLifecycleClosed(session) && session.TechnicalState != TechnicalInterviewState.Completed)
                return Conflict<TechnicalInterviewSessionDto>("SESSION_ALREADY_ENDED", "The interview session was ended in another flow or browser tab.");
            if (session.TechnicalState.HasValue && session.TechnicalState != TechnicalInterviewState.Completed)
            {
                var lockedPlan = await EnsureLockedPlanAsync(session, cancellationToken);
                if (!lockedPlan.IsSuccess)
                {
                    await RecordLegacyUpgradeFailureAsync(session, lockedPlan, cancellationToken);
                    return Conflict<TechnicalInterviewSessionDto>(lockedPlan.ErrorCode!, lockedPlan.Message!);
                }
            }
            await RecoverExpiredEvaluationAsync(session, cancellationToken);
            return TechnicalOperationResult<TechnicalInterviewSessionDto>.Ok(MapSession(session));
        }

        public async Task<TechnicalOperationResult<TechnicalCurrentQuestionDto>> GetCurrentQuestionAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken)
        {
            var session = await GetOwnedSessionAsync(userId, sessionId, cancellationToken);
            if (session is null)
                return NotFound<TechnicalCurrentQuestionDto>();
            if (IsLifecycleClosed(session) && session.TechnicalState != TechnicalInterviewState.Completed)
                return Conflict<TechnicalCurrentQuestionDto>("SESSION_ALREADY_ENDED", "The interview session was ended in another flow or browser tab.");

            if (session.TechnicalState.HasValue && session.TechnicalState != TechnicalInterviewState.Completed)
            {
                var lockedPlan = await EnsureLockedPlanAsync(session, cancellationToken);
                if (!lockedPlan.IsSuccess)
                {
                    await RecordLegacyUpgradeFailureAsync(session, lockedPlan, cancellationToken);
                    return Conflict<TechnicalCurrentQuestionDto>(lockedPlan.ErrorCode!, lockedPlan.Message!);
                }
            }

            await RecoverExpiredEvaluationAsync(session, cancellationToken);

            var attempt = GetReadyAttempt(session);
            return attempt is null
                ? Conflict<TechnicalCurrentQuestionDto>("NO_CURRENT_QUESTION", "The session does not have a question ready for answer.")
                : TechnicalOperationResult<TechnicalCurrentQuestionDto>.Ok(MapCurrentQuestion(session, attempt));
        }

        public async Task<TechnicalOperationResult<TechnicalSubmitAnswerResponseDto>> SubmitAnswerAsync(
            int userId,
            int sessionId,
            SubmitTechnicalAnswerRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
                return BadRequest<TechnicalSubmitAnswerResponseDto>("INVALID_IDEMPOTENCY_KEY", "A valid Idempotency-Key header is required.");

            var transcript = request.Transcript?.Trim() ?? string.Empty;
            if (transcript.Length == 0 || transcript.Length > _options.MaxTranscriptCharacters)
                return BadRequest<TechnicalSubmitAnswerResponseDto>("INVALID_TRANSCRIPT", $"Transcript length must be between 1 and {_options.MaxTranscriptCharacters} characters.");

            var session = await GetOwnedSessionAsync(userId, sessionId, cancellationToken);
            if (session is null)
                return NotFound<TechnicalSubmitAnswerResponseDto>();

            var lockedPlan = await EnsureLockedPlanAsync(session, cancellationToken);
            if (!lockedPlan.IsSuccess)
            {
                await RecordLegacyUpgradeFailureAsync(session, lockedPlan, cancellationToken);
                return Conflict<TechnicalSubmitAnswerResponseDto>(lockedPlan.ErrorCode!, lockedPlan.Message!);
            }

            var attempt = session.TechnicalQuestionAttempts.FirstOrDefault(item => item.AttemptId == request.AttemptId);
            if (attempt is null)
                return BadRequest<TechnicalSubmitAnswerResponseDto>("ATTEMPT_NOT_IN_SESSION", "Attempt does not belong to this session.");

            var existingEvaluation = attempt.Evaluations.SingleOrDefault();
            if (existingEvaluation is not null
                && string.Equals(attempt.SubmissionIdempotencyKey, idempotencyKey, StringComparison.Ordinal))
            {
                if (!string.Equals(attempt.AnswerTranscript, transcript, StringComparison.Ordinal))
                {
                    return Conflict<TechnicalSubmitAnswerResponseDto>(
                        "IDEMPOTENCY_PAYLOAD_MISMATCH",
                        "The Idempotency-Key was already used with a different answer transcript.");
                }
                _logger.LogInformation(
                    "Duplicate technical submission suppressed for session {SessionId}, attempt {AttemptId}. DuplicateCallCount={DuplicateCallCount}",
                    sessionId,
                    attempt.AttemptId,
                    1);
                return TechnicalOperationResult<TechnicalSubmitAnswerResponseDto>.Ok(
                    BuildSubmitResponse(session, attempt, existingEvaluation.Decision));
            }
            if (IsLifecycleClosed(session))
                return Conflict<TechnicalSubmitAnswerResponseDto>("SESSION_ALREADY_ENDED", "The interview session is no longer active.");
            if (attempt.Status == TechnicalAttemptStatus.Evaluating)
            {
                var isSameRequest = string.Equals(
                        attempt.SubmissionIdempotencyKey,
                        idempotencyKey,
                        StringComparison.Ordinal)
                    && string.Equals(attempt.AnswerTranscript, transcript, StringComparison.Ordinal);
                if (!IsEvaluationLeaseExpired(attempt))
                {
                    if (isSameRequest)
                    {
                        _logger.LogInformation(
                            "In-flight duplicate technical submission suppressed for session {SessionId}, attempt {AttemptId}. DuplicateCallCount={DuplicateCallCount}",
                            sessionId,
                            attempt.AttemptId,
                            1);
                    }
                    return isSameRequest
                        ? TechnicalOperationResult<TechnicalSubmitAnswerResponseDto>.Ok(
                            BuildSubmitResponse(session, attempt, TechnicalInterviewDecision.NextQuestion))
                        : Conflict<TechnicalSubmitAnswerResponseDto>(
                            "ANSWER_PROCESSING",
                            "This attempt is currently being evaluated by another request.");
                }

                // The previous request ended after persisting the processing state
                // but before persisting an evaluation. Reclaim the stale attempt;
                // the session concurrency token still permits only one recovery
                // request to commit, including after a browser refresh creates a
                // new idempotency key.
                attempt.Status = TechnicalAttemptStatus.Ready;
                session.TechnicalState = TechnicalInterviewState.QuestionReady;
            }

            if (session.TechnicalState != TechnicalInterviewState.QuestionReady
                || attempt.Status != TechnicalAttemptStatus.Ready)
            {
                return Conflict<TechnicalSubmitAnswerResponseDto>("INVALID_SUBMISSION_STATE", "This attempt is not ready for submission.");
            }

            if (session.TechnicalQuestionAttempts.Any(item =>
                item.AttemptId != attempt.AttemptId
                && item.SubmissionIdempotencyKey == idempotencyKey))
            {
                return Conflict<TechnicalSubmitAnswerResponseDto>("IDEMPOTENCY_KEY_REUSED", "The idempotency key was already used for another attempt.");
            }

            attempt.AnswerTranscript = transcript;
            attempt.AudioId = string.IsNullOrWhiteSpace(request.AudioId) ? null : request.AudioId.Trim();
            attempt.SubmissionIdempotencyKey = idempotencyKey;
            attempt.AnsweredAt = DateTime.UtcNow;
            attempt.Status = TechnicalAttemptStatus.Evaluating;
            attempt.EvaluationTaskStatus = TechnicalAITaskStatus.Processing;
            attempt.FeedbackTaskStatus = TechnicalAITaskStatus.NotStarted;
            // Backward-compatible derived status only. The backend selects any
            // sub-question from the locked Question Bank snapshot after scoring;
            // there is no question-generation AI operation.
            attempt.QuestionGenerationTaskStatus = TechnicalAITaskStatus.NotStarted;
            attempt.EvaluationFallbackUsed = false;
            attempt.FeedbackFallbackUsed = false;
            attempt.QuestionFallbackUsed = false;
            attempt.ProcessingStartedAt = DateTime.UtcNow;
            attempt.ProcessingCompletedAt = null;
            attempt.TotalProcessingLatencyMs = null;
            attempt.CriticalPathLatencyMs = null;
            attempt.SequentialEstimatedLatencyMs = null;
            attempt.ParallelLatencySavingMs = null;
            session.TechnicalState = TechnicalInterviewState.Evaluating;
            session.TechnicalConcurrencyVersion++;
            session.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict<TechnicalSubmitAnswerResponseDto>("CONCURRENT_SUBMISSION", "Another answer submission is already being processed.");
            }
            catch (DbUpdateException)
            {
                return Conflict<TechnicalSubmitAnswerResponseDto>("DUPLICATE_SUBMISSION", "The answer submission is duplicated.");
            }

            var root = session.TechnicalQuestionAttempts.Single(item => item.AttemptId == attempt.RootMainAttemptId);
            var rubric = _rubricProvider.GetRequired(session.TechnicalRubricVersion!);
            var children = session.TechnicalQuestionAttempts
                .Where(item => item.RootMainAttemptId == root.AttemptId && item.AttemptId != root.AttemptId)
                .ToList();
            var processingContext = BuildProcessingContext(
                session,
                attempt,
                root,
                children,
                rubric);
            var evaluationProcessing = await _evaluationProcessor.ProcessAsync(
                processingContext,
                cancellationToken);

            if (await IsLifecycleClosedInDatabaseAsync(session.InterviewSessionId, cancellationToken))
            {
                return Conflict<TechnicalSubmitAnswerResponseDto>(
                    "SESSION_ALREADY_ENDED",
                    "The interview session ended while the answer was being processed.");
            }

            var arbiterResult = _decisionArbiter.Resolve(
                processingContext,
                rubric,
                evaluationProcessing);

            TechnicalBankSubQuestionResult? bankSubQuestion = null;
            if (arbiterResult.IsSuccess && !arbiterResult.FinalizeMainQuestion)
            {
                var requestedType = arbiterResult.NextQuestion?.AttemptType;
                if (requestedType is TechnicalAttemptType.Clarification or TechnicalAttemptType.FollowUp)
                {
                    var followUpNumber = processingContext.CompletedFollowUpCount
                        + (attempt.QuestionType == TechnicalAttemptType.FollowUp ? 1 : 0)
                        + 1;
                    bankSubQuestion = await _selectionService.SelectBankSubQuestionAsync(
                        processingContext.LockedMainQuestion!,
                        requestedType.Value,
                        followUpNumber,
                        cancellationToken);
                    arbiterResult = bankSubQuestion.IsSuccess
                        ? arbiterResult with { QuestionStatus = TechnicalAITaskStatus.Fulfilled }
                        : FinalizeWithoutBankSubQuestion(
                            arbiterResult,
                            processingContext,
                            rubric,
                            bankSubQuestion.ErrorCode);
                }
            }

            ApplyProcessingOutcome(attempt, evaluationProcessing, arbiterResult);
            AddEvaluationInteractionLog(session, attempt.AttemptId, evaluationProcessing, arbiterResult);

            if (!arbiterResult.IsSuccess)
            {
                if (string.Equals(arbiterResult.ErrorCode, "NO_ACTIVE_NEXT_QUESTION", StringComparison.Ordinal))
                {
                    attempt.Status = TechnicalAttemptStatus.Failed;
                    session.TechnicalState = TechnicalInterviewState.Failed;
                    session.TechnicalConcurrencyVersion++;
                    session.UpdatedAt = DateTime.UtcNow;
                    if (!await TrySaveAnswerOutcomeAsync(cancellationToken))
                        return Conflict<TechnicalSubmitAnswerResponseDto>(
                            "SESSION_CONCURRENCY_CONFLICT",
                            "The session changed while the answer evaluation was being persisted.");
                    return Conflict<TechnicalSubmitAnswerResponseDto>(
                        "NO_ACTIVE_NEXT_QUESTION",
                        "Evaluation completed, but no active Question Bank candidate is available for the next main question.");
                }

                await ResetFailedEvaluationAsync(session, attempt, cancellationToken);
                return ExternalFailure<TechnicalSubmitAnswerResponseDto>(
                    arbiterResult.ErrorCode ?? "AI_EVALUATION_FAILED",
                    "Answer evaluation failed backend validation. The same attempt can be submitted again.");
            }

            var evaluationResult = evaluationProcessing.Evaluation.ProviderResult;
            var evaluationData = arbiterResult.EffectiveEvaluation!;
            var score = arbiterResult.Score!;

            attempt.RawScore = arbiterResult.RawScore;
            attempt.AppliedBonus = attempt.QuestionType == TechnicalAttemptType.FollowUp
                ? arbiterResult.AppliedBonus
                : null;
            attempt.BonusCalculationVersion = attempt.QuestionType == TechnicalAttemptType.FollowUp
                ? session.TechnicalBonusCalculationVersion
                : null;
            if (attempt.QuestionType == TechnicalAttemptType.Main)
            {
                root.InitialMainScore = arbiterResult.RawScore;
            }
            else if (attempt.QuestionType == TechnicalAttemptType.Clarification)
            {
                root.CompletedClarificationCount++;
            }
            else if (attempt.QuestionType == TechnicalAttemptType.FollowUp)
            {
                root.CompletedFollowUpCount++;
            }

            root.RequiredClarificationCount = arbiterResult.RequiredClarificationCount;
            root.RequiredFollowUpCount = arbiterResult.RequiredFollowUpCount;
            root.CumulativeFollowUpBonus = arbiterResult.CumulativeFollowUpBonus;
            root.AdaptiveStage = arbiterResult.AdaptiveStage;
            if (arbiterResult.FinalMainQuestionScore.HasValue)
            {
                root.FinalMainScore = arbiterResult.FinalMainQuestionScore.Value;
            }

            var evaluation = new TechnicalAnswerEvaluation
            {
                AttemptId = attempt.AttemptId,
                Attempt = attempt,
                RootMainAttemptId = root.AttemptId,
                RubricVersion = rubric.Version,
                ScoringPolicyVersion = rubric.ScoringPolicyVersion,
                AiSuggestedOverallScore = score.AiSuggestedOverallScore,
                FinalOverallScore = score.FinalOverallScore,
                DimensionEvaluationsJson = JsonSerializer.Serialize(evaluationData.DimensionEvaluations, JsonOptions),
                ScoringBreakdownJson = JsonSerializer.Serialize(score.Dimensions, JsonOptions),
                StrengthsJson = "[]",
                MissingPointsJson = "[]",
                IncorrectClaimsJson = "[]",
                ImprovementSuggestionsJson = "[]",
                FeedbackSummary = string.Empty,
                FeedbackPromptVersion = string.Empty,
                FeedbackModelName = string.Empty,
                FeedbackFallbackUsed = false,
                Decision = arbiterResult.Decision,
                AiSuggestedAction = arbiterResult.AiSuggestedAction,
                BackendResolvedAction = arbiterResult.Decision,
                DecisionReason = arbiterResult.DecisionReason,
                TargetRubricCodesJson = JsonSerializer.Serialize(
                    arbiterResult.NextQuestion?.TargetRubricCodes
                        ?? Array.Empty<string>(),
                    JsonOptions),
                AdaptiveRuleVersion = session.TechnicalAdaptiveRuleVersion,
                OverrideReason = arbiterResult.OverrideReason,
                FallbackUsed = arbiterResult.EvaluationFallbackUsed
                    || arbiterResult.QuestionFallbackUsed,
                Confidence = evaluationData.Confidence,
                PromptVersion = TechnicalPromptVersions.Evaluation,
                ModelName = evaluationResult?.Model ?? session.TechnicalAiModel ?? _options.Model,
                IsFinalForMainQuestion = arbiterResult.FinalizeMainQuestion,
                CreatedAt = DateTime.UtcNow
            };
            _context.TechnicalAnswerEvaluations.Add(evaluation);

            attempt.Status = TechnicalAttemptStatus.Completed;
            attempt.CompletedAt = DateTime.UtcNow;
            if (!arbiterResult.FinalizeMainQuestion)
            {
                var nextAttempt = CreateSubQuestionAttempt(
                    session,
                    attempt,
                    root,
                    arbiterResult.NextQuestion!.AttemptType!.Value,
                    bankSubQuestion!.SourceQuestionId,
                    bankSubQuestion.Content!,
                    arbiterResult.NextQuestion.GenerationReason
                        ?? TechnicalQuestionGenerationReason.AdaptiveScoreRule);
                _context.TechnicalQuestionAttempts.Add(nextAttempt);
                session.TechnicalState = TechnicalInterviewState.QuestionReady;
                session.TechnicalConcurrencyVersion++;
                session.UpdatedAt = DateTime.UtcNow;
                if (!await TrySaveAnswerOutcomeAsync(cancellationToken))
                    return Conflict<TechnicalSubmitAnswerResponseDto>(
                        "SESSION_CONCURRENCY_CONFLICT",
                        "The session changed while the answer evaluation was being persisted.");

                return TechnicalOperationResult<TechnicalSubmitAnswerResponseDto>.Ok(
                    BuildSubmitResponse(session, attempt, arbiterResult.Decision));
            }

            foreach (var rootAttempt in session.TechnicalQuestionAttempts.Where(item => item.RootMainAttemptId == root.AttemptId))
            {
                rootAttempt.Status = TechnicalAttemptStatus.Completed;
                rootAttempt.CompletedAt ??= DateTime.UtcNow;
            }

            session.TechnicalCompletedMainQuestionCount++;
            if (arbiterResult.Decision == TechnicalInterviewDecision.EndInterview)
            {
                if (session.InterviewCampaign.Mode == InterviewMode.RealTest
                    && processingContext.ReliabilityCount < _options.ReliabilityMinimumQuestionCount)
                {
                    session.TechnicalReliabilityFailureReason = "RELIABILITY_MINIMUM_CAPACITY_EXHAUSTED";
                    session.TechnicalState = TechnicalInterviewState.Failed;
                    session.TechnicalConcurrencyVersion++;
                    session.UpdatedAt = DateTime.UtcNow;
                    if (!await TrySaveAnswerOutcomeAsync(cancellationToken))
                        return Conflict<TechnicalSubmitAnswerResponseDto>(
                            "SESSION_CONCURRENCY_CONFLICT",
                            "The session changed while reliability failure state was being persisted.");
                    return Conflict<TechnicalSubmitAnswerResponseDto>(
                        "RELIABILITY_MINIMUM_UNREACHABLE",
                        "The round exhausted valid sub-question capacity before reaching the reliability minimum.");
                }
                var finalResult = await FinalizeSessionAsync(
                    session,
                    userId,
                    cancellationToken);
                if (finalResult.Status != TechnicalOperationStatus.Ok)
                    return TechnicalOperationResult<TechnicalSubmitAnswerResponseDto>.Failure(
                        finalResult.Status,
                        finalResult.ErrorCode ?? "FINALIZATION_FAILED",
                        finalResult.Message ?? "Technical session finalization failed.");

                return TechnicalOperationResult<TechnicalSubmitAnswerResponseDto>.Ok(
                    BuildSubmitResponse(session, attempt, arbiterResult.Decision));
            }

            var nextMainIndex = root.MainQuestionIndex + 1;
            var nextSlot = GetQuestionPlan(session)?.Slots.FirstOrDefault(item =>
                item.MainQuestionIndex == nextMainIndex);
            if (nextSlot?.LockedQuestion is null)
            {
                attempt.Status = TechnicalAttemptStatus.Failed;
                session.TechnicalState = TechnicalInterviewState.Failed;
                session.TechnicalConcurrencyVersion++;
                session.UpdatedAt = DateTime.UtcNow;
                if (!await TrySaveAnswerOutcomeAsync(cancellationToken))
                    return Conflict<TechnicalSubmitAnswerResponseDto>(
                        "SESSION_CONCURRENCY_CONFLICT",
                        "The session changed while the answer evaluation was being persisted.");
                return Conflict<TechnicalSubmitAnswerResponseDto>(
                    "LOCKED_MAIN_QUESTION_MISSING",
                    "The next locked Main question is unavailable.");
            }

            var nextMainAttempt = CreateMainQuestionAttempt(session, nextSlot);
            _context.TechnicalQuestionAttempts.Add(nextMainAttempt);
            session.TechnicalState = TechnicalInterviewState.QuestionReady;
            session.TechnicalConcurrencyVersion++;
            session.UpdatedAt = DateTime.UtcNow;
            if (!await TrySaveAnswerOutcomeAsync(cancellationToken))
                return Conflict<TechnicalSubmitAnswerResponseDto>(
                    "SESSION_CONCURRENCY_CONFLICT",
                    "The session changed while the answer evaluation was being persisted.");

            return TechnicalOperationResult<TechnicalSubmitAnswerResponseDto>.Ok(
                BuildSubmitResponse(session, attempt, arbiterResult.Decision));
        }

        private bool IsEvaluationLeaseExpired(TechnicalQuestionAttempt attempt)
        {
            if (!attempt.ProcessingStartedAt.HasValue)
            {
                return true;
            }

            var leaseDuration = TimeSpan.FromMilliseconds(_options.EvaluationTimeoutMs + 10_000);
            return attempt.ProcessingStartedAt.Value <= DateTime.UtcNow - leaseDuration;
        }

        private async Task<bool> RecoverExpiredEvaluationAsync(
            InterviewSession session,
            CancellationToken cancellationToken)
        {
            var expired = session.TechnicalQuestionAttempts
                .Where(item =>
                    item.Status == TechnicalAttemptStatus.Evaluating
                    && item.Evaluations.Count == 0
                    && IsEvaluationLeaseExpired(item))
                .OrderByDescending(item => item.SequenceNumber)
                .FirstOrDefault();
            if (expired is null)
            {
                return false;
            }

            expired.Status = TechnicalAttemptStatus.Ready;
            expired.AnswerTranscript = null;
            expired.AudioId = null;
            expired.SubmissionIdempotencyKey = null;
            expired.AnsweredAt = null;
            expired.EvaluationTaskStatus = TechnicalAITaskStatus.NotStarted;
            expired.FeedbackTaskStatus = TechnicalAITaskStatus.NotStarted;
            expired.QuestionGenerationTaskStatus = TechnicalAITaskStatus.NotStarted;
            expired.ProcessingStartedAt = null;
            expired.ProcessingCompletedAt = null;
            session.TechnicalState = TechnicalInterviewState.QuestionReady;
            session.TechnicalConcurrencyVersion++;
            session.UpdatedAt = DateTime.UtcNow;
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                return false;
            }
        }

        public async Task<TechnicalOperationResult<TechnicalInterviewResultDto>> CompleteAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken)
        {
            var session = await GetOwnedSessionAsync(userId, sessionId, cancellationToken);
            if (session is null)
                return NotFound<TechnicalInterviewResultDto>();
            if (session.TechnicalState == TechnicalInterviewState.Completed)
            {
                if (!await EnsureLifecycleCompletionAsync(userId, session))
                    return Conflict<TechnicalInterviewResultDto>(
                        "ROUND_LIFECYCLE_TRANSITION_FAILED",
                        "The Technical result is ready, but the next interview round could not be activated.");
                return TechnicalOperationResult<TechnicalInterviewResultDto>.Ok(BuildResult(session));
            }
            if (IsLifecycleClosed(session))
                return Conflict<TechnicalInterviewResultDto>("SESSION_ALREADY_ENDED", "The interview session is no longer active.");
            if (!string.IsNullOrWhiteSpace(session.TechnicalReliabilityFailureReason))
                return Conflict<TechnicalInterviewResultDto>(
                    "RELIABILITY_MINIMUM_UNREACHABLE",
                    session.TechnicalReliabilityFailureReason);
            if (session.TechnicalCompletedMainQuestionCount < GetTargetMainQuestionCount(session))
                return Conflict<TechnicalInterviewResultDto>("MAIN_QUESTION_TARGET_NOT_REACHED", "The required main questions are not completed.");
            if (session.TechnicalQuestionAttempts.Any(item => item.Status == TechnicalAttemptStatus.Ready))
                return Conflict<TechnicalInterviewResultDto>("QUESTION_STILL_PENDING", "The current question must be answered before completion.");
            if (session.TechnicalQuestionAttempts.Any(item => item.Status == TechnicalAttemptStatus.Evaluating))
                return Conflict<TechnicalInterviewResultDto>("EVALUATION_STILL_PROCESSING", "An answer evaluation is still processing.");
            var lockedPlan = GetQuestionPlan(session);
            if (lockedPlan is null
                || lockedPlan.Slots.Count(slot => slot.IsLocked) != GetTargetMainQuestionCount(session))
            {
                return Conflict<TechnicalInterviewResultDto>("LOCKED_MAIN_SET_INCOMPLETE", "Completion requires the complete locked Main-question set.");
            }

            return await FinalizeSessionAsync(session, userId, cancellationToken);
        }

        public async Task<TechnicalOperationResult<TechnicalInterviewResultDto>> GetResultAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken)
        {
            var session = await GetOwnedSessionAsync(userId, sessionId, cancellationToken);
            if (session is null)
                return NotFound<TechnicalInterviewResultDto>();
            if (IsLifecycleClosed(session) && session.TechnicalState != TechnicalInterviewState.Completed)
                return Conflict<TechnicalInterviewResultDto>("SESSION_ALREADY_ENDED", "The interview session ended before a Technical result was available.");
            if (session.TechnicalState != TechnicalInterviewState.Completed)
                return Conflict<TechnicalInterviewResultDto>("SESSION_NOT_COMPLETED", "Technical result is available only after completion.");

            if (!await EnsureLifecycleCompletionAsync(userId, session))
                return Conflict<TechnicalInterviewResultDto>(
                    "ROUND_LIFECYCLE_TRANSITION_FAILED",
                    "The Technical result is ready, but the next interview round could not be activated.");

            return TechnicalOperationResult<TechnicalInterviewResultDto>.Ok(BuildResult(session));
        }

        private async Task<TechnicalOperationResult<TechnicalCurrentQuestionDto>> ActivateLockedMainQuestionAsync(
            InterviewSession session,
            CancellationToken cancellationToken)
        {
            var plan = GetQuestionPlan(session);
            var nextIndex = session.TechnicalCompletedMainQuestionCount + 1;
            var slot = plan?.Slots.FirstOrDefault(item => item.MainQuestionIndex == nextIndex);
            if (slot?.LockedQuestion is null)
            {
                return Conflict<TechnicalCurrentQuestionDto>(
                    "LOCKED_MAIN_QUESTION_MISSING",
                    $"Locked Main question {nextIndex} is unavailable.");
            }

            var existing = session.TechnicalQuestionAttempts.FirstOrDefault(item =>
                item.QuestionType == TechnicalAttemptType.Main
                && item.MainQuestionIndex == nextIndex);
            if (existing is not null)
                return TechnicalOperationResult<TechnicalCurrentQuestionDto>.Ok(MapCurrentQuestion(session, existing));

            var attempt = CreateMainQuestionAttempt(session, slot);
            _context.TechnicalQuestionAttempts.Add(attempt);
            session.TechnicalState = TechnicalInterviewState.QuestionReady;
            session.TechnicalConcurrencyVersion++;
            session.UpdatedAt = DateTime.UtcNow;
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict<TechnicalCurrentQuestionDto>(
                    "SESSION_CONCURRENCY_CONFLICT",
                    "The session changed while the selected question was being persisted.");
            }
            catch (DbUpdateException)
            {
                return Conflict<TechnicalCurrentQuestionDto>(
                    "DUPLICATE_START",
                    "The locked Main question was already activated by another request.");
            }

            return TechnicalOperationResult<TechnicalCurrentQuestionDto>.Created(
                MapCurrentQuestion(session, attempt));
        }

        private TechnicalAnswerProcessingContext BuildProcessingContext(
            InterviewSession session,
            TechnicalQuestionAttempt attempt,
            TechnicalQuestionAttempt root,
            IReadOnlyList<TechnicalQuestionAttempt> children,
            TechnicalRubricDefinition rubric)
        {
            var campaign = session.InterviewCampaign;
            var cvSkills = campaign.CVExtractedProfile.Skills.Select(item => item.SkillName).ToList();
            var mainAttempts = session.TechnicalQuestionAttempts
                .Where(item => item.QuestionType == TechnicalAttemptType.Main)
                .ToList();
            var plan = GetQuestionPlan(session);
            var currentPlanSlot = plan?.Slots.FirstOrDefault(slot =>
                slot.MainQuestionIndex == root.MainQuestionIndex);
            var lockedMain = currentPlanSlot?.LockedQuestion
                ?? throw new InvalidOperationException("Technical processing requires a locked Main question snapshot.");
            var completedFollowUps = session.TechnicalQuestionAttempts.Count(item =>
                item.QuestionType == TechnicalAttemptType.FollowUp
                && item.Status == TechnicalAttemptStatus.Completed);
            var completedClarifications = session.TechnicalQuestionAttempts.Count(item =>
                item.QuestionType == TechnicalAttemptType.Clarification
                && item.Status == TechnicalAttemptStatus.Completed);
            var projectedFollowUps = completedFollowUps
                + (attempt.QuestionType == TechnicalAttemptType.FollowUp ? 1 : 0);
            var useAdaptiveFramework = plan is not null
                && plan.Slots.All(slot => slot.IsLocked);
            var targetMainQuestionCount = GetTargetMainQuestionCount(session);
            var effectiveReliabilityLimit = Math.Max(
                _options.ReliabilityFollowUpLimit,
                Math.Max(0, _options.ReliabilityMinimumQuestionCount - targetMainQuestionCount));
            var reliabilityCount = targetMainQuestionCount + projectedFollowUps;
            var rootProjectedClarifications = root.CompletedClarificationCount
                + (attempt.QuestionType == TechnicalAttemptType.Clarification ? 1 : 0);
            var rootProjectedFollowUps = root.CompletedFollowUpCount
                + (attempt.QuestionType == TechnicalAttemptType.FollowUp ? 1 : 0);
            var rootHasFollowUpCapacity = rootProjectedFollowUps < rubric.Limits.MaxFollowUpsPerMainQuestion
                && rootProjectedClarifications + rootProjectedFollowUps < rubric.Limits.MaxTotalSubQuestionsPerMainQuestion;
            var reliabilityRequired = useAdaptiveFramework
                && root.MainQuestionIndex == targetMainQuestionCount
                && projectedFollowUps < effectiveReliabilityLimit
                && reliabilityCount < _options.ReliabilityMinimumQuestionCount
                && rootHasFollowUpCapacity;
            var clarificationScore = children
                .Where(item => item.QuestionType == TechnicalAttemptType.Clarification && item.RawScore.HasValue)
                .OrderByDescending(item => item.SequenceWithinMain)
                .Select(item => item.RawScore)
                .FirstOrDefault();
            var currentMainBaseScore = clarificationScore.HasValue
                ? _scoringService.ApplyClarificationRecovery(
                    clarificationScore.Value,
                    _options.ClarificationRecoveryFactor,
                    rubric)
                : root.InitialMainScore ?? 0m;
            var previousAnswers = session.TechnicalQuestionAttempts
                .Where(item =>
                    item.RootMainAttemptId == root.AttemptId
                    && item.AttemptId != attempt.AttemptId
                    && item.AnswerTranscript != null)
                .OrderBy(item => item.SequenceNumber)
                .Select(item => new TechnicalAnswerContext(
                    ToApi(item.QuestionType),
                    item.QuestionContentSnapshot,
                    item.AnswerTranscript!))
                .ToImmutableArray();
            var latestPriorEvaluation = session.TechnicalQuestionAttempts
                .Where(item => item.RootMainAttemptId == root.AttemptId
                    && item.AttemptId != attempt.AttemptId)
                .SelectMany(item => item.Evaluations.Select(evaluation => new
                {
                    item.SequenceWithinMain,
                    Evaluation = evaluation
                }))
                .OrderByDescending(item => item.SequenceWithinMain)
                .ThenByDescending(item => item.Evaluation.CreatedAt)
                .Select(item => item.Evaluation)
                .FirstOrDefault();
            var remainingMissingEvidence = latestPriorEvaluation is null
                ? ImmutableArray<string>.Empty
                : Deserialize<TechnicalAIDimensionEvaluation>(latestPriorEvaluation.DimensionEvaluationsJson)
                    .SelectMany(item => item.MissingEvidence)
                    .Concat(DeserializeList(latestPriorEvaluation.MissingPointsJson))
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToImmutableArray();
            var collectedEvidence = session.TechnicalQuestionAttempts
                .Where(item => item.RootMainAttemptId == root.AttemptId && item.AttemptId != attempt.AttemptId)
                .SelectMany(item => item.Evaluations)
                .SelectMany(item => Deserialize<TechnicalAIDimensionEvaluation>(item.DimensionEvaluationsJson))
                .SelectMany(item => item.Evidence)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray();
            var priorIncorrectClaims = session.TechnicalQuestionAttempts
                .Where(item => item.RootMainAttemptId == root.AttemptId && item.AttemptId != attempt.AttemptId)
                .SelectMany(item => item.Evaluations)
                .SelectMany(item => DeserializeList(item.IncorrectClaimsJson))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray();
            var previousScores = session.TechnicalQuestionAttempts
                .Where(item => item.RootMainAttemptId == root.AttemptId
                    && item.AttemptId != attempt.AttemptId
                    && item.RawScore.HasValue)
                .OrderBy(item => item.SequenceWithinMain)
                .Select(item => item.RawScore!.Value)
                .ToImmutableArray();
            var targetSkill = root.TargetSkillSnapshot ?? currentPlanSlot?.TargetSkill ?? string.Empty;
            var relatedCvSkills = cvSkills
                .Where(skill => !string.IsNullOrWhiteSpace(targetSkill)
                    && TechnicalQuestionMetadata.FuzzyMatches(skill, targetSkill))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToArray();
            var relatedJdSkills = TechnicalQuestionMetadata
                .ParseStringArray(campaign.JDExtractedProfile.RequiredSkills)
                .Where(skill => !string.IsNullOrWhiteSpace(targetSkill)
                    && TechnicalQuestionMetadata.FuzzyMatches(skill, targetSkill))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToArray();

            return new TechnicalAnswerProcessingContext
            {
                SessionId = session.InterviewSessionId,
                AttemptId = attempt.AttemptId,
                RootMainAttemptId = root.AttemptId,
                QuestionId = lockedMain.SelectedQuestionId,
                QuestionType = ToApi(attempt.QuestionType),
                AttemptType = attempt.QuestionType,
                QuestionContent = attempt.QuestionContentSnapshot,
                MainQuestionContent = root.QuestionContentSnapshot,
                ExpectedAnswer = lockedMain.ExpectedAnswer,
                KeyPoints = lockedMain.ExpectedKeyPoints,
                QuestionSpecificRubric = lockedMain.QuestionSpecificRubric,
                GlobalRubricVersion = rubric.Version,
                Rubric = new TechnicalRubricPromptSnapshot(
                    rubric.MinimumScore,
                    rubric.MaximumScore,
                    rubric.EvidenceRequiredWhenScoreAbove,
                    rubric.Dimensions.Select(item => new TechnicalRubricPromptDimension(
                        item.Code,
                        item.Name,
                        item.Description,
                        item.Weight)).ToImmutableArray(),
                    rubric.Levels.Select(item => new TechnicalRubricPromptLevel(
                        item.Code,
                        item.Score,
                        item.Description)).ToImmutableArray()),
                CandidateAnswer = attempt.AnswerTranscript
                    ?? throw new InvalidOperationException("Attempt answer was not persisted before processing."),
                PreviousAnswers = previousAnswers,
                RemainingMissingEvidence = remainingMissingEvidence,
                CollectedEvidence = collectedEvidence,
                PreviousIncorrectClaims = priorIncorrectClaims,
                PreviousAttemptScores = previousScores,
                JobRole = session.TechnicalJobRole ?? string.Empty,
                ExperienceLevel = session.TechnicalExperienceLevel ?? string.Empty,
                Language = session.TechnicalLanguage ?? string.Empty,
                CvContext = JsonSerializer.Serialize(new
                {
                    roleTarget = campaign.CVExtractedProfile.RoleTarget,
                    skills = relatedCvSkills
                }, JsonOptions),
                JdContext = JsonSerializer.Serialize(new
                {
                    campaign.JDExtractedProfile.JobTitle,
                    campaign.JDExtractedProfile.RoleTarget,
                    campaign.JDExtractedProfile.ExperienceLevel,
                    requiredSkills = relatedJdSkills
                }, JsonOptions),
                ClarificationCount = children.Count(item => item.QuestionType == TechnicalAttemptType.Clarification),
                FollowUpCount = children.Count(item => item.QuestionType == TechnicalAttemptType.FollowUp),
                CompletedMainQuestionCount = session.TechnicalCompletedMainQuestionCount,
                MainQuestionIndex = root.MainQuestionIndex,
                TargetMainQuestionCount = targetMainQuestionCount,
                PromptVersions = new TechnicalPromptVersionSnapshot(
                    TechnicalPromptVersions.Evaluation),
                UseAdaptiveRubricFramework = useAdaptiveFramework,
                CurrentPlanSlot = currentPlanSlot,
                SourceType = root.SourceType ?? currentPlanSlot?.SourceType,
                TargetSkill = targetSkill,
                TargetSubskill = root.TargetSubskillSnapshot ?? currentPlanSlot?.TargetSubskill,
                EvaluationObjective = root.EvaluationObjective ?? currentPlanSlot?.EvaluationObjective,
                InitialMainScore = root.InitialMainScore,
                CurrentMainBaseScore = currentMainBaseScore,
                RequiredClarificationCount = root.RequiredClarificationCount,
                CompletedClarificationCount = root.CompletedClarificationCount,
                RequiredFollowUpCount = root.RequiredFollowUpCount,
                CompletedFollowUpCount = root.CompletedFollowUpCount,
                CumulativeFollowUpBonus = root.CumulativeFollowUpBonus,
                RemainingSubQuestionBudget = Math.Max(
                    0,
                    rubric.Limits.MaxTotalSubQuestionsPerMainQuestion
                        - rootProjectedClarifications
                        - rootProjectedFollowUps),
                ReliabilityCount = reliabilityCount,
                ReliabilityMinimumQuestionCount = _options.ReliabilityMinimumQuestionCount,
                IsReliabilityFollowUpRequired = reliabilityRequired,
                ScoringPolicyVersion = session.TechnicalScoringPolicyVersion ?? rubric.ScoringPolicyVersion,
                AdaptiveRuleVersion = session.TechnicalAdaptiveRuleVersion ?? "legacy-rubric-rule-v1",
                BonusCalculationVersion = session.TechnicalBonusCalculationVersion ?? string.Empty,
                LockedMainQuestion = lockedMain
            };
        }

        private async Task<TechnicalOperationResult<TechnicalInterviewResultDto>> FinalizeSessionAsync(
            InterviewSession session,
            int userId,
            CancellationToken cancellationToken)
        {
            var rubric = _rubricProvider.GetRequired(session.TechnicalRubricVersion!);
            var lockedPlan = GetQuestionPlan(session);
            var targetMainQuestionCount = GetTargetMainQuestionCount(session);
            if (lockedPlan is null
                || lockedPlan.Slots.Count(slot => slot.IsLocked) != targetMainQuestionCount
                || session.TechnicalQuestionAttempts.Any(item => item.Status is
                    TechnicalAttemptStatus.Ready or TechnicalAttemptStatus.Evaluating))
            {
                return Conflict<TechnicalInterviewResultDto>(
                    "TECHNICAL_COMPLETION_INVARIANT_FAILED",
                    "Locked questions, final scores and processing state are not ready for completion.");
            }
            var useAdaptiveFramework = GetQuestionPlan(session) is not null
                && session.InterviewCampaign.Mode != InterviewMode.Practice;
            IReadOnlyList<decimal> finalMainScores;
            if (useAdaptiveFramework)
            {
                var finalizedRoots = session.TechnicalQuestionAttempts
                    .Where(item => item.QuestionType == TechnicalAttemptType.Main && item.FinalMainScore.HasValue)
                    .OrderBy(item => item.MainQuestionIndex)
                    .ToList();
                if (finalizedRoots.Count != targetMainQuestionCount)
                {
                    return Conflict<TechnicalInterviewResultDto>(
                        "INCOMPLETE_SCORING_BREAKDOWN",
                        "An official Technical Score requires exactly three finalized Main Questions.");
                }

                finalMainScores = finalizedRoots.Select(item => item.FinalMainScore!.Value).ToList();
            }
            else
            {
                var finalEvaluations = session.TechnicalQuestionAttempts
                    .SelectMany(item => item.Evaluations)
                    .Where(item => item.IsFinalForMainQuestion)
                    .ToList();
                if (finalEvaluations.Count != session.TechnicalCompletedMainQuestionCount)
                {
                    return Conflict<TechnicalInterviewResultDto>(
                        "INCOMPLETE_SCORING_BREAKDOWN",
                        "Final main-question evaluations are incomplete.");
                }

                finalMainScores = finalEvaluations.Select(item => item.FinalOverallScore).ToList();
            }

            var finalScore = _scoringService.ScoreSession(
                finalMainScores,
                rubric,
                targetMainQuestionCount);
            var bandCode = rubric.GetPerformanceBandCode(finalScore);
            session.TechnicalFinalScore = finalScore;
            session.TechnicalPerformanceBand = bandCode;
            session.TechnicalState = TechnicalInterviewState.Completed;
            session.TechnicalCompletedAt = DateTime.UtcNow;
            session.TechnicalFinalFeedbackStatus = "PROCESSING";
            session.TechnicalFinalFeedbackStartedAt = DateTime.UtcNow;
            session.TechnicalFinalFeedbackError = null;
            session.TechnicalConcurrencyVersion++;
            session.UpdatedAt = DateTime.UtcNow;
            if (!await TrySaveAnswerOutcomeAsync(cancellationToken))
            {
                return Conflict<TechnicalInterviewResultDto>(
                    "SESSION_CONCURRENCY_CONFLICT",
                    "The session changed while final Technical Interview results were being persisted.");
            }

            // Persist official backend scores and completion before the one-time
            // synthesis call. Feedback failure must never roll the result back.
            await TryGenerateFinalFeedbackAsync(session, rubric, cancellationToken);
            session.UpdatedAt = DateTime.UtcNow;
            if (!await TrySaveAnswerOutcomeAsync(cancellationToken))
            {
                return Conflict<TechnicalInterviewResultDto>(
                    "SESSION_CONCURRENCY_CONFLICT",
                    "The Technical score is complete, but final feedback persistence must be retried.");
            }

            await EnsureLifecycleCompletionAsync(userId, session);

            return TechnicalOperationResult<TechnicalInterviewResultDto>.Ok(BuildResult(session));
        }

        public async Task<TechnicalOperationResult<TechnicalInterviewResultDto>> GenerateFeedbackAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken)
        {
            var feedbackGate = FeedbackGates.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
            await feedbackGate.WaitAsync(cancellationToken);
            try
            {
                var session = await GetOwnedSessionAsync(userId, sessionId, cancellationToken);
                if (session is null)
                    return NotFound<TechnicalInterviewResultDto>();
                if (session.TechnicalState != TechnicalInterviewState.Completed)
                    return Conflict<TechnicalInterviewResultDto>(
                        "ROUND_NOT_COMPLETED",
                        "Final feedback can only be generated after the Technical round is completed.");
                if (!string.IsNullOrWhiteSpace(session.TechnicalSummaryJson))
                    return TechnicalOperationResult<TechnicalInterviewResultDto>.Ok(BuildResult(session));

                var feedbackLease = TimeSpan.FromSeconds(_options.TimeoutSeconds + 10);
                if (string.Equals(session.TechnicalFinalFeedbackStatus, "PROCESSING", StringComparison.Ordinal)
                    && session.TechnicalFinalFeedbackStartedAt > DateTime.UtcNow - feedbackLease)
                {
                    return Conflict<TechnicalInterviewResultDto>(
                        "FINAL_FEEDBACK_PROCESSING",
                        "Final Technical feedback is already being generated.");
                }

                session.TechnicalFinalFeedbackStatus = "PROCESSING";
                session.TechnicalFinalFeedbackStartedAt = DateTime.UtcNow;
                session.TechnicalFinalFeedbackError = null;
                session.TechnicalConcurrencyVersion++;
                session.UpdatedAt = DateTime.UtcNow;
                if (!await TrySaveAnswerOutcomeAsync(cancellationToken))
                    return Conflict<TechnicalInterviewResultDto>(
                        "FINAL_FEEDBACK_PROCESSING",
                        "Another request already claimed final Technical feedback generation.");

                var rubric = _rubricProvider.GetRequired(session.TechnicalRubricVersion!);
                var generated = await TryGenerateFinalFeedbackAsync(session, rubric, cancellationToken);
                session.TechnicalConcurrencyVersion++;
                session.UpdatedAt = DateTime.UtcNow;
                if (!await TrySaveAnswerOutcomeAsync(cancellationToken))
                    return Conflict<TechnicalInterviewResultDto>(
                        "SESSION_CONCURRENCY_CONFLICT",
                        "The session changed while final Technical feedback was being persisted.");

                return generated
                    ? TechnicalOperationResult<TechnicalInterviewResultDto>.Ok(BuildResult(session))
                    : ExternalFailure<TechnicalInterviewResultDto>(
                        "FINAL_FEEDBACK_FAILED",
                        "The Technical score remains completed; final feedback can be retried without re-evaluating answers.");
            }
            finally
            {
                feedbackGate.Release();
            }
        }

        private async Task<bool> TryGenerateFinalFeedbackAsync(
            InterviewSession session,
            TechnicalRubricDefinition rubric,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(session.TechnicalSummaryJson))
                return true;

            var provisionalResult = BuildResult(session, includeStoredSummary: false);
            var jdRequiredSkills = TechnicalQuestionMetadata.ParseStringArray(
                session.InterviewCampaign.JDExtractedProfile?.RequiredSkills);
            var requiredSkills = jdRequiredSkills.Count > 0
                ? jdRequiredSkills
                : DeserializeList(session.TechnicalSelectedSkillsJson);
            var summaryRequest = new TechnicalAIFinalSummaryRequest
            {
                RubricVersion = rubric.Version,
                JobRole = session.TechnicalJobRole ?? string.Empty,
                ExperienceLevel = session.TechnicalExperienceLevel ?? string.Empty,
                Language = session.TechnicalLanguage ?? string.Empty,
                RequiredSkills = requiredSkills,
                CvJdMatchScore = session.TechnicalMatchScoreSnapshot,
                CvContext = JsonSerializer.Serialize(new
                {
                    roleTarget = session.InterviewCampaign.CVExtractedProfile?.RoleTarget,
                    skills = session.InterviewCampaign.CVExtractedProfile?.Skills
                        .Select(skill => skill.SkillName)
                        .Where(skill => !string.IsNullOrWhiteSpace(skill))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(20)
                        .ToList()
                        ?? new List<string>()
                }, JsonOptions),
                JdContext = JsonSerializer.Serialize(new
                {
                    jobTitle = session.InterviewCampaign.JDExtractedProfile?.JobTitle,
                    roleTarget = session.InterviewCampaign.JDExtractedProfile?.RoleTarget,
                    experienceLevel = session.InterviewCampaign.JDExtractedProfile?.ExperienceLevel,
                    requiredSkills = jdRequiredSkills
                }, JsonOptions),
                OverallScore = session.TechnicalFinalScore ?? provisionalResult.OverallScore,
                PerformanceBand = session.TechnicalPerformanceBand ?? string.Empty,
                MainQuestionResults = BuildFinalFeedbackMainQuestionResults(session),
                SkillResults = provisionalResult.SkillScores.Cast<object>().ToList()
            };
            var feedbackStartedAt = DateTime.UtcNow;
            AIProviderResult<TechnicalAIFinalSummaryResponse> summaryResult;
            try
            {
                summaryResult = await _providerResolver.Resolve().GenerateFinalSummaryAsync(
                    summaryRequest,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception,
                    "Technical final feedback provider failed for session {SessionId}.",
                    session.InterviewSessionId);
                var completedAt = DateTime.UtcNow;
                summaryResult = new AIProviderResult<TechnicalAIFinalSummaryResponse>
                {
                    Success = false,
                    Model = session.TechnicalAiModel ?? _options.Model,
                    ErrorCode = "PROVIDER_EXCEPTION",
                    LatencyMs = Math.Max(0, (long)(completedAt - feedbackStartedAt).TotalMilliseconds),
                    StartedAt = feedbackStartedAt,
                    CompletedAt = completedAt
                };
            }
            var valid = summaryResult.Success
                && summaryResult.Data is not null
                && !string.IsNullOrWhiteSpace(summaryResult.Data.OverallTechnicalAssessment);

            if (valid)
            {
                var data = summaryResult.Data!;
                var knowledgeGaps = CleanList(data.KnowledgeGaps);
                var recommendations = CleanList(data.RecommendationsForImprovement);
                var summary = new TechnicalFinalSummaryDto
                {
                    OverallTechnicalAssessment = data.OverallTechnicalAssessment.Trim(),
                    Summary = data.OverallTechnicalAssessment.Trim(),
                    Strengths = CleanList(data.Strengths),
                    KnowledgeGaps = knowledgeGaps,
                    AreasForImprovement = knowledgeGaps,
                    ReasoningAndApplicationAssessment = data.ReasoningAndApplicationAssessment?.Trim() ?? string.Empty,
                    CommunicationAssessment = data.CommunicationAssessment?.Trim() ?? string.Empty,
                    PerformanceBySkill = data.PerformanceBySkill
                        .Where(item => !string.IsNullOrWhiteSpace(item.Skill)
                            && !string.IsNullOrWhiteSpace(item.Assessment))
                        .Select(item => new TechnicalSkillFeedbackDto
                        {
                            Skill = item.Skill.Trim(),
                            Assessment = item.Assessment.Trim()
                        })
                        .ToList(),
                    RecommendationsForImprovement = recommendations,
                    RecommendedNextSteps = recommendations,
                    FinalTechnicalScore = session.TechnicalFinalScore ?? provisionalResult.OverallScore
                };
                session.TechnicalSummaryJson = JsonSerializer.Serialize(summary, JsonOptions);
            }
            session.TechnicalFinalFeedbackStatus = valid ? "COMPLETED" : "FAILED";
            session.TechnicalFinalFeedbackError = valid
                ? null
                : summaryResult.ErrorCode ?? "INVALID_FINAL_FEEDBACK";

            AddInteractionLog(
                session,
                null,
                AIInteractionOperationType.FinalSummary,
                TechnicalPromptVersions.Summary,
                summaryResult,
                fallbackUsed: false,
                errorCode: valid ? null : summaryResult.ErrorCode ?? "INVALID_FINAL_FEEDBACK");
            return valid;
        }

        private IReadOnlyList<object> BuildFinalFeedbackMainQuestionResults(InterviewSession session)
        {
            var plan = GetQuestionPlan(session);
            return session.TechnicalQuestionAttempts
                .Where(attempt => attempt.QuestionType == TechnicalAttemptType.Main)
                .OrderBy(attempt => attempt.MainQuestionIndex)
                .Select(root =>
                {
                    var slot = plan?.Slots.FirstOrDefault(item =>
                        item.MainQuestionIndex == root.MainQuestionIndex);
                    var attempts = session.TechnicalQuestionAttempts
                        .Where(attempt => attempt.RootMainAttemptId == root.AttemptId)
                        .OrderBy(attempt => attempt.SequenceWithinMain)
                        .Select(attempt =>
                        {
                            var evaluation = attempt.Evaluations
                                .OrderByDescending(item => item.CreatedAt)
                                .FirstOrDefault();
                            var dimensionEvaluations = evaluation is null
                                ? new List<TechnicalAIDimensionEvaluation>()
                                : Deserialize<TechnicalAIDimensionEvaluation>(
                                    evaluation.DimensionEvaluationsJson);
                            var dimensionScores = evaluation is null
                                ? new List<TechnicalDimensionScore>()
                                : Deserialize<TechnicalDimensionScore>(
                                    evaluation.ScoringBreakdownJson);
                            var evaluationByCode = dimensionEvaluations.ToDictionary(
                                item => item.RubricCode,
                                StringComparer.OrdinalIgnoreCase);

                            return new
                            {
                                type = ToApi(attempt.QuestionType),
                                question = attempt.QuestionContentSnapshot,
                                answer = attempt.AnswerTranscript,
                                questionScore = attempt.RawScore ?? evaluation?.FinalOverallScore,
                                followUpBonus = attempt.AppliedBonus,
                                criteria = dimensionScores.Select(score =>
                                {
                                    evaluationByCode.TryGetValue(score.RubricCode, out var dimension);
                                    return new
                                    {
                                        rubricCode = score.RubricCode,
                                        score = score.FinalScore,
                                        weight = score.Weight,
                                        weightedScore = score.WeightedScore,
                                        evidence = dimension?.Evidence ?? new List<string>(),
                                        missingEvidence = dimension?.MissingEvidence ?? new List<string>(),
                                        incorrectClaims = dimension?.IncorrectClaims ?? new List<string>()
                                    };
                                }).ToList(),
                                evidence = dimensionEvaluations
                                    .SelectMany(item => item.Evidence)
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .ToList(),
                                missingEvidence = dimensionEvaluations
                                    .SelectMany(item => item.MissingEvidence)
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .ToList(),
                                incorrectClaims = dimensionEvaluations
                                    .SelectMany(item => item.IncorrectClaims)
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .ToList()
                            };
                        })
                        .ToList();

                    return (object)new
                    {
                        questionId = slot?.SelectedQuestionId ?? root.QuestionId,
                        mainQuestionIndex = root.MainQuestionIndex,
                        question = root.QuestionContentSnapshot,
                        skill = root.TargetSkillSnapshot ?? root.SkillSnapshot,
                        source = (root.SourceType ?? slot?.SourceType)?.ToString().ToUpperInvariant(),
                        evaluationObjective = (root.EvaluationObjective ?? slot?.EvaluationObjective)
                            ?.ToString()
                            .ToUpperInvariant(),
                        initialMainScore = root.InitialMainScore,
                        finalQuestionScore = root.FinalMainScore,
                        cumulativeFollowUpBonus = root.CumulativeFollowUpBonus,
                        attempts
                    };
                })
                .ToList();
        }

        private TechnicalInterviewResultDto BuildResult(
            InterviewSession session,
            bool includeStoredSummary = true)
        {
            var mainAttempts = session.TechnicalQuestionAttempts
                .Where(item => item.QuestionType == TechnicalAttemptType.Main)
                .OrderBy(item => item.MainQuestionIndex)
                .ToList();
            var useAdaptiveFramework = GetQuestionPlan(session) is not null
                && session.InterviewCampaign.Mode != InterviewMode.Practice;
            var mainResults = new List<TechnicalMainQuestionResultDto>();

            foreach (var root in mainAttempts)
            {
                var finalEvaluation = session.TechnicalQuestionAttempts
                    .Where(item => item.RootMainAttemptId == root.AttemptId)
                    .SelectMany(item => item.Evaluations)
                    .SingleOrDefault(item => item.IsFinalForMainQuestion);
                if (useAdaptiveFramework && !root.FinalMainScore.HasValue)
                    continue;
                if (!useAdaptiveFramework && finalEvaluation is null)
                    continue;

                var mainEvaluation = root.Evaluations.SingleOrDefault() ?? finalEvaluation!;
                var feedbackEvaluation = finalEvaluation ?? mainEvaluation;
                var aiDimensions = Deserialize<TechnicalAIDimensionEvaluation>(mainEvaluation.DimensionEvaluationsJson);
                var scores = Deserialize<TechnicalDimensionScore>(mainEvaluation.ScoringBreakdownJson);
                var aiByCode = aiDimensions.ToDictionary(item => item.RubricCode, StringComparer.OrdinalIgnoreCase);
                var finalMainScore = root.FinalMainScore ?? feedbackEvaluation.FinalOverallScore;
                mainResults.Add(new TechnicalMainQuestionResultDto
                {
                    AttemptId = root.AttemptId,
                    QuestionId = GetQuestionPlan(session)?.Slots
                        .FirstOrDefault(slot => slot.MainQuestionIndex == root.MainQuestionIndex)
                        ?.SelectedQuestionId
                        ?? root.QuestionId,
                    MainQuestionIndex = root.MainQuestionIndex,
                    Question = root.QuestionContentSnapshot,
                    AnswerTranscript = root.AnswerTranscript,
                    Skill = root.SkillSnapshot ?? "Uncategorized",
                    Score = finalMainScore,
                    InitialMainScore = root.InitialMainScore ?? mainEvaluation.FinalOverallScore,
                    FinalMainScore = finalMainScore,
                    CumulativeFollowUpBonus = root.CumulativeFollowUpBonus,
                    SourceType = root.SourceType?.ToString().ToUpperInvariant(),
                    TargetSkill = root.TargetSkillSnapshot,
                    EvaluationObjective = root.EvaluationObjective?.ToString().ToUpperInvariant(),
                    PlanDeviation = root.PlanDeviation,
                    PlanDeviationReason = root.PlanDeviationReason,
                    Dimensions = scores.Select(score => new TechnicalDimensionResultDto
                    {
                        RubricCode = score.RubricCode,
                        Name = score.Name,
                        Score = score.FinalScore,
                        Weight = score.Weight,
                        WeightedScore = score.WeightedScore,
                        Level = score.Level,
                        Evidence = aiByCode.TryGetValue(score.RubricCode, out var ai) ? ai.Evidence : new(),
                        MissingEvidence = aiByCode.TryGetValue(score.RubricCode, out ai) ? ai.MissingEvidence : new(),
                        ReasonSummary = aiByCode.TryGetValue(score.RubricCode, out ai) ? ai.ReasonSummary : string.Empty,
                        IncorrectClaims = aiByCode.TryGetValue(score.RubricCode, out ai) ? ai.IncorrectClaims : new()
                    }).ToList(),
                    Strengths = DeserializeList(feedbackEvaluation.StrengthsJson),
                    MissingPoints = DeserializeList(feedbackEvaluation.MissingPointsJson),
                    IncorrectClaims = DeserializeList(feedbackEvaluation.IncorrectClaimsJson),
                    ImprovementSuggestions = DeserializeList(feedbackEvaluation.ImprovementSuggestionsJson),
                    FeedbackSummary = feedbackEvaluation.FeedbackSummary,
                    AdaptiveHistory = session.TechnicalQuestionAttempts
                        .Where(item => item.RootMainAttemptId == root.AttemptId
                            && item.AttemptId != root.AttemptId)
                        .OrderBy(item => item.SequenceWithinMain)
                        .Select(item => new TechnicalSubQuestionResultDto
                        {
                            AttemptId = item.AttemptId,
                            QuestionType = ToApi(item.QuestionType),
                            SequenceWithinMain = item.SequenceWithinMain,
                            Question = item.QuestionContentSnapshot,
                            AnswerTranscript = item.AnswerTranscript,
                            RawScore = item.RawScore,
                            FollowUpBonus = item.AppliedBonus,
                            GenerationReason = item.GenerationReason?.ToString().ToUpperInvariant()
                        }).ToList()
                });
            }

            var skillScores = mainResults
                .GroupBy(item => item.Skill, StringComparer.OrdinalIgnoreCase)
                .Select(group => new TechnicalSkillResultDto
                {
                    Skill = group.Key,
                    MainQuestionCount = group.Count(),
                    Score = Math.Round(group.Average(item => item.Score), 2, MidpointRounding.AwayFromZero)
                })
                .OrderBy(item => item.Skill)
                .ToList();

            var summary = includeStoredSummary && !string.IsNullOrWhiteSpace(session.TechnicalSummaryJson)
                ? JsonSerializer.Deserialize<TechnicalFinalSummaryDto>(session.TechnicalSummaryJson, JsonOptions) ?? new()
                : new TechnicalFinalSummaryDto();

            var overallScore = session.TechnicalFinalScore
                ?? (mainResults.Count == 0
                    ? 0m
                    : Math.Round(mainResults.Average(item => item.FinalMainScore), 2, MidpointRounding.AwayFromZero));
            var rubric = _rubricProvider.GetRequired(session.TechnicalRubricVersion ?? _options.RubricVersion);
            return new TechnicalInterviewResultDto
            {
                SessionId = session.InterviewSessionId,
                RubricVersion = session.TechnicalRubricVersion ?? string.Empty,
                ScoringPolicyVersion = session.TechnicalScoringPolicyVersion ?? string.Empty,
                OverallScore = overallScore,
                TechnicalScore = overallScore,
                MaxScore = rubric.MaximumScore,
                PerformanceBand = session.TechnicalPerformanceBand ?? string.Empty,
                FinalFeedbackStatus = !string.IsNullOrWhiteSpace(session.TechnicalSummaryJson)
                    ? "COMPLETED"
                    : session.TechnicalFinalFeedbackStatus,
                MainQuestions = mainResults,
                MainQuestionResults = mainResults,
                SkillScores = skillScores,
                Summary = summary,
                Strengths = summary.Strengths.Count > 0
                    ? summary.Strengths
                    : mainResults.SelectMany(item => item.Strengths).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Weaknesses = summary.AreasForImprovement.Count > 0
                    ? summary.AreasForImprovement
                    : mainResults.SelectMany(item => item.MissingPoints.Concat(item.IncorrectClaims))
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Recommendations = summary.RecommendedNextSteps.Count > 0
                    ? summary.RecommendedNextSteps
                    : mainResults.SelectMany(item => item.ImprovementSuggestions)
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };
        }

        private async Task ResetFailedEvaluationAsync(
            InterviewSession session,
            TechnicalQuestionAttempt attempt,
            CancellationToken cancellationToken)
        {
            attempt.AnswerTranscript = null;
            attempt.AudioId = null;
            attempt.SubmissionIdempotencyKey = null;
            attempt.AnsweredAt = null;
            attempt.Status = TechnicalAttemptStatus.Ready;
            session.TechnicalState = TechnicalInterviewState.QuestionReady;
            session.TechnicalConcurrencyVersion++;
            session.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<bool> TrySaveAnswerOutcomeAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                return false;
            }
            catch (DbUpdateException)
            {
                return false;
            }
        }

        private TechnicalQuestionAttempt CreateMainQuestionAttempt(
            InterviewSession session,
            TechnicalQuestionPlanSlot planSlot)
        {
            var snapshot = planSlot.LockedQuestion
                ?? throw new InvalidOperationException("Cannot activate an unlocked Main question slot.");
            var attemptId = Guid.NewGuid();
            return new TechnicalQuestionAttempt
            {
                AttemptId = attemptId,
                InterviewSessionId = session.InterviewSessionId,
                InterviewSession = session,
                // The immutable plan owns the historical QuestionId. Avoid a live FK
                // dependency so a bank hard-delete after initialization cannot prevent
                // this already-locked question from being activated.
                QuestionId = null,
                RootMainAttemptId = attemptId,
                QuestionType = TechnicalAttemptType.Main,
                QuestionContentSnapshot = snapshot.Content,
                SequenceNumber = NextSequenceNumber(session),
                MainQuestionIndex = planSlot.MainQuestionIndex,
                SequenceWithinMain = 0,
                SourceType = snapshot.SourceType,
                TargetSkillSnapshot = planSlot.TargetSkill,
                TargetSubskillSnapshot = planSlot.TargetSubskill,
                EvaluationObjective = snapshot.EvaluationObjective,
                SkillSnapshot = snapshot.Skill,
                SubskillSnapshot = snapshot.Subskill,
                DifficultySnapshot = snapshot.Difficulty,
                AdaptiveStage = TechnicalAdaptiveStage.MainQuestion,
                GenerationReason = TechnicalQuestionGenerationReason.QuestionPlan,
                PlanDeviation = false,
                PlanDeviationReason = null,
                Status = TechnicalAttemptStatus.Ready,
                CreatedAt = DateTime.UtcNow
            };
        }

        private TechnicalQuestionAttempt CreateSubQuestionAttempt(
            InterviewSession session,
            TechnicalQuestionAttempt parent,
            TechnicalQuestionAttempt root,
            TechnicalAttemptType type,
            int sourceQuestionId,
            string content,
            TechnicalQuestionGenerationReason generationReason)
        {
            return new TechnicalQuestionAttempt
            {
                AttemptId = Guid.NewGuid(),
                InterviewSessionId = session.InterviewSessionId,
                InterviewSession = session,
                QuestionId = sourceQuestionId,
                ParentAttemptId = parent.AttemptId,
                RootMainAttemptId = root.AttemptId,
                QuestionType = type,
                QuestionContentSnapshot = content,
                SequenceNumber = NextSequenceNumber(session),
                MainQuestionIndex = root.MainQuestionIndex,
                SequenceWithinMain = session.TechnicalQuestionAttempts
                    .Where(item => item.RootMainAttemptId == root.AttemptId)
                    .Select(item => item.SequenceWithinMain)
                    .DefaultIfEmpty(0)
                    .Max() + 1,
                SourceType = root.SourceType,
                TargetSkillSnapshot = root.TargetSkillSnapshot,
                TargetSubskillSnapshot = root.TargetSubskillSnapshot,
                EvaluationObjective = root.EvaluationObjective,
                SkillSnapshot = root.SkillSnapshot,
                SubskillSnapshot = root.SubskillSnapshot,
                DifficultySnapshot = root.DifficultySnapshot,
                GenerationReason = generationReason,
                Status = TechnicalAttemptStatus.Ready,
                CreatedAt = DateTime.UtcNow
            };
        }

        private TechnicalDecisionArbiterResult FinalizeWithoutBankSubQuestion(
            TechnicalDecisionArbiterResult result,
            TechnicalAnswerProcessingContext context,
            TechnicalRubricDefinition rubric,
            string? selectionErrorCode)
        {
            var baseScore = context.AttemptType switch
            {
                TechnicalAttemptType.Main => result.RawScore,
                TechnicalAttemptType.Clarification => _scoringService.ApplyClarificationRecovery(
                    result.RawScore,
                    _options.ClarificationRecoveryFactor,
                    rubric),
                _ => context.CurrentMainBaseScore
            };
            var finalScore = _scoringService.Normalize(
                baseScore + result.CumulativeFollowUpBonus,
                rubric);
            var finalDecision = context.CompletedMainQuestionCount + 1 >= context.TargetMainQuestionCount
                ? TechnicalInterviewDecision.EndInterview
                : TechnicalInterviewDecision.NextQuestion;

            return result with
            {
                Decision = finalDecision,
                FinalizeMainQuestion = true,
                NextQuestion = finalDecision == TechnicalInterviewDecision.NextQuestion
                    ? new TechnicalArbiterNextQuestion(
                        TechnicalAttemptType.Main,
                        Array.Empty<string>(),
                        TechnicalQuestionGenerationReason.QuestionPlan)
                    : null,
                QuestionStatus = TechnicalAITaskStatus.FallbackUsed,
                QuestionFallbackUsed = true,
                DecisionReason = "QUESTION_BANK_SUBQUESTION_UNAVAILABLE",
                OverrideReason = selectionErrorCode ?? "QUESTION_BANK_SUBQUESTION_UNAVAILABLE",
                FinalMainQuestionScore = finalScore,
                RequiredClarificationCount = context.RequiredClarificationCount,
                RequiredFollowUpCount = context.RequiredFollowUpCount,
                AdaptiveStage = TechnicalAdaptiveStage.Finalized
            };
        }

        private static int NextSequenceNumber(InterviewSession session)
        {
            return session.TechnicalQuestionAttempts.Count == 0
                ? 1
                : session.TechnicalQuestionAttempts.Max(item => item.SequenceNumber) + 1;
        }

        private async Task<InterviewSession?> GetOwnedSessionAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken)
        {
            return await _context.InterviewSessions
                .AsSplitQuery()
                .Include(session => session.InterviewCampaign)
                    .ThenInclude(campaign => campaign.CVExtractedProfile)
                        .ThenInclude(profile => profile.Skills)
                .Include(session => session.InterviewCampaign)
                    .ThenInclude(campaign => campaign.JDExtractedProfile)
                        .ThenInclude(profile => profile.JDFile)
                .Include(session => session.InterviewCampaign)
                    .ThenInclude(campaign => campaign.InterviewSessions.Where(item => !item.IsDeleted))
                .Include(session => session.TechnicalQuestionAttempts)
                    .ThenInclude(attempt => attempt.Question)
                .Include(session => session.TechnicalQuestionAttempts)
                    .ThenInclude(attempt => attempt.Evaluations)
                .FirstOrDefaultAsync(session =>
                    session.InterviewSessionId == sessionId
                    && session.InterviewCampaign.UserId == userId,
                    cancellationToken);
        }

        private static bool IsJdReadyForInterview(JDExtractedProfile profile)
        {
            return profile.IsConfirmed
                || profile.JDFile.Status is JDFileStatus.ConfirmationRequired
                    or JDFileStatus.Confirmed;
        }

        private static List<string> ResolveSelectedSkills(
            InterviewSession session,
            IReadOnlyCollection<string>? explicitSkills,
            IReadOnlyList<string> availableSkills)
        {
            if (explicitSkills is { Count: > 0 })
            {
                return availableSkills
                    .Where(available => explicitSkills.Any(requested =>
                        TechnicalQuestionMetadata.FuzzyMatches(available, requested)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            var campaign = session.InterviewCampaign;
            var desired = TechnicalQuestionMetadata.ParseStringArray(campaign.JDExtractedProfile.RequiredSkills)
                .Concat(TechnicalQuestionMetadata.ParseStringArray(campaign.JDExtractedProfile.NiceToHaveSkills))
                .Concat(campaign.CVExtractedProfile.Skills.Select(item => item.SkillName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var matched = availableSkills
                .Where(available => desired.Any(requested =>
                    TechnicalQuestionMetadata.FuzzyMatches(available, requested)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return matched.Count > 0 ? matched : availableSkills.ToList();
        }

        private static TechnicalQuestionAttempt? GetReadyAttempt(InterviewSession session)
        {
            return session.TechnicalQuestionAttempts
                .Where(item => item.Status == TechnicalAttemptStatus.Ready)
                .OrderByDescending(item => item.SequenceNumber)
                .FirstOrDefault();
        }

        private static TechnicalQuestionPlan? GetQuestionPlan(InterviewSession session)
        {
            return string.IsNullOrWhiteSpace(session.TechnicalQuestionPlanJson)
                ? null
                : TechnicalQuestionPlanSerializer.DeserializeRequired(session.TechnicalQuestionPlanJson);
        }

        private static TechnicalQuestionPlan? TryGetQuestionPlan(InterviewSession session)
        {
            try
            {
                return GetQuestionPlan(session);
            }
            catch (JsonException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static int GetTargetMainQuestionCount(InterviewSession session)
        {
            return TryGetQuestionPlan(session)?.TargetMainQuestionCount
                ?? (session.InterviewCampaign.Mode == InterviewMode.Practice
                    ? session.QuestionCount
                    : TechnicalQuestionPlan.RequiredSlotCount);
        }

        private static IReadOnlyList<QuestionDifficultyEnum> GetAllowedDifficulties(
            TechnicalMatchBand matchBand)
        {
            return matchBand switch
            {
                TechnicalMatchBand.Low => new[] { QuestionDifficultyEnum.Easy, QuestionDifficultyEnum.Medium },
                TechnicalMatchBand.High => new[] { QuestionDifficultyEnum.Medium, QuestionDifficultyEnum.Hard },
                _ => new[] { QuestionDifficultyEnum.Medium }
            };
        }

        private static TechnicalInterviewSessionDto MapSession(InterviewSession session)
        {
            var ready = GetReadyAttempt(session);
            var processing = session.TechnicalQuestionAttempts
                .Where(item => item.Status == TechnicalAttemptStatus.Evaluating)
                .OrderByDescending(item => item.SequenceNumber)
                .FirstOrDefault();
            var current = ready ?? processing;
            var root = current is null
                ? null
                : session.TechnicalQuestionAttempts.FirstOrDefault(item =>
                    item.AttemptId == current.RootMainAttemptId);
            var sessionStatus = session.TechnicalState.HasValue
                ? ToApi(session.TechnicalState.Value)
                : "NOT_INITIALIZED";
            var plan = TryGetQuestionPlan(session);
            return new TechnicalInterviewSessionDto
            {
                SessionId = session.InterviewSessionId,
                JobRole = session.TechnicalJobRole ?? string.Empty,
                ExperienceLevel = session.TechnicalExperienceLevel ?? string.Empty,
                Language = session.TechnicalLanguage ?? string.Empty,
                SelectedSkills = DeserializeList(session.TechnicalSelectedSkillsJson),
                TargetMainQuestionCount = GetTargetMainQuestionCount(session),
                CompletedMainQuestionCount = session.TechnicalCompletedMainQuestionCount,
                Status = sessionStatus,
                AiProvider = session.TechnicalAiProvider ?? string.Empty,
                RubricVersion = session.TechnicalRubricVersion ?? string.Empty,
                ScoringPolicyVersion = session.TechnicalScoringPolicyVersion ?? string.Empty,
                StartedAt = session.TechnicalStartedAt,
                CompletedAt = session.TechnicalCompletedAt,
                FinalScore = session.TechnicalFinalScore,
                PerformanceBand = session.TechnicalPerformanceBand,
                MatchScore = session.TechnicalMatchScoreSnapshot,
                MatchBand = session.TechnicalMatchBand?.ToString().ToUpperInvariant(),
                QuestionPlanVersion = session.TechnicalQuestionPlanVersion,
                AdaptiveRuleVersion = session.TechnicalAdaptiveRuleVersion,
                LockedMainQuestions = plan?.Slots
                    .Where(slot => slot.LockedQuestion is not null)
                    .OrderBy(slot => slot.MainQuestionIndex)
                    .Select(slot => MapLockedQuestion(slot.MainQuestionIndex, slot.LockedQuestion!))
                    .ToList() ?? new List<TechnicalLockedMainQuestionDto>(),
                AdaptiveStage = root?.AdaptiveStage?.ToString().ToUpperInvariant(),
                RecoverableFailureReason = session.TechnicalLegacyUpgradeFailureReason
                    ?? session.TechnicalReliabilityFailureReason,
                MainQuestionIndex = current?.MainQuestionIndex,
                TotalMainQuestions = GetTargetMainQuestionCount(session),
                QuestionType = current is null ? null : ToApi(current.QuestionType),
                SubQuestionIndex = current?.QuestionType == TechnicalAttemptType.Main
                    ? null
                    : current?.SequenceWithinMain,
                RequiredFollowUpCount = root?.RequiredFollowUpCount ?? 0,
                CompletedFollowUpCount = root?.CompletedFollowUpCount ?? 0,
                ProcessingStatus = processing is null
                    ? sessionStatus
                    : ToApi(processing.EvaluationTaskStatus),
                ProcessingStatuses = processing is null
                    ? null
                    : new TechnicalProcessingStatusDto
                    {
                        Evaluation = ToApi(processing.EvaluationTaskStatus),
                        QuestionGeneration = ToApi(processing.QuestionGenerationTaskStatus)
                    },
                SessionStatus = sessionStatus,
                Transcript = BuildTranscript(session.TechnicalQuestionAttempts)
            };
        }

        private sealed record TechnicalPlanLockResult(
            TechnicalQuestionPlan? Plan,
            string? ErrorCode,
            string? Message)
        {
            public bool IsSuccess => Plan is not null;
        }

        private async Task<IReadOnlyList<IReadOnlyList<int>>> GetPreviousQuestionOrdersAsync(
            int userId,
            InterviewSession currentSession,
            TechnicalQuestionPlan currentPlan,
            CancellationToken cancellationToken)
        {
            var history = await _context.InterviewSessions
                .AsNoTracking()
                .Where(session =>
                    session.InterviewSessionId != currentSession.InterviewSessionId
                    && session.InterviewCampaign.UserId == userId
                    && session.InterviewRoundType == InterviewRoundType.Technical
                    && session.TechnicalQuestionPlanJson != null)
                .OrderByDescending(session => session.TechnicalStartedAt ?? session.CreatedAt)
                .Take(100)
                .Select(session => new
                {
                    PlanJson = session.TechnicalQuestionPlanJson!,
                    session.TechnicalLanguage,
                    session.TechnicalJobRole,
                    session.TechnicalExperienceLevel,
                    session.TechnicalSelectedSkillsJson,
                    session.TechnicalMatchBand,
                    session.Difficulty,
                    session.QuestionCount,
                    session.InterviewCampaign.Mode,
                    session.InterviewCampaign.CVExtractedProfileId,
                    session.InterviewCampaign.JDExtractedProfileId
                })
                .ToListAsync(cancellationToken);

            var previousOrders = new List<IReadOnlyList<int>>();
            foreach (var item in history)
            {
                TechnicalQuestionPlan previousPlan;
                try
                {
                    previousPlan = TechnicalQuestionPlanSerializer.DeserializeRequired(item.PlanJson);
                }
                catch (JsonException)
                {
                    continue;
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                if (!previousPlan.Slots.All(slot => slot.IsLocked))
                    continue;

                var sameVersionedContext = !string.IsNullOrWhiteSpace(previousPlan.SelectionContextKey)
                    && string.Equals(
                        previousPlan.SelectionContextKey,
                        currentPlan.SelectionContextKey,
                        StringComparison.Ordinal);
                var sameLegacyContext = string.IsNullOrWhiteSpace(previousPlan.SelectionContextKey)
                    && item.CVExtractedProfileId == currentSession.InterviewCampaign.CVExtractedProfileId
                    && item.JDExtractedProfileId == currentSession.InterviewCampaign.JDExtractedProfileId
                    && item.Mode == currentSession.InterviewCampaign.Mode
                    && item.Difficulty == currentSession.Difficulty
                    && item.QuestionCount == currentPlan.TargetMainQuestionCount
                    && item.TechnicalMatchBand == currentPlan.MatchBand
                    && ContextValueEquals(
                        item.TechnicalLanguage,
                        currentSession.TechnicalLanguage)
                    && ContextValueEquals(
                        item.TechnicalJobRole,
                        currentSession.TechnicalJobRole)
                    && ContextValueEquals(
                        item.TechnicalExperienceLevel,
                        currentSession.TechnicalExperienceLevel)
                    && ContextValuesEqual(
                        DeserializeList(item.TechnicalSelectedSkillsJson),
                        DeserializeList(currentSession.TechnicalSelectedSkillsJson));
                if (!sameVersionedContext && !sameLegacyContext)
                    continue;

                previousOrders.Add(previousPlan.Slots
                    .OrderBy(slot => slot.MainQuestionIndex)
                    .Select(slot => slot.SelectedQuestionId!.Value)
                    .ToArray());
            }

            return previousOrders;
        }

        private static string BuildSelectionContextKey(
            InterviewSession session,
            TechnicalQuestionPlan plan,
            IReadOnlyCollection<string> selectedSkills)
        {
            var campaign = session.InterviewCampaign;
            var jd = campaign.JDExtractedProfile;
            var payload = JsonSerializer.Serialize(new
            {
                version = "technical-question-selection-context-v1",
                mode = campaign.Mode.ToString(),
                language = NormalizeContextValue(session.TechnicalLanguage),
                role = NormalizeContextValue(session.TechnicalJobRole),
                experience = NormalizeContextValue(session.TechnicalExperienceLevel),
                configuredDifficulty = session.Difficulty.ToString(),
                matchBand = plan.MatchBand.ToString(),
                plan.TargetMainQuestionCount,
                selectedSkills = NormalizeContextValues(selectedSkills),
                cvSkills = NormalizeContextValues(
                    campaign.CVExtractedProfile.Skills.Select(skill => skill.SkillName)),
                requiredJdSkills = NormalizeContextValues(
                    TechnicalQuestionMetadata.ParseStringArray(jd.RequiredSkills)),
                niceToHaveJdSkills = NormalizeContextValues(
                    TechnicalQuestionMetadata.ParseStringArray(jd.NiceToHaveSkills)),
                slots = plan.Slots
                    .OrderBy(slot => slot.MainQuestionIndex)
                    .Select(slot => new
                    {
                        source = slot.SourceType.ToString(),
                        skill = NormalizeContextValue(slot.TargetSkill),
                        subskill = NormalizeContextValue(slot.TargetSubskill),
                        difficulty = slot.PlannedDifficulty.ToString(),
                        objective = slot.EvaluationObjective.ToString()
                    })
                    .ToArray()
            }, JsonOptions);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        }

        private static bool ContextValueEquals(string? left, string? right) =>
            string.Equals(
                NormalizeContextValue(left),
                NormalizeContextValue(right),
                StringComparison.Ordinal);

        private static bool ContextValuesEqual(
            IEnumerable<string> left,
            IEnumerable<string> right) =>
            NormalizeContextValues(left).SequenceEqual(NormalizeContextValues(right));

        private static string NormalizeContextValue(string? value) =>
            value?.Trim().ToUpperInvariant() ?? string.Empty;

        private static string[] NormalizeContextValues(IEnumerable<string> values) =>
            values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(NormalizeContextValue)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

        private async Task<TechnicalPlanLockResult> EnsureLockedPlanAsync(
            InterviewSession session,
            CancellationToken cancellationToken)
        {
            if (_context.Database.CurrentTransaction is not null)
                return await EnsureLockedPlanCoreAsync(session, cancellationToken);

            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var result = await EnsureLockedPlanCoreAsync(session, cancellationToken);
            if (result.IsSuccess)
                await transaction.CommitAsync(cancellationToken);
            else
                await transaction.RollbackAsync(cancellationToken);
            return result;
        }

        private async Task<TechnicalPlanLockResult> EnsureLockedPlanCoreAsync(
            InterviewSession session,
            CancellationToken cancellationToken)
        {
            TechnicalQuestionPlan? existing;
            try
            {
                existing = GetQuestionPlan(session);
            }
            catch (JsonException)
            {
                return new TechnicalPlanLockResult(
                    null,
                    "LEGACY_PLAN_INVALID",
                    "The legacy Technical plan is invalid and was left unchanged.");
            }
            catch (InvalidOperationException)
            {
                return new TechnicalPlanLockResult(
                    null,
                    "LEGACY_PLAN_INVALID",
                    "The legacy Technical plan is incomplete and was left unchanged.");
            }
            if (existing is not null && existing.Slots.All(slot => slot.IsLocked))
            {
                var complete = existing.Slots.All(slot =>
                    slot.LockedQuestion is not null
                    && IsCompleteLockedSnapshot(slot.LockedQuestion)
                    && SnapshotMatchesSlot(slot.LockedQuestion, slot, existing.Version));
                return complete
                    ? new TechnicalPlanLockResult(existing, null, null)
                    : new TechnicalPlanLockResult(
                        null,
                        "LEGACY_PLAN_INVALID",
                        "The locked Technical plan contains an incomplete or mismatched Main snapshot and was left unchanged.");
            }
            if (session.TechnicalState == TechnicalInterviewState.Completed)
            {
                return new TechnicalPlanLockResult(
                    null,
                    "COMPLETED_LEGACY_SESSION_NOT_UPGRADED",
                    "Completed legacy sessions are preserved and are never re-scored.");
            }

            var selectedSkills = DeserializeList(session.TechnicalSelectedSkillsJson);
            if (selectedSkills.Count == 0)
            {
                return new TechnicalPlanLockResult(
                    null,
                    "LEGACY_SELECTED_SKILLS_UNAVAILABLE",
                    "The legacy session does not contain enough information to lock remaining Main questions.");
            }

            TechnicalQuestionPlan plan;
            if (existing is not null)
            {
                plan = existing;
            }
            else if (session.InterviewCampaign.Mode == InterviewMode.Practice)
            {
                plan = BuildPracticePlan(session, selectedSkills);
            }
            else
            {
                var build = _questionPlanBuilder.Build(new TechnicalQuestionPlanRequest(
                    session.TechnicalMatchScoreSnapshot ?? session.InterviewCampaign.CvJdMatchScore ?? 0,
                    session.InterviewCampaign.CVExtractedProfile.Skills.Select(item => item.SkillName).ToList(),
                    TechnicalQuestionMetadata.ParseStringArray(session.InterviewCampaign.JDExtractedProfile.RequiredSkills),
                    TechnicalQuestionMetadata.ParseStringArray(session.InterviewCampaign.JDExtractedProfile.NiceToHaveSkills),
                    selectedSkills,
                    session.TechnicalQuestionPlanVersion ?? _options.QuestionPlanVersion));
                if (!build.IsSuccess)
                {
                    return new TechnicalPlanLockResult(null, build.ErrorCode, build.Message);
                }
                plan = build.Plan!;
            }

            var rubric = _rubricProvider.GetRequired(session.TechnicalRubricVersion
                ?? (session.InterviewCampaign.Mode == InterviewMode.Practice
                    ? _options.PracticeRubricVersion
                    : _options.RubricVersion));
            var locked = await LockQuestionPlanAsync(
                session,
                plan,
                rubric,
                preserveLegacyAttempts: true,
                cancellationToken);
            if (!locked.IsSuccess)
                return locked;

            session.TechnicalQuestionPlanJson = TechnicalQuestionPlanSerializer.Serialize(locked.Plan!);
            session.TechnicalQuestionPlanVersion = locked.Plan!.Version;
            session.TechnicalLegacyUpgradeFailureReason = null;
            session.TechnicalConcurrencyVersion++;
            session.UpdatedAt = DateTime.UtcNow;
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return locked;
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                return new TechnicalPlanLockResult(
                    null,
                    "LEGACY_PLAN_UPGRADE_CONFLICT",
                    "The legacy session changed while its locked plan was being persisted.");
            }
        }

        private async Task RecordLegacyUpgradeFailureAsync(
            InterviewSession session,
            TechnicalPlanLockResult failure,
            CancellationToken cancellationToken)
        {
            if (string.Equals(failure.ErrorCode, "LEGACY_PLAN_UPGRADE_CONFLICT", StringComparison.Ordinal))
                return;
            var value = $"{failure.ErrorCode ?? "LEGACY_PLAN_UPGRADE_FAILED"}: {failure.Message}";
            session.TechnicalLegacyUpgradeFailureReason = value.Length <= 500
                ? value
                : value[..500];
            session.TechnicalConcurrencyVersion++;
            session.UpdatedAt = DateTime.UtcNow;
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // The recoverable error is still returned to the caller. Never mutate
                // plan/attempt history merely to force audit metadata through a conflict.
            }
        }

        private TechnicalQuestionPlan BuildPracticePlan(
            InterviewSession session,
            IReadOnlyList<string> selectedSkills)
        {
            var cvSkills = session.InterviewCampaign.CVExtractedProfile.Skills
                .Select(item => item.SkillName)
                .ToList();
            var slots = Enumerable.Range(1, session.QuestionCount)
                .Select(index =>
                {
                    var skill = selectedSkills[(index - 1) % selectedSkills.Count];
                    var source = cvSkills.Any(item => TechnicalQuestionMetadata.FuzzyMatches(item, skill))
                        ? TechnicalQuestionSourceType.CV
                        : TechnicalQuestionSourceType.JD;
                    return new TechnicalQuestionPlanSlot(
                        index,
                        source,
                        skill,
                        null,
                        session.Difficulty,
                        source == TechnicalQuestionSourceType.CV
                            ? TechnicalEvaluationObjective.CvSkillVerification
                            : TechnicalEvaluationObjective.JdCoreKnowledge);
                })
                .ToImmutableArray();
            return new TechnicalQuestionPlan(
                session.InterviewCampaign.CvJdMatchScore ?? 0,
                TechnicalMatchBand.Medium,
                slots.Count(item => item.SourceType == TechnicalQuestionSourceType.CV),
                slots.Count(item => item.SourceType == TechnicalQuestionSourceType.JD),
                $"{_options.QuestionPlanVersion}-practice",
                slots,
                session.QuestionCount);
        }

        private async Task<TechnicalPlanLockResult> LockQuestionPlanAsync(
            InterviewSession session,
            TechnicalQuestionPlan plan,
            TechnicalRubricDefinition rubric,
            bool preserveLegacyAttempts,
            CancellationToken cancellationToken)
        {
            var lockedAt = DateTime.UtcNow;
            var locked = new List<TechnicalQuestionPlanSlot>(plan.TargetMainQuestionCount);
            var usedQuestionIds = new HashSet<int>();
            var usedLegacyAttemptIds = new HashSet<Guid>();
            var legacyIndexAssignments = new Dictionary<TechnicalQuestionAttempt, int>();
            var reservedQuestionIds = plan.Slots
                .Where(item => item.SelectedQuestionId.HasValue)
                .Select(item => item.SelectedQuestionId!.Value)
                .ToHashSet();
            var legacyMains = session.TechnicalQuestionAttempts
                .Where(item => item.QuestionType == TechnicalAttemptType.Main)
                .OrderBy(item => item.MainQuestionIndex)
                .ThenBy(item => item.SequenceNumber)
                .ToList();
            foreach (var reserved in reservedQuestionIds)
            {
                var matchingAttempt = legacyMains.FirstOrDefault(item => item.QuestionId == reserved);
                if (matchingAttempt is not null)
                    usedLegacyAttemptIds.Add(matchingAttempt.AttemptId);
            }

            foreach (var slot in plan.Slots.OrderBy(item => item.MainQuestionIndex))
            {
                TechnicalLockedMainQuestionSnapshot? snapshot = slot.LockedQuestion;
                var legacyAttempt = preserveLegacyAttempts
                    ? legacyMains.FirstOrDefault(item =>
                        item.MainQuestionIndex == slot.MainQuestionIndex
                        && !usedLegacyAttemptIds.Contains(item.AttemptId))
                        ?? legacyMains.FirstOrDefault(item => !usedLegacyAttemptIds.Contains(item.AttemptId))
                    : null;
                if (snapshot is null && legacyAttempt is not null)
                {
                    usedLegacyAttemptIds.Add(legacyAttempt.AttemptId);
                    legacyIndexAssignments[legacyAttempt] = slot.MainQuestionIndex;
                    var question = legacyAttempt.Question;
                    if (question is null)
                    {
                        return new TechnicalPlanLockResult(
                            null,
                            "LEGACY_SNAPSHOT_METADATA_UNAVAILABLE",
                            $"Main question {slot.MainQuestionIndex} has no recoverable scoring metadata.");
                    }
                    snapshot = CreateLockedSnapshot(
                        slot,
                        question,
                        rubric,
                        lockedAt,
                        session.TechnicalLanguage ?? session.InterviewCampaign.Language,
                        plan.Version,
                        legacyAttempt.QuestionContentSnapshot);
                }

                if (snapshot is null)
                {
                    var selectionContext = BuildLockedSelectionContext(session, plan, slot, locked);
                    var pool = await _selectionService.PreparePoolAsync(selectionContext, cancellationToken);
                    var question = pool.Candidates.FirstOrDefault(item =>
                        !usedQuestionIds.Contains(item.QuestionId)
                        && !reservedQuestionIds.Contains(item.QuestionId));
                    if (question is null)
                    {
                        return new TechnicalPlanLockResult(
                            null,
                            pool.ErrorCode ?? "NO_PLAN_SLOT_CANDIDATE",
                            $"No unique Technical question can be locked for Main slot {slot.MainQuestionIndex} "
                            + $"(source={slot.SourceType}, skill={slot.TargetSkill}, difficulty={slot.PlannedDifficulty}, "
                            + $"role={session.TechnicalJobRole}, experience={session.TechnicalExperienceLevel}, "
                            + $"language={session.TechnicalLanguage}, relaxation={pool.Relaxation}).");
                    }

                    if (!TechnicalQuestionMetadata.FuzzyMatches(question.Skill ?? string.Empty, slot.TargetSkill)
                        || question.Difficulty != slot.PlannedDifficulty)
                    {
                        return new TechnicalPlanLockResult(
                            null,
                            "LOCKED_QUESTION_PLAN_MISMATCH",
                            $"The candidate for Main slot {slot.MainQuestionIndex} does not satisfy its locked skill and difficulty.");
                    }
                    snapshot = CreateLockedSnapshot(
                        slot,
                        question,
                        rubric,
                        lockedAt,
                        session.TechnicalLanguage ?? session.InterviewCampaign.Language,
                        plan.Version);
                }

                if (!IsCompleteLockedSnapshot(snapshot)
                    || !SnapshotMatchesSlot(snapshot, slot, plan.Version)
                    || !usedQuestionIds.Add(snapshot.SelectedQuestionId))
                {
                    return new TechnicalPlanLockResult(
                        null,
                        "INVALID_LOCKED_QUESTION_SET",
                        "Locked main questions must be unique and contain complete content and scoring metadata.");
                }
                locked.Add(slot with { LockedQuestion = snapshot });
            }

            if (locked.Count != plan.TargetMainQuestionCount
                || locked.Select(item => item.MainQuestionIndex).Distinct().Count() != plan.TargetMainQuestionCount
                || usedQuestionIds.Count != plan.TargetMainQuestionCount)
            {
                return new TechnicalPlanLockResult(null, "INVALID_LOCKED_QUESTION_SET", "The complete locked main-question set is invalid.");
            }

            foreach (var assignment in legacyIndexAssignments)
                assignment.Key.MainQuestionIndex = assignment.Value;

            return new TechnicalPlanLockResult(
                plan with { Slots = locked.ToImmutableArray() },
                null,
                null);
        }

        private TechnicalSelectionContext BuildLockedSelectionContext(
            InterviewSession session,
            TechnicalQuestionPlan plan,
            TechnicalQuestionPlanSlot slot,
            IReadOnlyList<TechnicalQuestionPlanSlot> locked)
        {
            var cvSkills = session.InterviewCampaign.CVExtractedProfile.Skills
                .Select(item => item.SkillName)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();
            var requiredJdSkills = TechnicalQuestionMetadata.ParseStringArray(
                session.InterviewCampaign.JDExtractedProfile.RequiredSkills);
            var jdSkills = requiredJdSkills.Concat(TechnicalQuestionMetadata.ParseStringArray(
                    session.InterviewCampaign.JDExtractedProfile.NiceToHaveSkills))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new TechnicalSelectionContext
            {
                Language = session.TechnicalLanguage ?? session.InterviewCampaign.Language,
                JobRole = session.TechnicalJobRole ?? session.InterviewCampaign.JDExtractedProfile.RoleTarget ?? string.Empty,
                ExperienceLevel = session.TechnicalExperienceLevel ?? string.Empty,
                Difficulty = slot.PlannedDifficulty,
                SelectedSkills = DeserializeList(session.TechnicalSelectedSkillsJson),
                AskedQuestionIds = locked.Where(item => item.SelectedQuestionId.HasValue)
                    .Select(item => item.SelectedQuestionId!.Value)
                    .ToHashSet(),
                SkillUsage = session.InterviewCampaign.Mode == InterviewMode.Practice
                    ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    : locked.Where(item => item.LockedQuestion is not null)
                        .GroupBy(item => item.LockedQuestion!.Skill, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
                AskedSubskills = locked.Where(item => !string.IsNullOrWhiteSpace(item.LockedQuestion?.Subskill))
                    .Select(item => item.LockedQuestion!.Subskill!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                PlanSlot = slot,
                CvSkills = cvSkills,
                JdSkills = jdSkills,
                RequiredJdSkills = requiredJdSkills,
                AllowedDifficulties = session.InterviewCampaign.Mode == InterviewMode.RealTest
                    ? new[] { slot.PlannedDifficulty }
                    : GetAllowedDifficulties(plan.MatchBand)
            };
        }

        private TechnicalLockedMainQuestionSnapshot CreateLockedSnapshot(
            TechnicalQuestionPlanSlot slot,
            Question question,
            TechnicalRubricDefinition rubric,
            DateTime lockedAt,
            string language,
            string questionPlanVersion,
            string? contentOverride = null)
        {
            return new TechnicalLockedMainQuestionSnapshot(
                question.QuestionId,
                contentOverride ?? question.QuestionContent,
                question.SuggestedAnswer,
                question.ExpectedKeyPoints ?? string.Empty,
                question.ScoringRubric ?? string.Empty,
                JsonSerializer.Serialize(new
                {
                    rubric.Version,
                    rubric.Dimensions,
                    rubric.Levels,
                    rubric.Limits
                }, JsonOptions),
                JsonSerializer.Serialize(new
                {
                    rubric.ScoringPolicyVersion,
                    question.TimeLimitSeconds
                }, JsonOptions),
                question.Skill ?? slot.TargetSkill,
                TechnicalQuestionMetadata.GetSubskill(question.QdrantPayloadJson) ?? slot.TargetSubskill,
                question.Difficulty,
                slot.SourceType,
                slot.EvaluationObjective,
                question.Language ?? language,
                questionPlanVersion,
                (question.UpdatedAt ?? question.CreatedAt).ToUniversalTime().ToString("O"),
                lockedAt,
                question.ClarificationQuestion,
                question.FollowUp1,
                question.FollowUp2);
        }

        private static bool IsCompleteLockedSnapshot(TechnicalLockedMainQuestionSnapshot snapshot)
        {
            return snapshot.SelectedQuestionId > 0
                && !string.IsNullOrWhiteSpace(snapshot.Content)
                && !string.IsNullOrWhiteSpace(snapshot.ExpectedAnswer)
                && !string.IsNullOrWhiteSpace(snapshot.ExpectedKeyPoints)
                && !string.IsNullOrWhiteSpace(snapshot.QuestionSpecificRubric)
                && !string.IsNullOrWhiteSpace(snapshot.RubricMetadataJson)
                && !string.IsNullOrWhiteSpace(snapshot.ScoringMetadataJson)
                && !string.IsNullOrWhiteSpace(snapshot.Skill)
                && !string.IsNullOrWhiteSpace(snapshot.Language)
                && !string.IsNullOrWhiteSpace(snapshot.QuestionPlanVersion);
        }

        private static bool SnapshotMatchesSlot(
            TechnicalLockedMainQuestionSnapshot snapshot,
            TechnicalQuestionPlanSlot slot,
            string planVersion)
        {
            return snapshot.SourceType == slot.SourceType
                && snapshot.EvaluationObjective == slot.EvaluationObjective
                && snapshot.Difficulty == slot.PlannedDifficulty
                && string.Equals(snapshot.QuestionPlanVersion, planVersion, StringComparison.Ordinal)
                && TechnicalQuestionMetadata.FuzzyMatches(snapshot.Skill, slot.TargetSkill)
                && (string.IsNullOrWhiteSpace(slot.TargetSubskill)
                    || TechnicalQuestionMetadata.FuzzyMatches(snapshot.Subskill ?? string.Empty, slot.TargetSubskill));
        }

        private static List<TechnicalTranscriptEntryDto> BuildTranscript(
            IEnumerable<TechnicalQuestionAttempt> attempts)
        {
            return attempts
                .OrderBy(attempt => attempt.SequenceNumber)
                .SelectMany(attempt =>
                {
                    var questionStatus = string.IsNullOrWhiteSpace(attempt.AnswerTranscript)
                        ? "CURRENT"
                        : "FINAL";
                    var entries = new List<TechnicalTranscriptEntryDto>
                    {
                        new()
                        {
                            Id = $"{attempt.AttemptId}:question",
                            AttemptId = attempt.AttemptId,
                            Role = "INTERVIEWER",
                            Content = attempt.QuestionContentSnapshot,
                            QuestionType = ToApi(attempt.QuestionType),
                            Status = questionStatus,
                            CreatedAt = attempt.CreatedAt
                        }
                    };

                    if (!string.IsNullOrWhiteSpace(attempt.AnswerTranscript))
                    {
                        entries.Add(new TechnicalTranscriptEntryDto
                        {
                            Id = $"{attempt.AttemptId}:answer",
                            AttemptId = attempt.AttemptId,
                            Role = "CANDIDATE",
                            Content = attempt.AnswerTranscript,
                            QuestionType = ToApi(attempt.QuestionType),
                            Status = attempt.Status == TechnicalAttemptStatus.Evaluating
                                ? "PROCESSING"
                                : "FINAL",
                            CreatedAt = attempt.AnsweredAt ?? attempt.CreatedAt
                        });
                    }

                    return entries;
                })
                .ToList();
        }

        private async Task<bool> EnsureLifecycleCompletionAsync(int userId, InterviewSession session)
        {
            if (session.Status == InterviewSessionStatus.Completed
                && session.InterviewCampaign.Status is InterviewCampaignStatus.Completed
                    or InterviewCampaignStatus.Cancelled
                    or InterviewCampaignStatus.Expired)
            {
                return true;
            }

            if (session.Status != InterviewSessionStatus.Active
                && session.Status != InterviewSessionStatus.Completed)
            {
                return false;
            }

            var lifecycleResult = await _sessionLifecycleService.CompleteSessionAsync(
                userId,
                session.InterviewSessionId);
            if (lifecycleResult.Success)
            {
                return true;
            }

            _logger.LogWarning(
                "Technical session {SessionId} completed, but generic session lifecycle completion was rejected: {Error}",
                session.InterviewSessionId,
                lifecycleResult.ErrorMessage);
            return false;
        }

        private static bool IsLifecycleClosed(InterviewSession session) =>
            session.Status == InterviewSessionStatus.Completed
            || session.Status == InterviewSessionStatus.Cancelled;

        private async Task<bool> IsLifecycleClosedInDatabaseAsync(
            int sessionId,
            CancellationToken cancellationToken)
        {
            var status = await _context.InterviewSessions
                .AsNoTracking()
                .Where(session => session.InterviewSessionId == sessionId)
                .Select(session => (InterviewSessionStatus?)session.Status)
                .SingleOrDefaultAsync(cancellationToken);
            return status == InterviewSessionStatus.Completed
                || status == InterviewSessionStatus.Cancelled;
        }

        private static TechnicalCurrentQuestionDto MapCurrentQuestion(
            InterviewSession session,
            TechnicalQuestionAttempt attempt)
        {
            var root = session.TechnicalQuestionAttempts.FirstOrDefault(item =>
                item.AttemptId == attempt.RootMainAttemptId) ?? attempt;
            var requiredSubQuestions = root.RequiredClarificationCount + root.RequiredFollowUpCount;
            var lockedQuestionId = GetQuestionPlan(session)?.Slots
                .FirstOrDefault(slot => slot.MainQuestionIndex == root.MainQuestionIndex)
                ?.SelectedQuestionId
                ?? root.QuestionId;
            var lockedSlot = TryGetQuestionPlan(session)?.Slots.FirstOrDefault(slot =>
                slot.MainQuestionIndex == root.MainQuestionIndex);
            if (attempt.GenerationReason == TechnicalQuestionGenerationReason.ReliabilityMinimum)
            {
                requiredSubQuestions++;
            }
            return new TechnicalCurrentQuestionDto
            {
                AttemptId = attempt.AttemptId,
                QuestionId = attempt.QuestionType == TechnicalAttemptType.Main
                    ? lockedQuestionId
                    : attempt.QuestionId,
                SelectedQuestionId = lockedQuestionId,
                LockedQuestionSnapshot = lockedSlot?.LockedQuestion is null
                    ? null
                    : MapLockedQuestion(root.MainQuestionIndex, lockedSlot.LockedQuestion),
                QuestionType = ToApi(attempt.QuestionType),
                Content = attempt.QuestionContentSnapshot,
                Skill = attempt.SkillSnapshot,
                Difficulty = attempt.DifficultySnapshot?.ToString(),
                MainQuestionIndex = attempt.MainQuestionIndex,
                TotalMainQuestions = GetTargetMainQuestionCount(session),
                SessionStatus = session.TechnicalState.HasValue ? ToApi(session.TechnicalState.Value) : "NOT_INITIALIZED",
                SubQuestionIndex = attempt.QuestionType == TechnicalAttemptType.Main
                    ? null
                    : attempt.SequenceWithinMain,
                RequiredFollowUpCount = root.RequiredFollowUpCount,
                CompletedFollowUpCount = root.CompletedFollowUpCount,
                RequiredSubQuestionCount = requiredSubQuestions,
                ProcessingStatus = session.TechnicalState.HasValue
                    ? ToApi(session.TechnicalState.Value)
                    : "NOT_INITIALIZED"
            };
        }

        private static TechnicalSubmitAnswerResponseDto BuildSubmitResponse(
            InterviewSession session,
            TechnicalQuestionAttempt attempt,
            TechnicalInterviewDecision decision)
        {
            var next = GetReadyAttempt(session);
            var progressAttempt = next ?? attempt;
            var progressRoot = session.TechnicalQuestionAttempts.FirstOrDefault(item =>
                item.AttemptId == progressAttempt.RootMainAttemptId) ?? progressAttempt;
            var storedEvaluation = attempt.Evaluations.SingleOrDefault();
            var resolvedAction = storedEvaluation is null
                && attempt.Status == TechnicalAttemptStatus.Evaluating
                ? "PROCESSING"
                : GetQuestionPlan(session) is null
                    ? ToLegacyApi(decision)
                    : ToApi(decision);
            return new TechnicalSubmitAnswerResponseDto
            {
                AttemptId = attempt.AttemptId,
                Processing = new TechnicalProcessingStatusDto
                {
                    Evaluation = ToApi(attempt.EvaluationTaskStatus),
                    QuestionGeneration = ToApi(attempt.QuestionGenerationTaskStatus)
                },
                Evaluation = new TechnicalEvaluationDecisionDto { Decision = resolvedAction },
                NextQuestion = next is null ? null : MapCurrentQuestion(session, next),
                SessionStatus = session.TechnicalState.HasValue ? ToApi(session.TechnicalState.Value) : "NOT_INITIALIZED",
                Fallbacks = new TechnicalFallbackStatusDto
                {
                    EvaluationFallbackUsed = attempt.EvaluationFallbackUsed,
                    QuestionFallbackUsed = attempt.QuestionFallbackUsed
                },
                ResolvedAction = resolvedAction,
                AiSuggestedAction = storedEvaluation?.AiSuggestedAction is { } aiAction
                    ? ToApi(aiAction)
                    : null,
                BackendResolvedAction = resolvedAction,
                OverrideReason = storedEvaluation?.OverrideReason,
                AdaptiveStage = progressRoot.AdaptiveStage?.ToString().ToUpperInvariant(),
                FallbackUsed = attempt.EvaluationFallbackUsed
                    || attempt.QuestionFallbackUsed,
                Progress = new TechnicalInterviewProgressDto
                {
                    MainQuestionIndex = progressAttempt.MainQuestionIndex,
                    TotalMainQuestions = GetTargetMainQuestionCount(session),
                    SubQuestionIndex = progressAttempt.QuestionType == TechnicalAttemptType.Main
                        ? null
                        : progressAttempt.SequenceWithinMain,
                    RequiredSubQuestionCount = progressRoot.RequiredClarificationCount
                        + progressRoot.RequiredFollowUpCount
                        + (progressAttempt.GenerationReason == TechnicalQuestionGenerationReason.ReliabilityMinimum ? 1 : 0),
                    RequiredFollowUpCount = progressRoot.RequiredFollowUpCount,
                    CompletedFollowUpCount = progressRoot.CompletedFollowUpCount
                }
            };
        }

        private static TechnicalLockedMainQuestionDto MapLockedQuestion(
            int mainQuestionIndex,
            TechnicalLockedMainQuestionSnapshot snapshot)
        {
            return new TechnicalLockedMainQuestionDto
            {
                MainQuestionIndex = mainQuestionIndex,
                SelectedQuestionId = snapshot.SelectedQuestionId,
                Skill = snapshot.Skill,
                Subskill = snapshot.Subskill,
                Difficulty = snapshot.Difficulty.ToString().ToUpperInvariant(),
                SourceType = snapshot.SourceType.ToString().ToUpperInvariant(),
                EvaluationObjective = snapshot.EvaluationObjective.ToString().ToUpperInvariant(),
                Language = snapshot.Language,
                QuestionPlanVersion = snapshot.QuestionPlanVersion,
                QuestionBankVersion = snapshot.QuestionBankVersion,
                LockedAt = snapshot.LockedAt
            };
        }

        private static void ApplyProcessingOutcome(
            TechnicalQuestionAttempt attempt,
            TechnicalAnswerEvaluationProcessingResult results,
            TechnicalDecisionArbiterResult arbiterResult)
        {
            attempt.EvaluationTaskStatus = arbiterResult.EvaluationStatus;
            attempt.FeedbackTaskStatus = TechnicalAITaskStatus.NotStarted;
            attempt.QuestionGenerationTaskStatus = arbiterResult.QuestionStatus;
            attempt.EvaluationFallbackUsed = arbiterResult.EvaluationFallbackUsed;
            attempt.FeedbackFallbackUsed = false;
            attempt.QuestionFallbackUsed = arbiterResult.QuestionFallbackUsed;
            attempt.CriticalPathLatencyMs = arbiterResult.CriticalPathLatencyMs;
            attempt.SequentialEstimatedLatencyMs = results.Metrics.SequentialEstimatedLatencyMs;
            attempt.ParallelLatencySavingMs = results.Metrics.ParallelLatencySavingMs;
            attempt.ProcessingCompletedAt = DateTime.UtcNow;
            attempt.TotalProcessingLatencyMs = attempt.ProcessingStartedAt.HasValue
                ? Math.Max(0, (long)(attempt.ProcessingCompletedAt.Value - attempt.ProcessingStartedAt.Value).TotalMilliseconds)
                : results.Metrics.TotalProcessingLatencyMs;
        }

        private void AddEvaluationInteractionLog(
            InterviewSession session,
            Guid attemptId,
            TechnicalAnswerEvaluationProcessingResult results,
            TechnicalDecisionArbiterResult arbiterResult)
        {
            AddTaskInteractionLog(
                session,
                attemptId,
                AIInteractionOperationType.AnswerEvaluation,
                TechnicalPromptVersions.Evaluation,
                results.Evaluation,
                arbiterResult.EvaluationStatus,
                arbiterResult.EvaluationFallbackUsed,
                arbiterResult.IsSuccess ? null : arbiterResult.ErrorCode);
        }

        private void AddTaskInteractionLog<T>(
            InterviewSession session,
            Guid attemptId,
            AIInteractionOperationType operation,
            string promptVersion,
            TechnicalAITaskOutcome<T> outcome,
            TechnicalAITaskStatus finalStatus,
            bool fallbackUsed,
            string? finalErrorCode)
        {
            var result = outcome.ProviderResult;
            _context.AIInteractionLogs.Add(new AIInteractionLog
            {
                Provider = session.TechnicalAiProvider ?? _options.Provider,
                Model = result?.Model ?? session.TechnicalAiModel ?? _options.Model,
                OperationType = operation,
                PromptVersion = promptVersion,
                RubricVersion = session.TechnicalRubricVersion ?? _options.RubricVersion,
                LatencyMs = outcome.LatencyMs,
                RetryCount = result?.RetryCount ?? 0,
                InputTokenCount = result?.InputTokens,
                OutputTokenCount = result?.OutputTokens,
                EstimatedCost = EstimateCost(result?.InputTokens, result?.OutputTokens),
                Status = ToLogStatus(finalStatus),
                ErrorCode = finalErrorCode
                    ?? outcome.ErrorCode
                    ?? (finalStatus == TechnicalAITaskStatus.InvalidOutput
                        ? "INVALID_OUTPUT"
                        : fallbackUsed
                            ? "FALLBACK_VALIDATION_OR_PROVIDER_FAILURE"
                            : null),
                FallbackUsed = fallbackUsed,
                InterviewSessionId = session.InterviewSessionId,
                AttemptId = attemptId,
                StartedAt = outcome.StartedAt,
                CompletedAt = outcome.CompletedAt,
                CreatedAt = DateTime.UtcNow
            });
        }

        private void AddInteractionLog<T>(
            InterviewSession session,
            Guid? attemptId,
            AIInteractionOperationType operation,
            string promptVersion,
            AIProviderResult<T> result,
            bool fallbackUsed,
            string? errorCode)
        {
            _context.AIInteractionLogs.Add(new AIInteractionLog
            {
                Provider = session.TechnicalAiProvider ?? _options.Provider,
                Model = result.Model,
                OperationType = operation,
                PromptVersion = promptVersion,
                RubricVersion = session.TechnicalRubricVersion ?? _options.RubricVersion,
                LatencyMs = result.LatencyMs,
                RetryCount = result.RetryCount,
                InputTokenCount = result.InputTokens,
                OutputTokenCount = result.OutputTokens,
                EstimatedCost = EstimateCost(result.InputTokens, result.OutputTokens),
                Status = fallbackUsed
                    ? AIInteractionStatus.FallbackUsed
                    : result.Success && errorCode is null
                        ? AIInteractionStatus.Succeeded
                        : string.Equals(errorCode, "TIMEOUT", StringComparison.Ordinal)
                            ? AIInteractionStatus.Timeout
                            : string.Equals(errorCode, "MALFORMED_JSON", StringComparison.Ordinal)
                                ? AIInteractionStatus.InvalidOutput
                                : AIInteractionStatus.Failed,
                ErrorCode = errorCode,
                FallbackUsed = fallbackUsed,
                InterviewSessionId = session.InterviewSessionId,
                AttemptId = attemptId,
                StartedAt = result.StartedAt,
                CompletedAt = result.CompletedAt,
                CreatedAt = DateTime.UtcNow
            });
        }

        private decimal EstimateCost(int? inputTokens, int? outputTokens)
        {
            var inputCost = (inputTokens ?? 0) * _options.InputTokenCostPerMillion;
            var outputCost = (outputTokens ?? 0) * _options.OutputTokenCostPerMillion;
            return Math.Round((inputCost + outputCost) / 1_000_000m, 8);
        }

        private static AIInteractionStatus ToLogStatus(TechnicalAITaskStatus status) => status switch
        {
            TechnicalAITaskStatus.Fulfilled => AIInteractionStatus.Succeeded,
            TechnicalAITaskStatus.Timeout => AIInteractionStatus.Timeout,
            TechnicalAITaskStatus.InvalidOutput => AIInteractionStatus.InvalidOutput,
            TechnicalAITaskStatus.FallbackUsed => AIInteractionStatus.FallbackUsed,
            _ => AIInteractionStatus.Failed
        };

        private static TechnicalFinalSummaryDto BuildDeterministicSummary(
            TechnicalInterviewResultDto result,
            string performanceBand,
            decimal maximumScore)
        {
            var strongest = result.SkillScores.OrderByDescending(item => item.Score).FirstOrDefault();
            var weakest = result.SkillScores.OrderBy(item => item.Score).FirstOrDefault();
            return new TechnicalFinalSummaryDto
            {
                Summary = $"Technical score {result.OverallScore:0.00}/{maximumScore:0.00}, performance band: {performanceBand}.",
                Strengths = strongest is null
                    ? new List<string>()
                    : new List<string> { $"Highest assessed skill: {strongest.Skill} ({strongest.Score:0.00}/{maximumScore:0.00})." },
                AreasForImprovement = weakest is null
                    ? new List<string>()
                    : new List<string> { $"Prioritize improvement in {weakest.Skill} ({weakest.Score:0.00}/{maximumScore:0.00})." },
                RecommendedNextSteps = new List<string> { "Review missing evidence and improvement suggestions for each main question." }
            };
        }

        private static List<string> CleanList(IEnumerable<string>? values)
        {
            return values?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList()
                ?? new List<string>();
        }

        private static string SerializeList(IEnumerable<string> values)
        {
            return JsonSerializer.Serialize(CleanList(values), JsonOptions);
        }

        private static List<string> DeserializeList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<string>();
            try
            {
                return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>();
            }
            catch (JsonException)
            {
                return new List<string>();
            }
        }

        private static List<T> Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
        }

        private static string ToApi(TechnicalInterviewState value) => value switch
        {
            TechnicalInterviewState.Created => "CREATED",
            TechnicalInterviewState.SelectingQuestion => "SELECTING_QUESTION",
            TechnicalInterviewState.QuestionReady => "QUESTION_READY",
            TechnicalInterviewState.Answering => "ANSWERING",
            TechnicalInterviewState.Evaluating => "EVALUATING",
            TechnicalInterviewState.Completed => "COMPLETED",
            TechnicalInterviewState.Failed => "FAILED",
            _ => value.ToString().ToUpperInvariant()
        };

        private static string ToApi(TechnicalAttemptType value) => value switch
        {
            TechnicalAttemptType.Main => "MAIN",
            TechnicalAttemptType.Clarification => "CLARIFICATION",
            TechnicalAttemptType.FollowUp => "FOLLOW_UP",
            _ => value.ToString().ToUpperInvariant()
        };

        private static string ToApi(TechnicalInterviewDecision value) => value switch
        {
            TechnicalInterviewDecision.Clarification => "CLARIFICATION",
            TechnicalInterviewDecision.FollowUp => "FOLLOW_UP",
            TechnicalInterviewDecision.NextQuestion => "NEXT_MAIN_QUESTION",
            TechnicalInterviewDecision.EndInterview => "COMPLETE",
            _ => value.ToString().ToUpperInvariant()
        };

        private static string ToLegacyApi(TechnicalInterviewDecision value) => value switch
        {
            TechnicalInterviewDecision.Clarification => "CLARIFICATION",
            TechnicalInterviewDecision.FollowUp => "FOLLOW_UP",
            TechnicalInterviewDecision.NextQuestion => "NEXT_QUESTION",
            TechnicalInterviewDecision.EndInterview => "END_INTERVIEW",
            _ => value.ToString().ToUpperInvariant()
        };

        private static string ToApi(TechnicalAITaskStatus value) => value switch
        {
            TechnicalAITaskStatus.NotStarted => "NOT_STARTED",
            TechnicalAITaskStatus.Processing => "PROCESSING",
            TechnicalAITaskStatus.Fulfilled => "COMPLETED",
            TechnicalAITaskStatus.Rejected => "REJECTED",
            TechnicalAITaskStatus.Timeout => "TIMEOUT",
            TechnicalAITaskStatus.InvalidOutput => "INVALID_OUTPUT",
            TechnicalAITaskStatus.FallbackUsed => "FALLBACK_USED",
            _ => value.ToString().ToUpperInvariant()
        };

        private static TechnicalOperationResult<T> NotFound<T>() =>
            TechnicalOperationResult<T>.Failure(TechnicalOperationStatus.NotFound, "SESSION_NOT_FOUND", "Technical session was not found.");

        private static TechnicalOperationResult<T> BadRequest<T>(string code, string message) =>
            TechnicalOperationResult<T>.Failure(TechnicalOperationStatus.BadRequest, code, message);

        private static TechnicalOperationResult<T> Conflict<T>(string code, string message) =>
            TechnicalOperationResult<T>.Failure(TechnicalOperationStatus.Conflict, code, message);

        private static TechnicalOperationResult<T> ExternalFailure<T>(string code, string message) =>
            TechnicalOperationResult<T>.Failure(TechnicalOperationStatus.ExternalFailure, code, message);
    }
}
