using ai_speis_be.Models.Enums;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.TechnicalInterviews.Rubrics;
using ai_speis_be.TechnicalInterviews.Scoring;
using ai_speis_be.TechnicalInterviews.Validation;

namespace ai_speis_be.TechnicalInterviews.Orchestration
{
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
        TechnicalArbiterNextQuestion? NextQuestion,
        TechnicalAITaskStatus EvaluationStatus,
        TechnicalAITaskStatus QuestionStatus,
        bool EvaluationFallbackUsed,
        bool QuestionFallbackUsed,
        long CriticalPathLatencyMs,
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
            TechnicalAnswerEvaluationProcessingResult results);
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
            TechnicalAnswerEvaluationProcessingResult results)
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
                nextQuestion,
                evaluationFallbackUsed
                    ? TechnicalAITaskStatus.FallbackUsed
                    : results.Evaluation.Status,
                questionStatus,
                evaluationFallbackUsed,
                questionFallbackUsed,
                criticalPath,
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

            var thresholdScore = context.AttemptType switch
            {
                TechnicalAttemptType.FollowUp => context.CurrentMainBaseScore,
                _ => currentScore
            };
            var desiredFollowUps = thresholdScore switch
            {
                < 3m => 0,
                < 5m => 2,
                < 8m => 1,
                _ => 0
            };
            var requiredClarificationCount = context.RequiredClarificationCount;
            var requiredFollowUpCount = context.RequiredFollowUpCount > 0
                ? context.RequiredFollowUpCount
                : desiredFollowUps;
            var action = thresholdScore < 3m
                ? context.AttemptType == TechnicalAttemptType.Main && canClarify
                    ? TechnicalInterviewDecision.Clarification
                    : TechnicalInterviewDecision.NextQuestion
                : projectedFollowUps < requiredFollowUpCount && canFollowUp
                    ? TechnicalInterviewDecision.FollowUp
                    : TechnicalInterviewDecision.NextQuestion;
            var generationReason = TechnicalQuestionGenerationReason.AdaptiveScoreRule;

            // Reliability is a hard completion constraint. It may only force a
            // content-grounded follow-up while capacity remains.
            if (context.IsReliabilityFollowUpRequired && canFollowUp)
            {
                action = TechnicalInterviewDecision.FollowUp;
                generationReason = TechnicalQuestionGenerationReason.ReliabilityMinimum;
                requiredFollowUpCount = Math.Max(requiredFollowUpCount, projectedFollowUps + 1);
            }

            if (action == TechnicalInterviewDecision.Clarification
                && (!canClarify || context.AttemptType == TechnicalAttemptType.Clarification))
            {
                action = canFollowUp
                    ? TechnicalInterviewDecision.FollowUp
                    : TechnicalInterviewDecision.NextQuestion;
            }
            if (action == TechnicalInterviewDecision.FollowUp && !canFollowUp)
            {
                action = TechnicalInterviewDecision.NextQuestion;
            }

            if (action == TechnicalInterviewDecision.Clarification)
            {
                return new TechnicalAdaptiveRuleOutcome(
                    action,
                    false,
                    TechnicalAttemptType.Clarification,
                    generationReason,
                    Math.Max(requiredClarificationCount, projectedClarifications + 1),
                    requiredFollowUpCount,
                    TechnicalAdaptiveStage.AwaitingClarification);
            }
            if (action == TechnicalInterviewDecision.FollowUp)
            {
                return new TechnicalAdaptiveRuleOutcome(
                    action,
                    false,
                    TechnicalAttemptType.FollowUp,
                    generationReason,
                    requiredClarificationCount,
                    requiredFollowUpCount,
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
                requiredClarificationCount,
                requiredFollowUpCount,
                TechnicalAdaptiveStage.Finalized);
        }

        private static TechnicalInterviewDecision ResolveRubricAction(
            TechnicalAIEvaluationResponse evaluation,
            decimal score,
            bool canClarify,
            bool canFollowUp)
        {
            var ambiguous = score < 3m && evaluation.DimensionEvaluations.All(item => item.Evidence.Count == 0);
            if (score < 3m && ambiguous && canClarify)
                return TechnicalInterviewDecision.Clarification;
            if (score < 5m && canFollowUp)
                return TechnicalInterviewDecision.FollowUp;
            if (score < 8m
                && canFollowUp
                && evaluation.DimensionEvaluations.Any(item => item.MissingEvidence.Count > 0))
            {
                return TechnicalInterviewDecision.FollowUp;
            }
            return TechnicalInterviewDecision.NextQuestion;
        }

        private static bool HasInsufficientEvidenceToFinalize(TechnicalAIEvaluationResponse evaluation)
        {
            return evaluation.DimensionEvaluations.Any(item => item.MissingEvidence.Count > 0)
                && evaluation.DimensionEvaluations.All(item => item.Evidence.Count == 0);
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
                    : "RUBRIC_RULE_LOW_SCORE_CLARIFICATION";
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

        private static TechnicalDecisionArbiterResult Failure(
            string errorCode,
            TechnicalAnswerEvaluationProcessingResult results,
            TechnicalAITaskStatus evaluationStatus,
            TechnicalQuestionScore? score = null,
            TechnicalInterviewDecision decision = TechnicalInterviewDecision.NextQuestion)
        {
            return new TechnicalDecisionArbiterResult(
                false,
                errorCode,
                decision,
                false,
                score,
                null,
                evaluationStatus,
                TechnicalAITaskStatus.NotStarted,
                false,
                false,
                results.Evaluation.LatencyMs,
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
