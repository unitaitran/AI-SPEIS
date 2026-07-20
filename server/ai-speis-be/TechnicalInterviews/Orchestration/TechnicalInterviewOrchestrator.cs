using System.Text.Json;
using System.Collections.Immutable;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.QuestionRepo;
using ai_speis_be.Services.InterviewSessionService;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.TechnicalInterviews.DTOs;
using ai_speis_be.TechnicalInterviews.Rubrics;
using ai_speis_be.TechnicalInterviews.Scoring;
using ai_speis_be.TechnicalInterviews.Selection;
using ai_speis_be.TechnicalInterviews.Validation;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.TechnicalInterviews.Orchestration
{
    public sealed class TechnicalInterviewOrchestrator : ITechnicalInterviewOrchestrator
    {
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
        private readonly ITechnicalAnswerParallelProcessor _parallelProcessor;
        private readonly ITechnicalInterviewDecisionArbiter _decisionArbiter;
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
            ITechnicalAnswerParallelProcessor parallelProcessor,
            ITechnicalInterviewDecisionArbiter decisionArbiter,
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
            _parallelProcessor = parallelProcessor;
            _decisionArbiter = decisionArbiter;
            _sessionLifecycleService = sessionLifecycleService;
            _options = options;
            _logger = logger;
        }

        public async Task<TechnicalOperationResult<TechnicalInterviewSessionDto>> InitializeAsync(
            int userId,
            InitializeTechnicalInterviewRequest request,
            CancellationToken cancellationToken)
        {
            var session = await GetOwnedSessionAsync(userId, request.InterviewSessionId, cancellationToken);
            if (session is null)
                return NotFound<TechnicalInterviewSessionDto>();
            if (session.InterviewRoundType != InterviewRoundType.Technical)
                return BadRequest<TechnicalInterviewSessionDto>("NOT_TECHNICAL_SESSION", "The session is not a Technical round.");
            if (session.TechnicalState.HasValue)
                return TechnicalOperationResult<TechnicalInterviewSessionDto>.Ok(MapSession(session));

            var jd = session.InterviewCampaign.JDExtractedProfile;
            var roleTargets = TechnicalQuestionMetadata.ResolveRoleAliases(jd.RoleTarget, jd.JobTitle);
            if (roleTargets.Count == 0)
                return BadRequest<TechnicalInterviewSessionDto>("UNSUPPORTED_JOB_ROLE", "The JD role cannot be mapped to the Technical Question Bank.");

            var language = session.InterviewCampaign.Language.Trim().ToLowerInvariant();
            var availableSkills = await _questionRepository.GetTechnicalSkillsAsync(
                language,
                roleTargets,
                cancellationToken);
            if (availableSkills.Count == 0)
                return BadRequest<TechnicalInterviewSessionDto>("NO_TECHNICAL_CANDIDATE", "No active Technical question matches the session language and role.");

            var selectedSkillsResult = ResolveSelectedSkills(session, request.SelectedSkills, availableSkills);
            if (selectedSkillsResult.Count == 0)
                return BadRequest<TechnicalInterviewSessionDto>("INVALID_SELECTED_SKILLS", "Selected skills do not match the Technical Question Bank.");

            var rubric = _rubricProvider.GetRequired(_options.RubricVersion);
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
            session.TechnicalConcurrencyVersion++;
            session.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return TechnicalOperationResult<TechnicalInterviewSessionDto>.Created(MapSession(session));
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict<TechnicalInterviewSessionDto>("SESSION_CONCURRENCY_CONFLICT", "The session changed concurrently.");
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

            var existingCurrent = GetReadyAttempt(session);
            if (existingCurrent is not null)
                return TechnicalOperationResult<TechnicalCurrentQuestionDto>.Ok(MapCurrentQuestion(session, existingCurrent));

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
            return await SelectNextMainQuestionAsync(session, cancellationToken);
        }

        public async Task<TechnicalOperationResult<TechnicalInterviewSessionDto>> GetSessionAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken)
        {
            var session = await GetOwnedSessionAsync(userId, sessionId, cancellationToken);
            return session is null
                ? NotFound<TechnicalInterviewSessionDto>()
                : TechnicalOperationResult<TechnicalInterviewSessionDto>.Ok(MapSession(session));
        }

        public async Task<TechnicalOperationResult<TechnicalCurrentQuestionDto>> GetCurrentQuestionAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken)
        {
            var session = await GetOwnedSessionAsync(userId, sessionId, cancellationToken);
            if (session is null)
                return NotFound<TechnicalCurrentQuestionDto>();

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

            var attempt = session.TechnicalQuestionAttempts.FirstOrDefault(item => item.AttemptId == request.AttemptId);
            if (attempt is null)
                return BadRequest<TechnicalSubmitAnswerResponseDto>("ATTEMPT_NOT_IN_SESSION", "Attempt does not belong to this session.");

            var existingEvaluation = attempt.Evaluations.SingleOrDefault();
            if (existingEvaluation is not null
                && string.Equals(attempt.SubmissionIdempotencyKey, idempotencyKey, StringComparison.Ordinal))
            {
                return TechnicalOperationResult<TechnicalSubmitAnswerResponseDto>.Ok(
                    BuildSubmitResponse(session, attempt, existingEvaluation.Decision));
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
            attempt.FeedbackTaskStatus = TechnicalAITaskStatus.Processing;
            attempt.QuestionGenerationTaskStatus = TechnicalAITaskStatus.Processing;
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
            var pool = session.TechnicalCompletedMainQuestionCount + 1 < session.QuestionCount
                ? await _selectionService.PreparePoolAsync(
                    BuildSelectionContext(session),
                    cancellationToken)
                : new TechnicalQuestionPoolResult();
            var processingContext = BuildProcessingContext(
                session,
                attempt,
                root,
                children,
                rubric,
                pool.Candidates);
            var parallelResults = await _parallelProcessor.ProcessAsync(
                processingContext,
                cancellationToken);

            var activeQuestions = await _questionRepository.GetActiveTechnicalQuestionsByIdsAsync(
                processingContext.CandidateQuestionPool.Select(item => item.QuestionId).ToArray(),
                cancellationToken);
            var activeCandidateIds = activeQuestions.Select(item => item.QuestionId).ToHashSet();
            var arbiterResult = _decisionArbiter.Resolve(
                processingContext,
                rubric,
                parallelResults,
                activeCandidateIds);

            ApplyProcessingOutcome(attempt, parallelResults, arbiterResult);
            AddParallelInteractionLogs(session, attempt.AttemptId, parallelResults, arbiterResult);

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
                            "The session changed while the parallel answer result was being persisted.");
                    return Conflict<TechnicalSubmitAnswerResponseDto>(
                        "NO_ACTIVE_NEXT_QUESTION",
                        "Evaluation completed, but no active Question Bank candidate is available for the next main question.");
                }

                await ResetFailedEvaluationAsync(session, attempt, cancellationToken);
                return ExternalFailure<TechnicalSubmitAnswerResponseDto>(
                    arbiterResult.ErrorCode ?? "AI_EVALUATION_FAILED",
                    "Answer evaluation failed backend validation. The same attempt can be submitted again.");
            }

            var evaluationResult = parallelResults.Evaluation.ProviderResult!;
            var evaluationData = evaluationResult.Data!;
            var score = arbiterResult.Score!;
            var feedback = arbiterResult.Feedback!;

            var evaluation = new TechnicalAnswerEvaluation
            {
                AttemptId = attempt.AttemptId,
                RootMainAttemptId = root.AttemptId,
                RubricVersion = rubric.Version,
                AiSuggestedOverallScore = score.AiSuggestedOverallScore,
                FinalOverallScore = score.FinalOverallScore,
                DimensionEvaluationsJson = JsonSerializer.Serialize(evaluationData.DimensionEvaluations, JsonOptions),
                ScoringBreakdownJson = JsonSerializer.Serialize(score.Dimensions, JsonOptions),
                StrengthsJson = SerializeList(feedback.Strengths),
                MissingPointsJson = SerializeList(feedback.MissingPoints),
                IncorrectClaimsJson = SerializeList(feedback.IncorrectClaims),
                ImprovementSuggestionsJson = SerializeList(feedback.ImprovementSuggestions),
                FeedbackSummary = feedback.Summary,
                FeedbackPromptVersion = TechnicalPromptVersions.Feedback,
                FeedbackModelName = parallelResults.Feedback.ProviderResult?.Model ?? evaluationResult.Model,
                FeedbackFallbackUsed = feedback.FallbackUsed,
                Decision = arbiterResult.Decision,
                Confidence = evaluationData.Confidence,
                PromptVersion = TechnicalPromptVersions.Evaluation,
                ModelName = evaluationResult.Model,
                IsFinalForMainQuestion = arbiterResult.FinalizeMainQuestion,
                CreatedAt = DateTime.UtcNow
            };
            _context.TechnicalAnswerEvaluations.Add(evaluation);
            attempt.Evaluations.Add(evaluation);

            attempt.Status = TechnicalAttemptStatus.Completed;
            attempt.CompletedAt = DateTime.UtcNow;
            if (!arbiterResult.FinalizeMainQuestion)
            {
                var nextAttempt = CreateSubQuestionAttempt(
                    session,
                    attempt,
                    root,
                    arbiterResult.NextQuestion!.AttemptType!.Value,
                    arbiterResult.NextQuestion.Content!);
                _context.TechnicalQuestionAttempts.Add(nextAttempt);
                session.TechnicalQuestionAttempts.Add(nextAttempt);
                session.TechnicalState = TechnicalInterviewState.QuestionReady;
                session.TechnicalConcurrencyVersion++;
                session.UpdatedAt = DateTime.UtcNow;
                if (!await TrySaveAnswerOutcomeAsync(cancellationToken))
                    return Conflict<TechnicalSubmitAnswerResponseDto>(
                        "SESSION_CONCURRENCY_CONFLICT",
                        "The session changed while the parallel answer result was being persisted.");

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
                var finalResult = await FinalizeSessionAsync(
                    session,
                    userId,
                    cancellationToken,
                    generateNaturalSummary: false);
                if (finalResult.Status != TechnicalOperationStatus.Ok)
                    return TechnicalOperationResult<TechnicalSubmitAnswerResponseDto>.Failure(
                        finalResult.Status,
                        finalResult.ErrorCode ?? "FINALIZATION_FAILED",
                        finalResult.Message ?? "Technical session finalization failed.");

                return TechnicalOperationResult<TechnicalSubmitAnswerResponseDto>.Ok(
                    BuildSubmitResponse(session, attempt, arbiterResult.Decision));
            }

            var nextQuestionId = arbiterResult.NextQuestion?.SelectedMainQuestionId;
            var nextQuestion = activeQuestions.FirstOrDefault(item => item.QuestionId == nextQuestionId);
            if (nextQuestion is null)
            {
                attempt.Status = TechnicalAttemptStatus.Failed;
                session.TechnicalState = TechnicalInterviewState.Failed;
                session.TechnicalConcurrencyVersion++;
                session.UpdatedAt = DateTime.UtcNow;
                if (!await TrySaveAnswerOutcomeAsync(cancellationToken))
                    return Conflict<TechnicalSubmitAnswerResponseDto>(
                        "SESSION_CONCURRENCY_CONFLICT",
                        "The session changed while the parallel answer result was being persisted.");
                return Conflict<TechnicalSubmitAnswerResponseDto>(
                    "NEXT_QUESTION_BECAME_INACTIVE",
                    "The selected next question is no longer active.");
            }

            var nextMainAttempt = CreateMainQuestionAttempt(session, nextQuestion);
            _context.TechnicalQuestionAttempts.Add(nextMainAttempt);
            session.TechnicalQuestionAttempts.Add(nextMainAttempt);
            session.TechnicalState = TechnicalInterviewState.QuestionReady;
            session.TechnicalConcurrencyVersion++;
            session.UpdatedAt = DateTime.UtcNow;
            if (!await TrySaveAnswerOutcomeAsync(cancellationToken))
                return Conflict<TechnicalSubmitAnswerResponseDto>(
                    "SESSION_CONCURRENCY_CONFLICT",
                    "The session changed while the parallel answer result was being persisted.");

            return TechnicalOperationResult<TechnicalSubmitAnswerResponseDto>.Ok(
                BuildSubmitResponse(session, attempt, arbiterResult.Decision));
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
                return TechnicalOperationResult<TechnicalInterviewResultDto>.Ok(BuildResult(session));
            if (session.TechnicalCompletedMainQuestionCount < session.QuestionCount)
                return Conflict<TechnicalInterviewResultDto>("MAIN_QUESTION_TARGET_NOT_REACHED", "The required main questions are not completed.");
            if (session.TechnicalQuestionAttempts.Any(item => item.Status == TechnicalAttemptStatus.Ready))
                return Conflict<TechnicalInterviewResultDto>("QUESTION_STILL_PENDING", "The current question must be answered before completion.");

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
            if (session.TechnicalState != TechnicalInterviewState.Completed)
                return Conflict<TechnicalInterviewResultDto>("SESSION_NOT_COMPLETED", "Technical result is available only after completion.");

            return TechnicalOperationResult<TechnicalInterviewResultDto>.Ok(BuildResult(session));
        }

        private async Task<TechnicalOperationResult<TechnicalCurrentQuestionDto>> SelectNextMainQuestionAsync(
            InterviewSession session,
            CancellationToken cancellationToken)
        {
            session.TechnicalState = TechnicalInterviewState.SelectingQuestion;
            session.TechnicalConcurrencyVersion++;
            await _context.SaveChangesAsync(cancellationToken);

            var selection = await _selectionService.SelectAsync(
                BuildSelectionContext(session),
                cancellationToken);

            if (selection.AIResult is not null)
            {
                AddInteractionLog(
                    session,
                    null,
                    AIInteractionOperationType.QuestionSelection,
                    TechnicalPromptVersions.Selection,
                    selection.AIResult,
                    selection.FallbackUsed,
                    selection.AIResult.ErrorCode);
            }

            if (selection.Question is null)
            {
                session.TechnicalState = TechnicalInterviewState.Failed;
                session.TechnicalConcurrencyVersion++;
                await _context.SaveChangesAsync(cancellationToken);
                return TechnicalOperationResult<TechnicalCurrentQuestionDto>.Failure(
                    TechnicalOperationStatus.Conflict,
                    selection.ErrorCode ?? "NO_TECHNICAL_CANDIDATE",
                    "No Technical question is available for the remaining session constraints.");
            }

            var question = selection.Question;
            var attempt = CreateMainQuestionAttempt(session, question);
            _context.TechnicalQuestionAttempts.Add(attempt);
            session.TechnicalQuestionAttempts.Add(attempt);
            session.TechnicalState = TechnicalInterviewState.QuestionReady;
            session.TechnicalConcurrencyVersion++;
            session.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            return TechnicalOperationResult<TechnicalCurrentQuestionDto>.Created(
                MapCurrentQuestion(session, attempt));
        }

        private static TechnicalSelectionContext BuildSelectionContext(InterviewSession session)
        {
            var mainAttempts = session.TechnicalQuestionAttempts
                .Where(item => item.QuestionType == TechnicalAttemptType.Main)
                .ToList();
            return new TechnicalSelectionContext
            {
                Language = session.TechnicalLanguage!,
                JobRole = session.TechnicalJobRole!,
                ExperienceLevel = session.TechnicalExperienceLevel ?? string.Empty,
                Difficulty = session.Difficulty,
                SelectedSkills = DeserializeList(session.TechnicalSelectedSkillsJson),
                AskedQuestionIds = mainAttempts
                    .Where(item => item.QuestionId.HasValue)
                    .Select(item => item.QuestionId!.Value)
                    .ToHashSet(),
                SkillUsage = mainAttempts
                    .Where(item => !string.IsNullOrWhiteSpace(item.SkillSnapshot))
                    .GroupBy(item => item.SkillSnapshot!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
                AskedSubskills = mainAttempts
                    .Where(item => !string.IsNullOrWhiteSpace(item.SubskillSnapshot))
                    .Select(item => item.SubskillSnapshot!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
            };
        }

        private TechnicalAnswerProcessingContext BuildProcessingContext(
            InterviewSession session,
            TechnicalQuestionAttempt attempt,
            TechnicalQuestionAttempt root,
            IReadOnlyList<TechnicalQuestionAttempt> children,
            TechnicalRubricDefinition rubric,
            IReadOnlyList<Question> candidatePool)
        {
            var question = root.Question
                ?? throw new InvalidOperationException("Main Technical attempt is missing its Question reference.");
            var campaign = session.InterviewCampaign;
            var cvSkills = campaign.CVExtractedProfile.Skills.Select(item => item.SkillName).ToList();
            var mainAttempts = session.TechnicalQuestionAttempts
                .Where(item => item.QuestionType == TechnicalAttemptType.Main)
                .ToList();
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

            return new TechnicalAnswerProcessingContext
            {
                SessionId = session.InterviewSessionId,
                AttemptId = attempt.AttemptId,
                RootMainAttemptId = root.AttemptId,
                QuestionId = root.QuestionId
                    ?? throw new InvalidOperationException("Main Technical attempt has no Question id."),
                QuestionType = ToApi(attempt.QuestionType),
                QuestionContent = attempt.QuestionContentSnapshot,
                MainQuestionContent = root.QuestionContentSnapshot,
                ExpectedAnswer = question.SuggestedAnswer,
                KeyPoints = question.ExpectedKeyPoints ?? string.Empty,
                QuestionSpecificRubric = question.ScoringRubric ?? string.Empty,
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
                JobRole = session.TechnicalJobRole ?? string.Empty,
                ExperienceLevel = session.TechnicalExperienceLevel ?? string.Empty,
                Language = session.TechnicalLanguage ?? string.Empty,
                CvContext = JsonSerializer.Serialize(new
                {
                    roleTarget = campaign.CVExtractedProfile.RoleTarget,
                    skills = cvSkills
                }, JsonOptions),
                JdContext = JsonSerializer.Serialize(new
                {
                    campaign.JDExtractedProfile.JobTitle,
                    campaign.JDExtractedProfile.RoleTarget,
                    campaign.JDExtractedProfile.ExperienceLevel,
                    requiredSkills = TechnicalQuestionMetadata.ParseStringArray(campaign.JDExtractedProfile.RequiredSkills)
                }, JsonOptions),
                ClarificationCount = children.Count(item => item.QuestionType == TechnicalAttemptType.Clarification),
                FollowUpCount = children.Count(item => item.QuestionType == TechnicalAttemptType.FollowUp),
                CompletedMainQuestionCount = session.TechnicalCompletedMainQuestionCount,
                MainQuestionIndex = root.MainQuestionIndex,
                TargetMainQuestionCount = session.QuestionCount,
                AskedQuestionIds = mainAttempts
                    .Where(item => item.QuestionId.HasValue)
                    .Select(item => item.QuestionId!.Value)
                    .ToImmutableHashSet(),
                CandidateQuestionPool = candidatePool.Select(item => new TechnicalAIQuestionCandidate(
                    item.QuestionId,
                    item.QuestionContent,
                    item.Skill ?? string.Empty,
                    TechnicalQuestionMetadata.GetSubskill(item.QdrantPayloadJson),
                    item.Difficulty.ToString(),
                    item.ExperienceLevel ?? string.Empty)).ToImmutableArray(),
                SkillCoverage = mainAttempts
                    .Where(item => !string.IsNullOrWhiteSpace(item.SkillSnapshot))
                    .GroupBy(item => item.SkillSnapshot!, StringComparer.OrdinalIgnoreCase)
                    .ToImmutableDictionary(
                        group => group.Key,
                        group => group.Count(),
                        StringComparer.OrdinalIgnoreCase),
                DifficultyCoverage = mainAttempts
                    .Where(item => item.DifficultySnapshot.HasValue)
                    .GroupBy(item => item.DifficultySnapshot!.Value.ToString(), StringComparer.OrdinalIgnoreCase)
                    .ToImmutableDictionary(
                        group => group.Key,
                        group => group.Count(),
                        StringComparer.OrdinalIgnoreCase),
                PromptVersions = new TechnicalPromptVersionSnapshot(
                    TechnicalPromptVersions.Evaluation,
                    TechnicalPromptVersions.Feedback,
                    TechnicalPromptVersions.QuestionBundle)
            };
        }

        private async Task<TechnicalOperationResult<TechnicalInterviewResultDto>> FinalizeSessionAsync(
            InterviewSession session,
            int userId,
            CancellationToken cancellationToken,
            bool generateNaturalSummary = true)
        {
            var rubric = _rubricProvider.GetRequired(session.TechnicalRubricVersion!);
            var finalEvaluations = session.TechnicalQuestionAttempts
                .SelectMany(item => item.Evaluations)
                .Where(item => item.IsFinalForMainQuestion)
                .ToList();
            if (finalEvaluations.Count != session.TechnicalCompletedMainQuestionCount)
            {
                return Conflict<TechnicalInterviewResultDto>("INCOMPLETE_SCORING_BREAKDOWN", "Final main-question evaluations are incomplete.");
            }

            var finalScore = _scoringService.ScoreSession(
                finalEvaluations.Select(item => item.FinalOverallScore),
                rubric);
            var band = rubric.GetPerformanceBand(finalScore);
            session.TechnicalFinalScore = finalScore;
            session.TechnicalPerformanceBand = band.Name;

            var provisionalResult = BuildResult(session, includeStoredSummary: false);
            TechnicalFinalSummaryDto summary;
            if (!generateNaturalSummary)
            {
                summary = BuildDeterministicSummary(provisionalResult, band.Name);
            }
            else
            {
                var summaryResult = await _providerResolver.Resolve().GenerateFinalSummaryAsync(
                    new TechnicalAIFinalSummaryRequest
                    {
                        RubricVersion = rubric.Version,
                        OverallScore = finalScore,
                        PerformanceBand = band.Name,
                        MainQuestionResults = provisionalResult.MainQuestions.Cast<object>().ToList(),
                        SkillResults = provisionalResult.SkillScores.Cast<object>().ToList()
                    },
                    cancellationToken);
                var fallbackUsed = !summaryResult.Success
                    || summaryResult.Data is null
                    || string.IsNullOrWhiteSpace(summaryResult.Data.Summary);
                if (fallbackUsed)
                {
                    summary = BuildDeterministicSummary(provisionalResult, band.Name);
                }
                else
                {
                    summary = new TechnicalFinalSummaryDto
                    {
                        Summary = summaryResult.Data!.Summary.Trim(),
                        Strengths = CleanList(summaryResult.Data.Strengths),
                        AreasForImprovement = CleanList(summaryResult.Data.AreasForImprovement),
                        RecommendedNextSteps = CleanList(summaryResult.Data.RecommendedNextSteps)
                    };
                }

                AddInteractionLog(
                    session,
                    null,
                    AIInteractionOperationType.FinalSummary,
                    TechnicalPromptVersions.Summary,
                    summaryResult,
                    fallbackUsed,
                    summaryResult.ErrorCode);
            }
            session.TechnicalSummaryJson = JsonSerializer.Serialize(summary, JsonOptions);
            session.TechnicalState = TechnicalInterviewState.Completed;
            session.TechnicalCompletedAt = DateTime.UtcNow;
            session.TechnicalConcurrencyVersion++;
            session.UpdatedAt = DateTime.UtcNow;
            if (!await TrySaveAnswerOutcomeAsync(cancellationToken))
            {
                return Conflict<TechnicalInterviewResultDto>(
                    "SESSION_CONCURRENCY_CONFLICT",
                    "The session changed while final Technical Interview results were being persisted.");
            }

            if (session.Status == InterviewSessionStatus.Active)
            {
                var lifecycleResult = await _sessionLifecycleService.CompleteSessionAsync(userId, session.InterviewSessionId);
                if (!lifecycleResult.Success)
                {
                    _logger.LogWarning(
                        "Technical session {SessionId} completed, but generic session lifecycle completion was rejected: {Error}",
                        session.InterviewSessionId,
                        lifecycleResult.ErrorMessage);
                }
            }

            return TechnicalOperationResult<TechnicalInterviewResultDto>.Ok(BuildResult(session));
        }

        private TechnicalInterviewResultDto BuildResult(
            InterviewSession session,
            bool includeStoredSummary = true)
        {
            var finalEvaluations = session.TechnicalQuestionAttempts
                .SelectMany(item => item.Evaluations)
                .Where(item => item.IsFinalForMainQuestion)
                .ToList();
            var mainAttempts = session.TechnicalQuestionAttempts
                .Where(item => item.QuestionType == TechnicalAttemptType.Main)
                .ToDictionary(item => item.AttemptId);

            var mainResults = finalEvaluations.Select(evaluation =>
            {
                var root = mainAttempts[evaluation.RootMainAttemptId];
                var aiDimensions = Deserialize<TechnicalAIDimensionEvaluation>(evaluation.DimensionEvaluationsJson);
                var scores = Deserialize<TechnicalDimensionScore>(evaluation.ScoringBreakdownJson);
                var aiByCode = aiDimensions.ToDictionary(item => item.RubricCode, StringComparer.OrdinalIgnoreCase);
                return new TechnicalMainQuestionResultDto
                {
                    AttemptId = root.AttemptId,
                    QuestionId = root.QuestionId,
                    MainQuestionIndex = root.MainQuestionIndex,
                    Question = root.QuestionContentSnapshot,
                    Skill = root.SkillSnapshot ?? "Uncategorized",
                    Score = evaluation.FinalOverallScore,
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
                        ReasonSummary = aiByCode.TryGetValue(score.RubricCode, out ai) ? ai.ReasonSummary : string.Empty
                    }).ToList(),
                    Strengths = DeserializeList(evaluation.StrengthsJson),
                    MissingPoints = DeserializeList(evaluation.MissingPointsJson),
                    IncorrectClaims = DeserializeList(evaluation.IncorrectClaimsJson),
                    ImprovementSuggestions = DeserializeList(evaluation.ImprovementSuggestionsJson),
                    FeedbackSummary = evaluation.FeedbackSummary
                };
            }).OrderBy(item => item.MainQuestionIndex).ToList();

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

            return new TechnicalInterviewResultDto
            {
                SessionId = session.InterviewSessionId,
                RubricVersion = session.TechnicalRubricVersion ?? string.Empty,
                ScoringPolicyVersion = session.TechnicalScoringPolicyVersion ?? string.Empty,
                OverallScore = session.TechnicalFinalScore
                    ?? (mainResults.Count == 0 ? 0m : Math.Round(mainResults.Average(item => item.Score), 2, MidpointRounding.AwayFromZero)),
                PerformanceBand = session.TechnicalPerformanceBand ?? string.Empty,
                MainQuestions = mainResults,
                SkillScores = skillScores,
                Summary = summary
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
            Question question)
        {
            var attemptId = Guid.NewGuid();
            return new TechnicalQuestionAttempt
            {
                AttemptId = attemptId,
                InterviewSessionId = session.InterviewSessionId,
                QuestionId = question.QuestionId,
                RootMainAttemptId = attemptId,
                QuestionType = TechnicalAttemptType.Main,
                QuestionContentSnapshot = question.QuestionContent,
                SequenceNumber = NextSequenceNumber(session),
                MainQuestionIndex = session.TechnicalCompletedMainQuestionCount + 1,
                SkillSnapshot = question.Skill,
                SubskillSnapshot = TechnicalQuestionMetadata.GetSubskill(question.QdrantPayloadJson),
                DifficultySnapshot = question.Difficulty,
                Status = TechnicalAttemptStatus.Ready,
                CreatedAt = DateTime.UtcNow
            };
        }

        private TechnicalQuestionAttempt CreateSubQuestionAttempt(
            InterviewSession session,
            TechnicalQuestionAttempt parent,
            TechnicalQuestionAttempt root,
            TechnicalAttemptType type,
            string content)
        {
            return new TechnicalQuestionAttempt
            {
                AttemptId = Guid.NewGuid(),
                InterviewSessionId = session.InterviewSessionId,
                QuestionId = null,
                ParentAttemptId = parent.AttemptId,
                RootMainAttemptId = root.AttemptId,
                QuestionType = type,
                QuestionContentSnapshot = content.Trim(),
                SequenceNumber = NextSequenceNumber(session),
                MainQuestionIndex = root.MainQuestionIndex,
                SkillSnapshot = root.SkillSnapshot,
                SubskillSnapshot = root.SubskillSnapshot,
                DifficultySnapshot = root.DifficultySnapshot,
                Status = TechnicalAttemptStatus.Ready,
                CreatedAt = DateTime.UtcNow
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
                .Include(session => session.TechnicalQuestionAttempts)
                    .ThenInclude(attempt => attempt.Question)
                .Include(session => session.TechnicalQuestionAttempts)
                    .ThenInclude(attempt => attempt.Evaluations)
                .FirstOrDefaultAsync(session =>
                    session.InterviewSessionId == sessionId
                    && session.InterviewCampaign.UserId == userId,
                    cancellationToken);
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

        private static TechnicalInterviewSessionDto MapSession(InterviewSession session)
        {
            return new TechnicalInterviewSessionDto
            {
                SessionId = session.InterviewSessionId,
                JobRole = session.TechnicalJobRole ?? string.Empty,
                ExperienceLevel = session.TechnicalExperienceLevel ?? string.Empty,
                Language = session.TechnicalLanguage ?? string.Empty,
                SelectedSkills = DeserializeList(session.TechnicalSelectedSkillsJson),
                TargetMainQuestionCount = session.QuestionCount,
                CompletedMainQuestionCount = session.TechnicalCompletedMainQuestionCount,
                Status = session.TechnicalState.HasValue ? ToApi(session.TechnicalState.Value) : "NOT_INITIALIZED",
                AiProvider = session.TechnicalAiProvider ?? string.Empty,
                RubricVersion = session.TechnicalRubricVersion ?? string.Empty,
                ScoringPolicyVersion = session.TechnicalScoringPolicyVersion ?? string.Empty,
                StartedAt = session.TechnicalStartedAt,
                CompletedAt = session.TechnicalCompletedAt,
                FinalScore = session.TechnicalFinalScore,
                PerformanceBand = session.TechnicalPerformanceBand
            };
        }

        private static TechnicalCurrentQuestionDto MapCurrentQuestion(
            InterviewSession session,
            TechnicalQuestionAttempt attempt)
        {
            return new TechnicalCurrentQuestionDto
            {
                AttemptId = attempt.AttemptId,
                QuestionId = attempt.QuestionId,
                QuestionType = ToApi(attempt.QuestionType),
                Content = attempt.QuestionContentSnapshot,
                Skill = attempt.SkillSnapshot,
                Difficulty = attempt.DifficultySnapshot?.ToString(),
                MainQuestionIndex = attempt.MainQuestionIndex,
                TotalMainQuestions = session.QuestionCount,
                SessionStatus = session.TechnicalState.HasValue ? ToApi(session.TechnicalState.Value) : "NOT_INITIALIZED"
            };
        }

        private static TechnicalSubmitAnswerResponseDto BuildSubmitResponse(
            InterviewSession session,
            TechnicalQuestionAttempt attempt,
            TechnicalInterviewDecision decision)
        {
            var next = GetReadyAttempt(session);
            return new TechnicalSubmitAnswerResponseDto
            {
                AttemptId = attempt.AttemptId,
                Processing = new TechnicalProcessingStatusDto
                {
                    Evaluation = ToApi(attempt.EvaluationTaskStatus),
                    Feedback = ToApi(attempt.FeedbackTaskStatus),
                    QuestionGeneration = ToApi(attempt.QuestionGenerationTaskStatus)
                },
                Evaluation = new TechnicalEvaluationDecisionDto { Decision = ToApi(decision) },
                Feedback = new TechnicalFeedbackAcknowledgementDto
                {
                    Status = ToApi(attempt.FeedbackTaskStatus),
                    AvailableInResult = true
                },
                NextQuestion = next is null ? null : MapCurrentQuestion(session, next),
                SessionStatus = session.TechnicalState.HasValue ? ToApi(session.TechnicalState.Value) : "NOT_INITIALIZED",
                Fallbacks = new TechnicalFallbackStatusDto
                {
                    EvaluationFallbackUsed = attempt.EvaluationFallbackUsed,
                    FeedbackFallbackUsed = attempt.FeedbackFallbackUsed,
                    QuestionFallbackUsed = attempt.QuestionFallbackUsed
                }
            };
        }

        private static void ApplyProcessingOutcome(
            TechnicalQuestionAttempt attempt,
            TechnicalParallelAIResults results,
            TechnicalDecisionArbiterResult arbiterResult)
        {
            attempt.EvaluationTaskStatus = arbiterResult.EvaluationStatus;
            attempt.FeedbackTaskStatus = arbiterResult.FeedbackStatus;
            attempt.QuestionGenerationTaskStatus = arbiterResult.QuestionStatus;
            attempt.EvaluationFallbackUsed = arbiterResult.EvaluationFallbackUsed;
            attempt.FeedbackFallbackUsed = arbiterResult.FeedbackFallbackUsed;
            attempt.QuestionFallbackUsed = arbiterResult.QuestionFallbackUsed;
            attempt.CriticalPathLatencyMs = arbiterResult.CriticalPathLatencyMs;
            attempt.SequentialEstimatedLatencyMs = results.Metrics.SequentialEstimatedLatencyMs;
            attempt.ParallelLatencySavingMs = results.Metrics.ParallelLatencySavingMs;
            attempt.ProcessingCompletedAt = DateTime.UtcNow;
            attempt.TotalProcessingLatencyMs = attempt.ProcessingStartedAt.HasValue
                ? Math.Max(0, (long)(attempt.ProcessingCompletedAt.Value - attempt.ProcessingStartedAt.Value).TotalMilliseconds)
                : results.Metrics.TotalProcessingLatencyMs;
        }

        private void AddParallelInteractionLogs(
            InterviewSession session,
            Guid attemptId,
            TechnicalParallelAIResults results,
            TechnicalDecisionArbiterResult arbiterResult)
        {
            AddTaskInteractionLog(
                session,
                attemptId,
                AIInteractionOperationType.AnswerEvaluation,
                TechnicalPromptVersions.Evaluation,
                results.Evaluation,
                arbiterResult.EvaluationStatus,
                arbiterResult.EvaluationFallbackUsed);
            AddTaskInteractionLog(
                session,
                attemptId,
                AIInteractionOperationType.FeedbackGeneration,
                TechnicalPromptVersions.Feedback,
                results.Feedback,
                arbiterResult.FeedbackStatus,
                arbiterResult.FeedbackFallbackUsed);
            AddTaskInteractionLog(
                session,
                attemptId,
                AIInteractionOperationType.QuestionBundleGeneration,
                TechnicalPromptVersions.QuestionBundle,
                results.QuestionBundle,
                arbiterResult.QuestionStatus,
                arbiterResult.QuestionFallbackUsed);
        }

        private void AddTaskInteractionLog<T>(
            InterviewSession session,
            Guid attemptId,
            AIInteractionOperationType operation,
            string promptVersion,
            TechnicalAITaskOutcome<T> outcome,
            TechnicalAITaskStatus finalStatus,
            bool fallbackUsed)
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
                EstimatedCost = null,
                Status = ToLogStatus(finalStatus),
                ErrorCode = outcome.ErrorCode ?? (finalStatus == TechnicalAITaskStatus.InvalidOutput
                    ? "INVALID_OUTPUT"
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
                EstimatedCost = null,
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
            string performanceBand)
        {
            var strongest = result.SkillScores.OrderByDescending(item => item.Score).FirstOrDefault();
            var weakest = result.SkillScores.OrderBy(item => item.Score).FirstOrDefault();
            return new TechnicalFinalSummaryDto
            {
                Summary = $"Technical score {result.OverallScore:0.00}/5.00, performance band: {performanceBand}.",
                Strengths = strongest is null
                    ? new List<string>()
                    : new List<string> { $"Highest assessed skill: {strongest.Skill} ({strongest.Score:0.00}/5.00)." },
                AreasForImprovement = weakest is null
                    ? new List<string>()
                    : new List<string> { $"Prioritize improvement in {weakest.Skill} ({weakest.Score:0.00}/5.00)." },
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
