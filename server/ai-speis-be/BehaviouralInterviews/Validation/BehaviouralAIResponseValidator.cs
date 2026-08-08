using System.Globalization;
using System.Text;
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
            if (evaluation?.DimensionEvaluations == null || evaluation.DimensionEvaluations.Count == 0)
            {
                return Invalid("INVALID_RUBRIC_CODES");
            }

            var expectedDimensions = rubric.Dimensions.ToList();
            var expectedCodes = expectedDimensions
                .Select(item => item.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 1. Chuẩn hóa & ánh xạ RubricCode từ AI (Hỗ trợ match theo Code, Name, Keyword)
            foreach (var dim in evaluation.DimensionEvaluations)
            {
                if (string.IsNullOrWhiteSpace(dim.RubricCode)) continue;
                var rawCode = dim.RubricCode.Trim();

                // Direct match theo Code
                var matched = expectedDimensions.FirstOrDefault(d =>
                    string.Equals(d.Code, rawCode, StringComparison.OrdinalIgnoreCase));

                // Direct match theo Name
                matched ??= expectedDimensions.FirstOrDefault(d =>
                    string.Equals(d.Name, rawCode, StringComparison.OrdinalIgnoreCase));

                // Keyword match
                if (matched == null)
                {
                    var upper = rawCode.ToUpperInvariant();
                    if (upper.Contains("SITUATION") || upper.Contains("CONTEXT") || upper.Contains("TASK"))
                        matched = expectedDimensions.FirstOrDefault(d => d.Code.Equals("SITUATION_TASK", StringComparison.OrdinalIgnoreCase));
                    else if (upper.Contains("ACTION") || upper.Contains("OWNERSHIP"))
                        matched = expectedDimensions.FirstOrDefault(d => d.Code.Equals("ACTION", StringComparison.OrdinalIgnoreCase));
                    else if (upper.Contains("RESULT") || upper.Contains("REFLECTION"))
                        matched = expectedDimensions.FirstOrDefault(d => d.Code.Equals("RESULT", StringComparison.OrdinalIgnoreCase));
                    else if (upper.Contains("COMPETENCY") || upper.Contains("FIT"))
                        matched = expectedDimensions.FirstOrDefault(d => d.Code.Equals("COMPETENCY", StringComparison.OrdinalIgnoreCase));
                    else if (upper.Contains("COMMUNICATION"))
                        matched = expectedDimensions.FirstOrDefault(d => d.Code.Equals("COMMUNICATION", StringComparison.OrdinalIgnoreCase));
                }

                if (matched != null)
                {
                    dim.RubricCode = matched.Code;
                }
            }

            // 2. Khử trùng lặp & Tự động bổ sung các dimension bị thiếu (fallback an toàn cho dimension đó)
            var existingByCode = evaluation.DimensionEvaluations
                .Where(d => expectedCodes.Contains(d.RubricCode))
                .GroupBy(d => d.RubricCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var normalizedEvaluations = new List<BehaviouralAIDimensionEvaluation>();
            foreach (var dim in expectedDimensions)
            {
                if (existingByCode.TryGetValue(dim.Code, out var existing))
                {
                    normalizedEvaluations.Add(existing);
                }
                else
                {
                    normalizedEvaluations.Add(new BehaviouralAIDimensionEvaluation
                    {
                        RubricCode = dim.Code,
                        SuggestedScore = 0m,
                        Evidence = new List<string>(),
                        MissingEvidence = new List<string> { dim.Name }
                    });
                }
            }

            evaluation.DimensionEvaluations = normalizedEvaluations;

            var actualCodes = evaluation.DimensionEvaluations
                .Select(item => item.RubricCode)
                .ToList();

            if (actualCodes.Count != expectedCodes.Count
                || actualCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != actualCodes.Count
                || actualCodes.Any(code => !expectedCodes.Contains(code)))
            {
                return Invalid("INVALID_RUBRIC_CODES");
            }

            var transcript = NormalizeEvidence(string.Join(" ", answerContext.Select(item => item.Answer)));

            foreach (var dimension in evaluation.DimensionEvaluations)
            {
                if (dimension.SuggestedScore < rubric.MinimumScore
                    || dimension.SuggestedScore > rubric.MaximumScore)
                {
                    dimension.SuggestedScore = 0m;
                    dimension.Evidence.Clear();
                    dimension.MissingEvidence ??= new List<string>();
                    dimension.MissingEvidence.Add("Invalid score returned by AI.");
                }

                if (dimension.Evidence.Any(evidence =>
                    !string.IsNullOrWhiteSpace(evidence)
                    && !IsGroundedEvidence(evidence, transcript)))
                {
                    // Filter out non-verbatim dimension evidence snippets instead of failing whole score
                    dimension.Evidence.RemoveAll(evidence => string.IsNullOrWhiteSpace(evidence) || !IsGroundedEvidence(evidence, transcript));
                }
            }

            return new BehaviouralEvaluationValidationResult(true, BehaviourResolvedAction.NextMainQuestion, null);
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

        private static bool IsGroundedEvidence(string evidence, string normalizedTranscript)
        {
            if (string.IsNullOrWhiteSpace(evidence) || evidence.Length > 1_000)
            {
                return false;
            }

            var normalizedEvidence = NormalizeEvidence(evidence);
            return normalizedEvidence.Length > 0
                && normalizedTranscript.Contains(normalizedEvidence, StringComparison.Ordinal);
        }

        private static string NormalizeEvidence(string value)
        {
            var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            var previousWasSeparator = true;

            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                var normalizedCharacter = character is 'đ' or 'Đ' ? 'd' : character;
                if (char.IsLetterOrDigit(normalizedCharacter))
                {
                    builder.Append(normalizedCharacter);
                    previousWasSeparator = false;
                }
                else if (!previousWasSeparator)
                {
                    builder.Append(' ');
                    previousWasSeparator = true;
                }
            }

            return Regex.Replace(builder.ToString().Trim(), @"\s+", " ");
        }
    }
}

