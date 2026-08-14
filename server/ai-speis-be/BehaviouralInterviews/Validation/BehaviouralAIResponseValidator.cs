using System.Globalization;
using System.Text;
using System.Text.Json;
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
        string? ErrorCode)
    {
        public bool IsPartial { get; init; }
        public BehaviouralAIEvaluationResponse? NormalizedEvaluation { get; init; }
        public IReadOnlyList<string> InvalidCriterionCodes { get; init; } = Array.Empty<string>();
    }

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
            if (evaluation?.DimensionEvaluations is null || evaluation.DimensionEvaluations.Count == 0)
            {
                return Invalid("INVALID_BEHAVIOURAL_EVALUATION");
            }

            var expected = rubric.Dimensions.ToList();
            var dimensions = evaluation.DimensionEvaluations;
            var transcript = NormalizeEvidence(string.Join(" ", answerContext.Select(item => item.Answer)));
            var normalized = new List<BehaviouralAIDimensionEvaluation>(expected.Count);
            var invalidCodes = new List<string>();
            var recognizableDimensions = dimensions
                .Where(item => expected.Any(expectedDimension => IsRubricMatch(item.RubricCode, expectedDimension)))
                .ToList();

            if (recognizableDimensions.Count == 0)
            {
                return Invalid("INVALID_BEHAVIOURAL_DIMENSIONS");
            }

            var partial = recognizableDimensions.Count != dimensions.Count;
            string? firstError = partial ? "INVALID_BEHAVIOURAL_DIMENSIONS" : null;

            foreach (var rubricDimension in expected)
            {
                var matches = recognizableDimensions
                    .Where(item => IsRubricMatch(item.RubricCode, rubricDimension))
                    .ToList();
                if (matches.Count != 1)
                {
                    partial = true;
                    var errorCode = matches.Count == 0
                        ? "MISSING_BEHAVIOURAL_CRITERION"
                        : "INVALID_BEHAVIOURAL_DIMENSIONS";
                    firstError ??= errorCode;
                    invalidCodes.Add(rubricDimension.Code);
                    normalized.Add(BuildInvalidDimension(rubricDimension, errorCode));
                    continue;
                }

                var dimension = matches[0];
                dimension.RubricCode = rubricDimension.Code;
                var criterionError = ValidateCriterion(dimension, rubric, transcript);
                if (criterionError is not null)
                {
                    partial = true;
                    firstError ??= criterionError;
                    invalidCodes.Add(rubricDimension.Code);
                    normalized.Add(BuildInvalidDimension(rubricDimension, criterionError));
                    continue;
                }

                normalized.Add(dimension);
            }

            evaluation.DimensionEvaluations = normalized;
            return new BehaviouralEvaluationValidationResult(
                true,
                BehaviourResolvedAction.NextMainQuestion,
                firstError)
            {
                IsPartial = partial,
                NormalizedEvaluation = evaluation,
                InvalidCriterionCodes = invalidCodes
            };
        }

        private static bool IsRubricMatch(string? code, BehaviouralRubricDimension rubricDimension)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            var value = code.Trim();
            if (string.Equals(value, rubricDimension.Code, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, rubricDimension.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var normalized = value.ToUpperInvariant();
            return rubricDimension.Code.ToUpperInvariant() switch
            {
                "SITUATION_TASK" => normalized.Contains("SITUATION")
                    || normalized.Contains("CONTEXT")
                    || normalized.Contains("TASK"),
                "ACTION" => normalized.Contains("ACTION") || normalized.Contains("OWNERSHIP"),
                "RESULT" => normalized.Contains("RESULT") || normalized.Contains("REFLECTION"),
                "COMPETENCY" => normalized.Contains("COMPETENCY") || normalized.Contains("FIT"),
                "COMMUNICATION" => normalized.Contains("COMMUNICATION"),
                _ => false
            };
        }

        private static string? ValidateCriterion(
            BehaviouralAIDimensionEvaluation dimension,
            BehaviouralRubricDefinition rubric,
            string transcript)
        {
            if (dimension.SuggestedScore is null)
            {
                return "INVALID_BEHAVIOURAL_SCORE";
            }

            var roundedScore = Math.Round(dimension.SuggestedScore.Value, rubric.RoundingPrecision, MidpointRounding.AwayFromZero);
            if (roundedScore < rubric.MinimumScore || roundedScore > rubric.MaximumScore)
            {
                return "INVALID_BEHAVIOURAL_SCORE";
            }

            dimension.Evidence ??= new List<string>();
            dimension.MissingEvidence = NormalizeMissingEvidence(dimension.MissingEvidence);
            dimension.Evidence.RemoveAll(string.IsNullOrWhiteSpace);
            dimension.Evidence.RemoveAll(evidence => !IsGroundedEvidence(evidence, transcript));

            dimension.SuggestedScore = roundedScore;
            return null;
        }

        private static BehaviouralAIDimensionEvaluation BuildInvalidDimension(
            BehaviouralRubricDimension rubricDimension,
            string errorCode)
        {
            return new BehaviouralAIDimensionEvaluation
            {
                RubricCode = rubricDimension.Code,
                SuggestedScore = 0m,
                Evidence = new List<string>(),
                MissingEvidence = new List<string> { $"AI evaluation unavailable ({errorCode})." }
            };
        }

        private static BehaviouralEvaluationValidationResult Invalid(string errorCode)
        {
            return new BehaviouralEvaluationValidationResult(
                false,
                BehaviourResolvedAction.NextMainQuestion,
                errorCode);
        }

        private static bool HasPrecision(decimal value, int precision) =>
            decimal.Round(value, precision, MidpointRounding.AwayFromZero) == value;

        private static List<string> NormalizeMissingEvidence(IEnumerable<string>? items)
        {
            var normalized = new List<string>();
            foreach (var item in items ?? Array.Empty<string>())
            {
                var value = item?.Trim();
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (value.StartsWith("[", StringComparison.Ordinal) && value.EndsWith("]", StringComparison.Ordinal))
                {
                    try
                    {
                        normalized.AddRange(JsonSerializer.Deserialize<List<string>>(value) ?? new List<string>());
                    }
                    catch (JsonException)
                    {
                        normalized.AddRange(value[1..^1]
                            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(line => line.Trim().Trim('"', ',', ' '))
                            .Where(line => !string.IsNullOrWhiteSpace(line)));
                    }
                    continue;
                }

                normalized.Add(value);
            }

            return normalized
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();
        }

        private static bool IsGroundedEvidence(string evidence, string normalizedTranscript)
        {
            if (string.IsNullOrWhiteSpace(evidence) || evidence.Length > 1_000)
            {
                return false;
            }

            var normalizedEvidence = NormalizeEvidence(evidence);
            if (normalizedEvidence.Length == 0)
            {
                return false;
            }

            if (normalizedTranscript.Contains(normalizedEvidence, StringComparison.Ordinal))
            {
                return true;
            }

            var evidenceTokens = normalizedEvidence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var transcriptTokens = normalizedTranscript.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.Ordinal);
            var significantTokens = evidenceTokens
                .Where(token => token.Length > 2 && token is not "the" and not "and" and not "for" and not "with" and not "that" and not "this" and not "from")
                .ToList();
            if (significantTokens.Count == 0)
            {
                significantTokens = evidenceTokens.ToList();
            }

            return significantTokens.Count > 0
                && (double)significantTokens.Count(transcriptTokens.Contains) / significantTokens.Count >= 0.40;
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
