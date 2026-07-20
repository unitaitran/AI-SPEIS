using System.Text.RegularExpressions;
using ai_speis_be.Models.Enums;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Rubrics;

namespace ai_speis_be.TechnicalInterviews.Validation
{
    public sealed record TechnicalEvaluationValidationResult(
        bool IsValid,
        TechnicalInterviewDecision Decision,
        string? ErrorCode);

    public interface ITechnicalAIResponseValidator
    {
        bool IsValidSelection(int selectedQuestionId, IReadOnlySet<int> candidateIds);

        TechnicalEvaluationValidationResult ValidateEvaluation(
            TechnicalAIEvaluationResponse evaluation,
            TechnicalRubricDefinition rubric,
            IReadOnlyList<TechnicalAnswerContext> answerContext);
    }

    public sealed class TechnicalAIResponseValidator : ITechnicalAIResponseValidator
    {
        public bool IsValidSelection(int selectedQuestionId, IReadOnlySet<int> candidateIds)
        {
            return selectedQuestionId > 0 && candidateIds.Contains(selectedQuestionId);
        }

        public TechnicalEvaluationValidationResult ValidateEvaluation(
            TechnicalAIEvaluationResponse evaluation,
            TechnicalRubricDefinition rubric,
            IReadOnlyList<TechnicalAnswerContext> answerContext)
        {
            if (!TryParseDecision(evaluation.Decision, out var decision))
            {
                return Invalid("INVALID_DECISION");
            }

            if (evaluation.Confidence is < 0m or > 1m)
            {
                return Invalid("INVALID_CONFIDENCE");
            }

            var expectedCodes = rubric.Dimensions
                .Select(item => item.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var actualCodes = evaluation.DimensionEvaluations
                .Select(item => item.RubricCode)
                .ToList();

            if (actualCodes.Count != expectedCodes.Count
                || actualCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != actualCodes.Count
                || actualCodes.Any(code => !expectedCodes.Contains(code)))
            {
                return Invalid("INVALID_RUBRIC_CODES");
            }

            var transcript = Normalize(string.Join(" ", answerContext.Select(item => item.Answer)));
            foreach (var dimension in evaluation.DimensionEvaluations)
            {
                if (dimension.SuggestedScore < rubric.MinimumScore
                    || dimension.SuggestedScore > rubric.MaximumScore)
                {
                    return Invalid("SCORE_OUT_OF_RANGE");
                }

                if (!string.Equals(
                    dimension.SuggestedLevel,
                    rubric.GetLevelCode(dimension.SuggestedScore),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return Invalid("LEVEL_SCORE_MISMATCH");
                }

                if (dimension.SuggestedScore > rubric.EvidenceRequiredWhenScoreAbove
                    && dimension.Evidence.Count == 0)
                {
                    return Invalid("MISSING_SCORE_EVIDENCE");
                }

                if (dimension.Evidence.Any(evidence =>
                    string.IsNullOrWhiteSpace(evidence)
                    || evidence.Length > 1_000
                    || !transcript.Contains(Normalize(evidence), StringComparison.Ordinal)))
                {
                    return Invalid("EVIDENCE_NOT_IN_ANSWER");
                }

                if (string.IsNullOrWhiteSpace(dimension.ReasonSummary)
                    || dimension.ReasonSummary.Length > 1_000)
                {
                    return Invalid("INVALID_REASON_SUMMARY");
                }
            }

            return new TechnicalEvaluationValidationResult(true, decision, null);
        }

        private static bool TryParseDecision(string value, out TechnicalInterviewDecision decision)
        {
            var normalized = value?.Trim().Replace("_", string.Empty, StringComparison.Ordinal) ?? string.Empty;
            return Enum.TryParse(normalized, true, out decision)
                && Enum.IsDefined(decision);
        }

        private static TechnicalEvaluationValidationResult Invalid(string errorCode)
        {
            return new TechnicalEvaluationValidationResult(
                false,
                TechnicalInterviewDecision.NextQuestion,
                errorCode);
        }

        private static string Normalize(string value)
        {
            return Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ");
        }
    }
}
