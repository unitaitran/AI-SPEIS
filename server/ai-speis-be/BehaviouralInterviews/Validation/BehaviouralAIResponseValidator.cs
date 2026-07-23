using System.Text.RegularExpressions;
using ai_speis_be.Models.Enums;
using ai_speis_be.BehaviouralInterviews.AI;
using ai_speis_be.BehaviouralInterviews.Rubrics;

namespace ai_speis_be.BehaviouralInterviews.Validation
{
    public sealed record BehaviouralSelectionValidationResult(
        bool IsValid,
        string? ErrorCode);

    public sealed record BehaviouralEvaluationValidationResult(
        bool IsValid,
        BehaviourResolvedAction Decision,
        string? ErrorCode);

    public interface IBehaviouralAIResponseValidator
    {
        BehaviouralSelectionValidationResult ValidateSelection(
            BehaviouralAISelectionResponse selection,
            BehaviouralAISelectionConstraints constraints,
            IReadOnlyDictionary<int, string?> candidateSkillsById);

        BehaviouralEvaluationValidationResult ValidateEvaluation(
            BehaviouralAIEvaluationResponse evaluation,
            BehaviouralRubricDefinition rubric,
            IReadOnlyList<BehaviouralAnswerContext> answerContext);
    }

    public sealed class BehaviouralAIResponseValidator : IBehaviouralAIResponseValidator
    {
        public BehaviouralSelectionValidationResult ValidateSelection(
            BehaviouralAISelectionResponse selection,
            BehaviouralAISelectionConstraints constraints,
            IReadOnlyDictionary<int, string?> candidateSkillsById)
        {
            var selected = selection.SelectedQuestions;
            if (selected.Count != constraints.RequiredQuestionCount)
            {
                return new BehaviouralSelectionValidationResult(false, "WRONG_QUESTION_COUNT");
            }

            if (selected.Select(item => item.QuestionId).Distinct().Count() != selected.Count)
            {
                return new BehaviouralSelectionValidationResult(false, "DUPLICATE_QUESTION_ID");
            }

            if (selected.Any(item => !candidateSkillsById.ContainsKey(item.QuestionId)))
            {
                return new BehaviouralSelectionValidationResult(false, "QUESTION_NOT_IN_POOL");
            }

            var skills = selected
                .Select(item => candidateSkillsById[item.QuestionId])
                .Where(skill => !string.IsNullOrWhiteSpace(skill))
                .Select(skill => skill!.Trim())
                .ToList();

            var skillCounts = skills.GroupBy(skill => skill, StringComparer.OrdinalIgnoreCase);
            if (constraints.MaximumQuestionsPerSkill > 0
                && skillCounts.Any(group => group.Count() > constraints.MaximumQuestionsPerSkill))
            {
                return new BehaviouralSelectionValidationResult(false, "SKILL_LIMIT_EXCEEDED");
            }

            var distinctSkillCount = skills.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var poolDistinctSkillCount = candidateSkillsById.Values
                .Where(skill => !string.IsNullOrWhiteSpace(skill))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            // Chỉ ép minimum coverage khi pool thực sự có đủ skill đa dạng.
            var requiredCoverage = Math.Min(constraints.MinimumCoveredSkills, poolDistinctSkillCount);
            if (distinctSkillCount < requiredCoverage)
            {
                return new BehaviouralSelectionValidationResult(false, "INSUFFICIENT_SKILL_COVERAGE");
            }

            return new BehaviouralSelectionValidationResult(true, null);
        }

        public BehaviouralEvaluationValidationResult ValidateEvaluation(
            BehaviouralAIEvaluationResponse evaluation,
            BehaviouralRubricDefinition rubric,
            IReadOnlyList<BehaviouralAnswerContext> answerContext)
        {
            if (!TryParseDecision(evaluation.Decision, out var decision))
            {
                return Invalid("INVALID_DECISION");
            }

            if (evaluation.Confidence is < 0m or > 1m)
            {
                return Invalid("INVALID_CONFIDENCE");
            }

            var allowedStatuses = new[] { "EXCELLENT", "GOOD", "PARTIAL", "VAGUE", "INSUFFICIENT" };
            if (!allowedStatuses.Contains(evaluation.AnswerQuality?.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                return Invalid("INVALID_ANSWER_STATUS");
            }

            if (evaluation.OverallRubricScore < (decimal)rubric.MinimumScore
                || evaluation.OverallRubricScore > (decimal)rubric.MaximumScore)
            {
                return Invalid("INVALID_OVERALL_RUBRIC_SCORE");
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
            if (evaluation.Evidence.Count > 3 || evaluation.MissingAspects.Count > 3)
            {
                return Invalid("EVALUATION_DETAIL_LIMIT_EXCEEDED");
            }
            if (evaluation.Evidence.Any(evidence =>
                string.IsNullOrWhiteSpace(evidence)
                || evidence.Length > 300
                || !transcript.Contains(Normalize(evidence), StringComparison.Ordinal)))
            {
                return Invalid("EVIDENCE_NOT_IN_ANSWER");
            }
            if (evaluation.MissingAspects.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 300))
            {
                return Invalid("INVALID_MISSING_ASPECT");
            }
            foreach (var dimension in evaluation.DimensionEvaluations)
            {
                if (dimension.SuggestedScore < rubric.MinimumScore
                    || dimension.SuggestedScore > rubric.MaximumScore)
                {
                    return Invalid("SCORE_OUT_OF_RANGE");
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

            return new BehaviouralEvaluationValidationResult(true, decision, null);
        }

        private static bool TryParseDecision(string value, out BehaviourResolvedAction decision)
        {
            var normalized = value?.Trim().Replace("_", string.Empty, StringComparison.Ordinal) ?? string.Empty;
            // "NEXT_MAIN_QUESTION" -> "NEXTMAINQUESTION", "FOLLOW_UP_1" -> "FOLLOWUP1"
            return Enum.TryParse(normalized, true, out decision)
                && Enum.IsDefined(decision);
        }

        private static BehaviouralEvaluationValidationResult Invalid(string errorCode)
        {
            return new BehaviouralEvaluationValidationResult(
                false,
                BehaviourResolvedAction.NextMainQuestion,
                errorCode);
        }

        private static string Normalize(string value)
        {
            return Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ");
        }
    }
}
