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
        IReadOnlyList<string> TargetRubricCodes,
        TechnicalQuestionGenerationReason? GenerationReason = null);

    internal sealed record TechnicalAdaptiveRuleOutcome(
        TechnicalInterviewDecision Decision,
        bool FinalizeMainQuestion,
        TechnicalAttemptType? NextAttemptType,
        TechnicalQuestionGenerationReason? NextGenerationReason,
        int RequiredClarificationCount,
        int RequiredFollowUpCount,
        TechnicalAdaptiveStage Stage);

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
        string DecisionReason,
        string? OverrideReason,
        decimal RawScore,
        decimal AppliedBonus,
        decimal CumulativeFollowUpBonus,
        decimal? FinalMainQuestionScore,
        int RequiredClarificationCount,
        int RequiredFollowUpCount,
        TechnicalAdaptiveStage AdaptiveStage,
        TechnicalAIEvaluationResponse? EffectiveEvaluation);

    public interface ITechnicalInterviewDecisionArbiter
    {
        TechnicalDecisionArbiterResult Resolve(
            TechnicalAnswerProcessingContext context,
            TechnicalRubricDefinition rubric,
            TechnicalAnswerEvaluationResult results);
    }

    public sealed class TechnicalInterviewDecisionArbiter : ITechnicalInterviewDecisionArbiter
    {
        private readonly ITechnicalAIResponseValidator _validator;
        private readonly ITechnicalRubricScoringService _scoringService;
        private readonly ITechnicalFollowUpDecisionEngine _decisionEngine;
        private readonly ITechnicalFollowUpBonusCalculator _bonusCalculator;
        private readonly TechnicalInterviewOptions _options;

        public TechnicalInterviewDecisionArbiter(
            ITechnicalAIResponseValidator validator,
            ITechnicalRubricScoringService scoringService,
            ITechnicalFollowUpDecisionEngine decisionEngine,
            ITechnicalFollowUpBonusCalculator bonusCalculator,
            TechnicalInterviewOptions options)
        {
            _validator = validator;
            _scoringService = scoringService;
            _decisionEngine = decisionEngine;
            _bonusCalculator = bonusCalculator;
            _options = options;
        }

        public TechnicalDecisionArbiterResult Resolve(
            TechnicalAnswerProcessingContext context,
            TechnicalRubricDefinition rubric,
            TechnicalAnswerEvaluationResult results)
        {
            if (!results.Evaluation.IsFulfilled)
            {
                return Failure(
                    results.Evaluation.ErrorCode ?? "AI_EVALUATION_FAILED",
                    results,
                    results.Evaluation.Status);
            }

            var evaluationFallbackUsed = false;
            var evaluation = results.Evaluation.ProviderResult!.Data!;
            var answerContext = context.BuildCompleteAnswerContext();
            var evidenceGroundedByBackend = GroundMissingScoreEvidence(
                evaluation,
                rubric,
                answerContext);
            var validation = _validator.ValidateEvaluation(
                evaluation,
                rubric,
                answerContext);
            if (!validation.IsValid)
            {
                return Failure(
                    validation.ErrorCode ?? "EVALUATION_VALIDATION_FAILED",
                    results,
                    TechnicalAITaskStatus.InvalidOutput);
            }

            var score = _scoringService.ScoreQuestion(evaluation, rubric);
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
                var adaptive = ResolveAdaptiveRule(
                    context,
                    rubric,
                    evaluation,
                    score.FinalOverallScore);
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
                    ResolveRubricAction(
                        evaluation,
                        score.FinalOverallScore,
                        canClarify: true,
                        canFollowUp: true),
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

            var overrideReason = evidenceGroundedByBackend
                ? "EVIDENCE_GROUNDED_FROM_TRANSCRIPT"
                : context.IsReliabilityFollowUpRequired
                    && resolvedDecision == TechnicalInterviewDecision.FollowUp
                    ? "RELIABILITY_MINIMUM"
                : null;
            var decisionReason = ResolveDecisionReason(
                context,
                evaluation,
                score.FinalOverallScore,
                resolvedDecision,
                evaluationFallbackUsed);

            var feedback = EvaluationEvidence(evaluation);
            var feedbackStatus = TechnicalAITaskStatus.NotStarted;
            var questionStatus = TechnicalAITaskStatus.NotStarted;
            var questionFallbackUsed = false;
            TechnicalArbiterNextQuestion? nextQuestion = null;

            if (!finalizeMainQuestion)
            {
                nextQuestion = new TechnicalArbiterNextQuestion(
                    nextAttemptType,
                    ResolveTargetRubricCodes(evaluation, rubric),
                    nextGenerationReason);
            }
            else if (resolvedDecision == TechnicalInterviewDecision.NextQuestion)
            {
                nextQuestion = new TechnicalArbiterNextQuestion(
                    TechnicalAttemptType.Main,
                    Array.Empty<string>(),
                    TechnicalQuestionGenerationReason.QuestionPlan);
            }

            var criticalPath = Math.Max(
                results.Evaluation.LatencyMs,
                0);

            return new TechnicalDecisionArbiterResult(
                true,
                null,
                resolvedDecision,
                finalizeMainQuestion,
                score,
                feedback,
                nextQuestion,
                evaluationFallbackUsed
                    ? TechnicalAITaskStatus.FallbackUsed
                    : results.Evaluation.Status,
                feedbackStatus,
                questionStatus,
                evaluationFallbackUsed,
                false,
                questionFallbackUsed,
                criticalPath,
                null,
                decisionReason,
                overrideReason,
                score.FinalOverallScore,
                appliedBonus,
                cumulativeBonus,
                finalMainQuestionScore,
                requiredClarificationCount,
                requiredFollowUpCount,
                adaptiveStage,
                evaluation);
        }

        private TechnicalAdaptiveRuleOutcome ResolveAdaptiveRule(
            TechnicalAnswerProcessingContext context,
            TechnicalRubricDefinition rubric,
            TechnicalAIEvaluationResponse evaluation,
            decimal currentScore)
        {
            var projectedClarifications = context.CompletedClarificationCount
                + (context.AttemptType == TechnicalAttemptType.Clarification ? 1 : 0);
            var projectedFollowUps = context.CompletedFollowUpCount
                + (context.AttemptType == TechnicalAttemptType.FollowUp ? 1 : 0);
            var totalSubQuestions = projectedClarifications + projectedFollowUps;
            var canClarify = projectedClarifications < rubric.Limits.MaxClarificationsPerMainQuestion
                && totalSubQuestions < rubric.Limits.MaxTotalSubQuestionsPerMainQuestion;
            var canFollowUp = projectedFollowUps < rubric.Limits.MaxFollowUpsPerMainQuestion
                && totalSubQuestions < rubric.Limits.MaxTotalSubQuestionsPerMainQuestion;

            var action = TechnicalInterviewDecision.NextQuestion;
            var requiredClarifications = context.RequiredClarificationCount;
            var requiredFollowUps = context.RequiredFollowUpCount;
            var generationReason = TechnicalQuestionGenerationReason.AdaptiveScoreRule;

            if (context.AttemptType == TechnicalAttemptType.Main)
            {
                if (currentScore < 3m && canClarify)
                {
                    action = TechnicalInterviewDecision.Clarification;
                    requiredClarifications = 1;
                }
                else if (currentScore < 5m && canFollowUp)
                {
                    action = TechnicalInterviewDecision.FollowUp;
                    requiredFollowUps = Math.Min(2, rubric.Limits.MaxFollowUpsPerMainQuestion);
                }
                else if (currentScore < 8m && canFollowUp)
                {
                    action = TechnicalInterviewDecision.FollowUp;
                    requiredFollowUps = Math.Min(1, rubric.Limits.MaxFollowUpsPerMainQuestion);
                }
            }
            else if (context.AttemptType == TechnicalAttemptType.Clarification)
            {
                var recoveredScore = _scoringService.ApplyClarificationRecovery(
                    currentScore,
                    _options.ClarificationRecoveryFactor,
                    rubric);
                if (recoveredScore >= 3m && recoveredScore < 5m && canFollowUp)
                {
                    action = TechnicalInterviewDecision.FollowUp;
                    requiredFollowUps = Math.Min(2, rubric.Limits.MaxFollowUpsPerMainQuestion);
                }
                else if (recoveredScore >= 5m && recoveredScore < 8m && canFollowUp)
                {
                    action = TechnicalInterviewDecision.FollowUp;
                    requiredFollowUps = Math.Min(1, rubric.Limits.MaxFollowUpsPerMainQuestion);
                }
            }
            else if (projectedFollowUps < requiredFollowUps && canFollowUp)
            {
                action = TechnicalInterviewDecision.FollowUp;
            }

            // The reliability rule may add one bank-backed follow-up only after the
            // score-derived branch is otherwise complete.
            if (action == TechnicalInterviewDecision.NextQuestion
                && context.IsReliabilityFollowUpRequired
                && canFollowUp)
            {
                action = TechnicalInterviewDecision.FollowUp;
                requiredFollowUps = Math.Max(requiredFollowUps, projectedFollowUps + 1);
                generationReason = TechnicalQuestionGenerationReason.ReliabilityMinimum;
            }

            if (action == TechnicalInterviewDecision.Clarification)
            {
                return new TechnicalAdaptiveRuleOutcome(
                    action,
                    false,
                    TechnicalAttemptType.Clarification,
                    generationReason,
                    Math.Max(requiredClarifications, projectedClarifications + 1),
                    requiredFollowUps,
                    TechnicalAdaptiveStage.AwaitingClarification);
            }
            if (action == TechnicalInterviewDecision.FollowUp)
            {
                return new TechnicalAdaptiveRuleOutcome(
                    action,
                    false,
                    TechnicalAttemptType.FollowUp,
                    generationReason,
                    requiredClarifications,
                    Math.Max(requiredFollowUps, projectedFollowUps + 1),
                    generationReason == TechnicalQuestionGenerationReason.ReliabilityMinimum
                        ? TechnicalAdaptiveStage.AwaitingReliabilityFollowUp
                        : TechnicalAdaptiveStage.AwaitingFollowUp);
            }

            var finalDecision = context.CompletedMainQuestionCount + 1 >= context.TargetMainQuestionCount
                ? TechnicalInterviewDecision.EndInterview
                : TechnicalInterviewDecision.NextQuestion;
            return new TechnicalAdaptiveRuleOutcome(
                finalDecision,
                true,
                null,
                null,
                requiredClarifications,
                requiredFollowUps,
                TechnicalAdaptiveStage.Finalized);
        }

        private static TechnicalInterviewDecision ResolveRubricAction(
            TechnicalAIEvaluationResponse evaluation,
            decimal score,
            bool canClarify,
            bool canFollowUp)
        {
            var ambiguous = evaluation.Evaluation.AnswerQuality.Contains("AMBIG", StringComparison.OrdinalIgnoreCase)
                || evaluation.Evaluation.AnswerQuality.Contains("UNCLEAR", StringComparison.OrdinalIgnoreCase)
                || evaluation.Evaluation.AnswerQuality.Contains("NON_RESPONSIVE", StringComparison.OrdinalIgnoreCase)
                || evaluation.Evaluation.AnswerQuality.Contains("UNVERIFIED", StringComparison.OrdinalIgnoreCase);
            if (score < 3m && ambiguous && canClarify)
                return TechnicalInterviewDecision.Clarification;
            if (score < 5m && canFollowUp)
                return TechnicalInterviewDecision.FollowUp;
            if (score < 8m
                && canFollowUp
                && (evaluation.MissingPoints.Count > 0 || evaluation.IncorrectClaims.Count > 0))
            {
                return TechnicalInterviewDecision.FollowUp;
            }
            return TechnicalInterviewDecision.NextQuestion;
        }

        private static bool HasInsufficientEvidenceToFinalize(TechnicalAIEvaluationResponse evaluation)
        {
            return evaluation.Confidence < 0.35m
                || (evaluation.Evaluation.MissingPoints.Count > 0
                    && evaluation.DimensionEvaluations.All(item => item.Evidence.Count == 0));
        }

        private static string ResolveDecisionReason(
            TechnicalAnswerProcessingContext context,
            TechnicalAIEvaluationResponse evaluation,
            decimal score,
            TechnicalInterviewDecision decision,
            bool evaluationFallbackUsed)
        {
            if (context.IsReliabilityFollowUpRequired
                && decision == TechnicalInterviewDecision.FollowUp)
            {
                return "RUBRIC_RULE_RELIABILITY_MINIMUM";
            }
            if (decision == TechnicalInterviewDecision.Clarification)
            {
                return evaluationFallbackUsed
                    ? "RUBRIC_RULE_UNVERIFIED_ANSWER_CLARIFICATION"
                    : "RUBRIC_RULE_LOW_SCORE_AMBIGUOUS_ANSWER";
            }
            if (decision == TechnicalInterviewDecision.FollowUp)
            {
                if (score < 3m)
                    return "RUBRIC_RULE_LOW_SCORE_FOLLOW_UP";
                if (score < 5m)
                    return "RUBRIC_RULE_PARTIAL_ANSWER_FOLLOW_UP";
                return HasInsufficientEvidenceToFinalize(evaluation)
                    ? "RUBRIC_RULE_INSUFFICIENT_EVIDENCE_FOLLOW_UP"
                    : "RUBRIC_RULE_MISSING_OR_INCORRECT_POINT_FOLLOW_UP";
            }

            return "RUBRIC_RULE_ADVANCE_OR_CAPACITY_REACHED";
        }

        private static bool GroundMissingScoreEvidence(
            TechnicalAIEvaluationResponse evaluation,
            TechnicalRubricDefinition rubric,
            IReadOnlyList<TechnicalAnswerContext> answerContext)
        {
            var sourceAnswer = answerContext
                .Select(item => item.Answer?.Trim())
                .LastOrDefault(answer => !string.IsNullOrWhiteSpace(answer));
            if (string.IsNullOrWhiteSpace(sourceAnswer))
            {
                return false;
            }

            // Keep the backend-provided quote short while preserving an exact,
            // contiguous excerpt from the candidate transcript. The model's
            // score and reason are unchanged; this only supplies omitted audit
            // evidence so a valid evaluation is not rejected wholesale.
            const int maximumEvidenceLength = 300;
            var groundedQuote = sourceAnswer.Length <= maximumEvidenceLength
                ? sourceAnswer
                : sourceAnswer[..maximumEvidenceLength].TrimEnd();
            var repaired = false;
            foreach (var dimension in evaluation.DimensionEvaluations.Where(item =>
                item.SuggestedScore > rubric.EvidenceRequiredWhenScoreAbove
                && (item.Evidence is null || item.Evidence.Count == 0)))
            {
                dimension.Evidence = new List<string> { groundedQuote };
                repaired = true;
            }

            return repaired;
        }

        private static TechnicalMergedFeedback EvaluationEvidence(
            TechnicalAIEvaluationResponse evaluation)
        {
            return new TechnicalMergedFeedback(
                string.Empty,
                Array.Empty<string>(),
                CleanList(evaluation.MissingPoints).Take(3).ToList(),
                CleanList(evaluation.IncorrectClaims
                    .Concat(evaluation.DimensionEvaluations.SelectMany(item => item.IncorrectClaims))).Take(3).ToList(),
                Array.Empty<string>(),
                false);
        }

        private static IReadOnlyList<string> ResolveTargetRubricCodes(
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

        private static TechnicalDecisionArbiterResult Failure(
            string errorCode,
            TechnicalAnswerEvaluationResult results,
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
                TechnicalAITaskStatus.NotStarted,
                TechnicalAITaskStatus.NotStarted,
                false,
                feedback?.FallbackUsed == true,
                false,
                results.Evaluation.LatencyMs,
                null,
                "RUBRIC_RULE_RESOLUTION_FAILED",
                null,
                score?.FinalOverallScore ?? 0m,
                0m,
                0m,
                null,
                0,
                0,
                TechnicalAdaptiveStage.MainQuestion,
                null);
        }
    }
}
