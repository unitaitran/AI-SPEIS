using System.Text.RegularExpressions;
using ai_speis_be.Models.Enums;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.TechnicalInterviews.Rubrics;
using ai_speis_be.TechnicalInterviews.Scoring;
using ai_speis_be.TechnicalInterviews.Validation;

namespace ai_speis_be.TechnicalInterviews.Orchestration
{
    public sealed record TechnicalMergedFeedback(
        string Summary,
        IReadOnlyList<string> Strengths,
        IReadOnlyList<string> MissingPoints,
        IReadOnlyList<string> IncorrectClaims,
        IReadOnlyList<string> ImprovementSuggestions,
        bool FallbackUsed);

    public sealed record TechnicalArbiterNextQuestion(
        TechnicalAttemptType? AttemptType,
        string? Content,
        IReadOnlyList<string> TargetRubricCodes,
        int? SelectedMainQuestionId,
        TechnicalQuestionGenerationReason? GenerationReason = null,
        bool PlanDeviation = false,
        string? PlanDeviationReason = null);

    public sealed record TechnicalDecisionArbiterResult(
        bool IsSuccess,
        string? ErrorCode,
        TechnicalInterviewDecision Decision,
        bool FinalizeMainQuestion,
        TechnicalQuestionScore? Score,
        TechnicalMergedFeedback? Feedback,
        TechnicalArbiterNextQuestion? NextQuestion,
        TechnicalAITaskStatus EvaluationStatus,
        TechnicalAITaskStatus FeedbackStatus,
        TechnicalAITaskStatus QuestionStatus,
        bool EvaluationFallbackUsed,
        bool FeedbackFallbackUsed,
        bool QuestionFallbackUsed,
        long CriticalPathLatencyMs,
        TechnicalInterviewDecision? AiSuggestedAction,
        string? OverrideReason,
        decimal RawScore,
        decimal AppliedBonus,
        decimal CumulativeFollowUpBonus,
        decimal? FinalMainQuestionScore,
        int RequiredClarificationCount,
        int RequiredFollowUpCount,
        TechnicalAdaptiveStage AdaptiveStage);

    public interface ITechnicalInterviewDecisionArbiter
    {
        TechnicalDecisionArbiterResult Resolve(
            TechnicalAnswerProcessingContext context,
            TechnicalRubricDefinition rubric,
            TechnicalParallelAIResults results,
            IReadOnlySet<int> activeCandidateQuestionIds);
    }

    public sealed class TechnicalInterviewDecisionArbiter : ITechnicalInterviewDecisionArbiter
    {
        private readonly ITechnicalAIResponseValidator _validator;
        private readonly ITechnicalRubricScoringService _scoringService;
        private readonly ITechnicalFollowUpDecisionEngine _decisionEngine;
        private readonly ITechnicalAdaptiveRuleEngine _adaptiveRuleEngine;
        private readonly ITechnicalFollowUpBonusCalculator _bonusCalculator;
        private readonly TechnicalInterviewOptions _options;

        public TechnicalInterviewDecisionArbiter(
            ITechnicalAIResponseValidator validator,
            ITechnicalRubricScoringService scoringService,
            ITechnicalFollowUpDecisionEngine decisionEngine,
            ITechnicalAdaptiveRuleEngine adaptiveRuleEngine,
            ITechnicalFollowUpBonusCalculator bonusCalculator,
            TechnicalInterviewOptions options)
        {
            _validator = validator;
            _scoringService = scoringService;
            _decisionEngine = decisionEngine;
            _adaptiveRuleEngine = adaptiveRuleEngine;
            _bonusCalculator = bonusCalculator;
            _options = options;
        }

        public TechnicalDecisionArbiterResult Resolve(
            TechnicalAnswerProcessingContext context,
            TechnicalRubricDefinition rubric,
            TechnicalParallelAIResults results,
            IReadOnlySet<int> activeCandidateQuestionIds)
        {
            if (!results.Evaluation.IsFulfilled)
            {
                return Failure(
                    results.Evaluation.ErrorCode ?? "AI_EVALUATION_FAILED",
                    results,
                    results.Evaluation.Status);
            }

            var evaluation = results.Evaluation.ProviderResult!.Data!;
            var validation = _validator.ValidateEvaluation(
                evaluation,
                rubric,
                context.BuildCompleteAnswerContext());
            if (!validation.IsValid)
            {
                return Failure(
                    validation.ErrorCode ?? "INVALID_AI_EVALUATION",
                    results,
                    TechnicalAITaskStatus.InvalidOutput);
            }

            var score = _scoringService.ScoreQuestion(evaluation, rubric);
            var aiSuggestedAction = validation.AiSuggestedDecision;
            var appliedBonus = 0m;
            var cumulativeBonus = context.CumulativeFollowUpBonus;
            if (context.UseAdaptiveRubricFramework
                && context.AttemptType == TechnicalAttemptType.FollowUp)
            {
                var bonus = _bonusCalculator.Calculate(score, evaluation, rubric);
                appliedBonus = bonus.Bonus;
                cumulativeBonus = Math.Min(2m, cumulativeBonus + appliedBonus);
            }

            TechnicalInterviewDecision resolvedDecision;
            bool finalizeMainQuestion;
            TechnicalAttemptType? nextAttemptType;
            TechnicalQuestionGenerationReason? nextGenerationReason;
            int requiredClarificationCount;
            int requiredFollowUpCount;
            TechnicalAdaptiveStage adaptiveStage;
            decimal? finalMainQuestionScore = null;

            if (context.UseAdaptiveRubricFramework)
            {
                var initialMainScore = context.AttemptType == TechnicalAttemptType.Main
                    ? score.FinalOverallScore
                    : context.InitialMainScore
                        ?? throw new InvalidOperationException("Adaptive sub-question context is missing the initial Main score.");
                var adaptive = _adaptiveRuleEngine.Resolve(new TechnicalAdaptiveRuleInput(
                    context.AttemptType,
                    initialMainScore,
                    context.RequiredClarificationCount,
                    context.CompletedClarificationCount,
                    context.RequiredFollowUpCount,
                    context.CompletedFollowUpCount,
                    context.CompletedMainQuestionCount,
                    context.TargetMainQuestionCount,
                    context.IsReliabilityFollowUpRequired,
                    context.CurrentGenerationReason == TechnicalQuestionGenerationReason.ReliabilityMinimum));
                resolvedDecision = adaptive.Decision;
                finalizeMainQuestion = adaptive.FinalizeMainQuestion;
                nextAttemptType = adaptive.NextAttemptType;
                nextGenerationReason = adaptive.NextGenerationReason;
                requiredClarificationCount = adaptive.RequiredClarificationCount;
                requiredFollowUpCount = adaptive.RequiredFollowUpCount;
                adaptiveStage = adaptive.Stage;

                if (finalizeMainQuestion)
                {
                    var baseScore = context.AttemptType switch
                    {
                        TechnicalAttemptType.Main => score.FinalOverallScore,
                        TechnicalAttemptType.Clarification => _scoringService.ApplyClarificationRecovery(
                            score.FinalOverallScore,
                            _options.ClarificationRecoveryFactor,
                            rubric),
                        _ => context.CurrentMainBaseScore
                    };
                    finalMainQuestionScore = _scoringService.Normalize(baseScore + cumulativeBonus, rubric);
                }
            }
            else
            {
                var legacy = _decisionEngine.Resolve(
                    aiSuggestedAction ?? TechnicalInterviewDecision.NextQuestion,
                    context.ClarificationCount,
                    context.FollowUpCount,
                    context.CompletedMainQuestionCount,
                    context.TargetMainQuestionCount,
                    hasValidNextQuestion: true,
                    rubric.Limits);
                resolvedDecision = legacy.Decision;
                finalizeMainQuestion = legacy.FinalizeMainQuestion;
                nextAttemptType = legacy.NextAttemptType;
                nextGenerationReason = TechnicalQuestionGenerationReason.AdaptiveScoreRule;
                requiredClarificationCount = context.RequiredClarificationCount;
                requiredFollowUpCount = context.RequiredFollowUpCount;
                adaptiveStage = legacy.FinalizeMainQuestion
                    ? TechnicalAdaptiveStage.Finalized
                    : legacy.NextAttemptType == TechnicalAttemptType.Clarification
                        ? TechnicalAdaptiveStage.AwaitingClarification
                        : TechnicalAdaptiveStage.AwaitingFollowUp;
                finalMainQuestionScore = legacy.FinalizeMainQuestion ? score.FinalOverallScore : null;
            }

            var overrideReason = aiSuggestedAction.HasValue
                ? aiSuggestedAction.Value == resolvedDecision
                    ? null
                    : "BACKEND_ADAPTIVE_RULE_OVERRIDE"
                : "AI_SUGGESTED_ACTION_INVALID_OR_MISSING";

            var feedback = MergeFeedback(context, evaluation, results.Feedback);
            var feedbackStatus = feedback.FallbackUsed
                ? TechnicalAITaskStatus.FallbackUsed
                : results.Feedback.Status;
            var questionStatus = results.QuestionBundle.Status;
            var questionFallbackUsed = false;
            TechnicalArbiterNextQuestion? nextQuestion = null;

            if (!finalizeMainQuestion)
            {
                var bundleCandidate = nextAttemptType == TechnicalAttemptType.Clarification
                    ? results.QuestionBundle.ProviderResult?.Data?.ClarificationCandidate
                    : results.QuestionBundle.ProviderResult?.Data?.FollowUpCandidate;
                var candidateValid = results.QuestionBundle.IsFulfilled
                    && IsValidSubQuestion(bundleCandidate, nextAttemptType!.Value, evaluation, rubric, context);

                if (candidateValid)
                {
                    nextQuestion = new TechnicalArbiterNextQuestion(
                        nextAttemptType,
                        bundleCandidate!.Content.Trim(),
                        CleanCodes(bundleCandidate.TargetRubricCodes),
                        null,
                        nextGenerationReason);
                }
                else
                {
                    questionFallbackUsed = true;
                    questionStatus = TechnicalAITaskStatus.FallbackUsed;
                    var targetCodes = ResolveFallbackTargetCodes(evaluation, rubric);
                    nextQuestion = new TechnicalArbiterNextQuestion(
                        nextAttemptType,
                        BuildSubQuestionFallback(context.Language, nextAttemptType!.Value),
                        targetCodes,
                        null,
                        nextGenerationReason);
                }
            }
            else if (resolvedDecision == TechnicalInterviewDecision.NextQuestion)
            {
                var selectedId = results.QuestionBundle.IsFulfilled
                    ? results.QuestionBundle.ProviderResult?.Data?.NextMainQuestionCandidate?.SelectedQuestionId
                    : null;
                var validSelectedId = selectedId.HasValue
                    && activeCandidateQuestionIds.Contains(selectedId.Value)
                    && context.CandidateQuestionPool.Any(item => item.QuestionId == selectedId.Value)
                    && !context.AskedQuestionIds.Contains(selectedId.Value);

                if (!validSelectedId)
                {
                    selectedId = context.CandidateQuestionPool
                        .Select(item => item.QuestionId)
                        .FirstOrDefault(id =>
                            activeCandidateQuestionIds.Contains(id)
                            && !context.AskedQuestionIds.Contains(id));
                    questionFallbackUsed = true;
                    questionStatus = TechnicalAITaskStatus.FallbackUsed;
                }

                if (!selectedId.HasValue || selectedId.Value <= 0)
                {
                    return Failure(
                        "NO_ACTIVE_NEXT_QUESTION",
                        results,
                        TechnicalAITaskStatus.Fulfilled,
                        feedback,
                        score,
                        resolvedDecision);
                }

                nextQuestion = new TechnicalArbiterNextQuestion(
                    TechnicalAttemptType.Main,
                    null,
                    Array.Empty<string>(),
                    selectedId.Value,
                    TechnicalQuestionGenerationReason.QuestionPlan,
                    context.NextPlanDeviation,
                    context.NextPlanDeviationReason);
            }

            var criticalPath = Math.Max(
                results.Evaluation.LatencyMs,
                resolvedDecision == TechnicalInterviewDecision.EndInterview
                    ? 0
                    : results.QuestionBundle.LatencyMs);

            return new TechnicalDecisionArbiterResult(
                true,
                null,
                resolvedDecision,
                finalizeMainQuestion,
                score,
                feedback,
                nextQuestion,
                results.Evaluation.Status,
                feedbackStatus,
                questionStatus,
                false,
                feedback.FallbackUsed,
                questionFallbackUsed,
                criticalPath,
                aiSuggestedAction,
                overrideReason,
                score.FinalOverallScore,
                appliedBonus,
                cumulativeBonus,
                finalMainQuestionScore,
                requiredClarificationCount,
                requiredFollowUpCount,
                adaptiveStage);
        }

        private static TechnicalMergedFeedback MergeFeedback(
            TechnicalAnswerProcessingContext context,
            TechnicalAIEvaluationResponse evaluation,
            TechnicalAITaskOutcome<TechnicalAIFeedbackDraftResponse> feedbackOutcome)
        {
            var strengths = SafeCleanList(evaluation.Strengths, context);
            var missingPoints = SafeCleanList(evaluation.MissingPoints, context);
            var incorrectClaims = SafeCleanList(evaluation.IncorrectClaims
                .Concat(evaluation.DimensionEvaluations.SelectMany(item => item.IncorrectClaims)), context);
            var improvements = SafeCleanList(evaluation.ImprovementSuggestions, context);
            var authoritative = strengths
                .Concat(missingPoints)
                .Concat(incorrectClaims)
                .Concat(improvements)
                .ToList();

            if (!feedbackOutcome.IsFulfilled)
            {
                return DeterministicFeedback(strengths, missingPoints, incorrectClaims, improvements);
            }

            var draft = feedbackOutcome.ProviderResult!.Data!;
            if (!IsValidDraft(draft)
                || !IsSafeFeedbackText(draft.Summary, context)
                || (authoritative.Count > 0 && !HasMeaningfulOverlap(draft.Summary, authoritative)))
            {
                return DeterministicFeedback(strengths, missingPoints, incorrectClaims, improvements);
            }

            var supportedDraftImprovements = CleanList(draft.ImprovementSuggestions)
                .Where(item => IsSafeFeedbackText(item, context))
                .Where(item => HasMeaningfulOverlap(item, missingPoints.Concat(incorrectClaims).Concat(improvements)))
                .ToList();

            return new TechnicalMergedFeedback(
                draft.Summary.Trim(),
                strengths,
                missingPoints,
                incorrectClaims,
                CleanList(improvements.Concat(supportedDraftImprovements)),
                false);
        }

        private static TechnicalMergedFeedback DeterministicFeedback(
            IReadOnlyList<string> strengths,
            IReadOnlyList<string> missingPoints,
            IReadOnlyList<string> incorrectClaims,
            IReadOnlyList<string> improvements)
        {
            var summary = missingPoints.Count > 0 || incorrectClaims.Count > 0
                ? "The answer was evaluated successfully and has specific areas that need stronger supporting evidence or correction."
                : "The answer was evaluated successfully against the configured technical rubric.";
            return new TechnicalMergedFeedback(
                summary,
                strengths,
                missingPoints,
                incorrectClaims,
                improvements,
                true);
        }

        private static bool IsValidDraft(TechnicalAIFeedbackDraftResponse draft)
        {
            return !string.IsNullOrWhiteSpace(draft.Summary)
                && draft.Summary.Length <= 1_000
                && new[]
                {
                    draft.Strengths,
                    draft.MissingPoints,
                    draft.IncorrectClaims,
                    draft.ImprovementSuggestions
                }.All(list => list.Count <= 20 && list.All(item =>
                    !string.IsNullOrWhiteSpace(item) && item.Length <= 1_000));
        }

        private static bool IsValidSubQuestion(
            TechnicalAISubQuestionCandidate? candidate,
            TechnicalAttemptType type,
            TechnicalAIEvaluationResponse evaluation,
            TechnicalRubricDefinition rubric,
            TechnicalAnswerProcessingContext context)
        {
            if (candidate is null
                || string.IsNullOrWhiteSpace(candidate.Content)
                || candidate.Content.Length > 2_000
                || string.IsNullOrWhiteSpace(candidate.Purpose)
                || candidate.Purpose.Length > 500
                || !IsSafeFeedbackText(candidate.Content, context))
            {
                return false;
            }

            var rubricCodes = rubric.Dimensions.Select(item => item.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var targetCodes = CleanCodes(candidate.TargetRubricCodes);
            if (targetCodes.Count == 0 || targetCodes.Any(code => !rubricCodes.Contains(code)))
            {
                return false;
            }

            if (type != TechnicalAttemptType.FollowUp)
            {
                return true;
            }

            var missingCodes = evaluation.DimensionEvaluations
                .Where(item => item.MissingEvidence.Count > 0)
                .Select(item => item.RubricCode)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return missingCodes.Count == 0 || targetCodes.Any(missingCodes.Contains);
        }

        private static IReadOnlyList<string> ResolveFallbackTargetCodes(
            TechnicalAIEvaluationResponse evaluation,
            TechnicalRubricDefinition rubric)
        {
            var missing = evaluation.DimensionEvaluations
                .Where(item => item.MissingEvidence.Count > 0)
                .OrderBy(item => item.SuggestedScore)
                .Select(item => item.RubricCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToList();
            if (missing.Count > 0)
            {
                return missing;
            }

            return evaluation.DimensionEvaluations
                .OrderBy(item => item.SuggestedScore)
                .Select(item => item.RubricCode)
                .Where(code => rubric.Dimensions.Any(dimension =>
                    string.Equals(dimension.Code, code, StringComparison.OrdinalIgnoreCase)))
                .Take(1)
                .ToList();
        }

        private static string BuildSubQuestionFallback(string language, TechnicalAttemptType type)
        {
            var vietnamese = string.Equals(language, "vi", StringComparison.OrdinalIgnoreCase);
            return type == TechnicalAttemptType.Clarification
                ? vietnamese
                    ? "Bạn có thể làm rõ cách bạn đi đến kết luận vừa nêu không?"
                    : "Could you clarify how you reached the conclusion in your answer?"
                : vietnamese
                    ? "Bạn có thể bổ sung một ví dụ thực tế hoặc phân tích trade-off cho câu trả lời vừa rồi không?"
                    : "Could you add a practical example or discuss a relevant trade-off for your answer?";
        }

        private static bool IsSafeFeedbackText(string value, TechnicalAnswerProcessingContext context)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalizedAnswer = Normalize(context.CandidateAnswer);
            return !ContainsUnstatedSecretPhrase(value, context.ExpectedAnswer, normalizedAnswer)
                && !ContainsUnstatedSecretPhrase(value, context.KeyPoints, normalizedAnswer);
        }

        private static bool ContainsUnstatedSecretPhrase(
            string output,
            string secret,
            string normalizedAnswer)
        {
            var outputWords = Words(output);
            var secretWords = Words(secret);
            if (secretWords.Count < 5 || outputWords.Count < 5)
            {
                return false;
            }

            var normalizedOutput = string.Join(' ', outputWords);
            for (var index = 0; index <= secretWords.Count - 5; index++)
            {
                var phrase = string.Join(' ', secretWords.Skip(index).Take(5));
                if (normalizedOutput.Contains(phrase, StringComparison.Ordinal)
                    && !normalizedAnswer.Contains(phrase, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasMeaningfulOverlap(string value, IEnumerable<string> references)
        {
            var tokens = Words(value).Where(word => word.Length >= 4).ToHashSet(StringComparer.Ordinal);
            return references.SelectMany(Words).Any(token => token.Length >= 4 && tokens.Contains(token));
        }

        private static List<string> Words(string value)
        {
            return Regex.Split(Normalize(value), @"[^\p{L}\p{N}]+")
                .Where(item => item.Length > 0)
                .ToList();
        }

        private static string Normalize(string value)
        {
            return value.Trim().ToLowerInvariant();
        }

        private static List<string> CleanList(IEnumerable<string>? values)
        {
            return values?
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList()
                ?? new List<string>();
        }

        private static List<string> SafeCleanList(
            IEnumerable<string>? values,
            TechnicalAnswerProcessingContext context)
        {
            return CleanList(values)
                .Where(item => IsSafeFeedbackText(item, context))
                .ToList();
        }

        private static List<string> CleanCodes(IEnumerable<string>? values)
        {
            return CleanList(values).Select(item => item.ToUpperInvariant()).ToList();
        }

        private static TechnicalDecisionArbiterResult Failure(
            string errorCode,
            TechnicalParallelAIResults results,
            TechnicalAITaskStatus evaluationStatus,
            TechnicalMergedFeedback? feedback = null,
            TechnicalQuestionScore? score = null,
            TechnicalInterviewDecision decision = TechnicalInterviewDecision.NextQuestion)
        {
            return new TechnicalDecisionArbiterResult(
                false,
                errorCode,
                decision,
                false,
                score,
                feedback,
                null,
                evaluationStatus,
                feedback?.FallbackUsed == true
                    ? TechnicalAITaskStatus.FallbackUsed
                    : results.Feedback.Status,
                results.QuestionBundle.Status,
                false,
                feedback?.FallbackUsed == true,
                false,
                results.Evaluation.LatencyMs,
                null,
                null,
                score?.FinalOverallScore ?? 0m,
                0m,
                0m,
                null,
                0,
                0,
                TechnicalAdaptiveStage.MainQuestion);
        }
    }
}
