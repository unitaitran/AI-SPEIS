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
            if (!TryParseRecommendedAction(evaluation.RecommendedAction, out var decision))
            {
                return Invalid("INVALID_DECISION");
            }

            if (evaluation.Confidence is < 0m or > 1m)
            {
                return Invalid("INVALID_CONFIDENCE");
            }

            var answerStatus = evaluation.AnswerStatus?.Trim().ToUpperInvariant();
            if (answerStatus is not ("INSUFFICIENT" or "PARTIAL" or "ACCEPTABLE" or "STRONG"))
            {
                return Invalid("INVALID_ANSWER_STATUS");
            }

            if (evaluation.Evidence.Count > 3 || evaluation.MissingAspects.Count > 3)
                return Invalid("EVALUATION_LIST_LIMIT_EXCEEDED");

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
                if (dimension.Evidence.Count > 3 || dimension.MissingEvidence.Count > 3)
                {
                    return Invalid("DIMENSION_LIST_LIMIT_EXCEEDED");
                }
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

        private static bool TryParseRecommendedAction(string value, out BehaviourResolvedAction decision)
        {
            var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
            decision = normalized switch
            {
                "CLARIFICATION" => BehaviourResolvedAction.Clarification,
                "FOLLOW_UP" => BehaviourResolvedAction.FollowUp1,
                "NEXT_MAIN" or "COMPLETE_ROUND" => BehaviourResolvedAction.NextMainQuestion,
                _ => BehaviourResolvedAction.NextMainQuestion
            };
            return normalized is "CLARIFICATION" or "FOLLOW_UP" or "NEXT_MAIN" or "COMPLETE_ROUND";
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
