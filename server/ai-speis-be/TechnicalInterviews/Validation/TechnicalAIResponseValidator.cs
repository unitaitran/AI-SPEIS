using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Rubrics;

namespace ai_speis_be.TechnicalInterviews.Validation
{
    public sealed record TechnicalSelectionValidationResult(
        bool IsValid,
        string? ErrorCode);

    public sealed record TechnicalEvaluationValidationResult(
        bool IsValid,
        string? ErrorCode)
    {
        public bool IsPartial { get; init; }
        public TechnicalV2EvaluationResponse? NormalizedEvaluation { get; init; }
        public IReadOnlyList<string> InvalidCriterionCodes { get; init; } = Array.Empty<string>();
    }

    public interface ITechnicalAIResponseValidator
    {
        bool IsValidSelection(int selectedQuestionId, IReadOnlySet<int> candidateIds);

        TechnicalSelectionValidationResult ValidateSelection(
            TechnicalAISelectionResponse selection,
            TechnicalAISelectionConstraints constraints,
            IReadOnlySet<int> poolQuestionIds);

        TechnicalEvaluationValidationResult ValidateEvaluationV2(
            TechnicalV2EvaluationResponse evaluation,
            TechnicalRubricDefinition rubric,
            IReadOnlyList<TechnicalAnswerContext> answerContext);
    }

    public sealed class TechnicalAIResponseValidator : ITechnicalAIResponseValidator
    {
        public bool IsValidSelection(int selectedQuestionId, IReadOnlySet<int> candidateIds)
        {
            return selectedQuestionId > 0 && candidateIds.Contains(selectedQuestionId);
        }

        public TechnicalSelectionValidationResult ValidateSelection(
            TechnicalAISelectionResponse selection,
            TechnicalAISelectionConstraints constraints,
            IReadOnlySet<int> poolQuestionIds)
        {
            var selected = selection.SelectedQuestions;
            if (selected.Count != constraints.RequiredQuestionCount)
            {
                return new TechnicalSelectionValidationResult(false, "WRONG_QUESTION_COUNT");
            }

            if (selected.Select(item => item.QuestionId).Distinct().Count() != selected.Count)
            {
                return new TechnicalSelectionValidationResult(false, "DUPLICATE_QUESTION_ID");
            }

            if (selected.Any(item => !poolQuestionIds.Contains(item.QuestionId)))
            {
                return new TechnicalSelectionValidationResult(false, "QUESTION_NOT_IN_POOL");
            }

            return new TechnicalSelectionValidationResult(true, null);
        }

        public TechnicalEvaluationValidationResult ValidateEvaluationV2(
            TechnicalV2EvaluationResponse evaluation,
            TechnicalRubricDefinition rubric,
            IReadOnlyList<TechnicalAnswerContext> answerContext)
        {
            if (evaluation?.Evaluation is null
                || evaluation.Evaluation.DimensionEvaluations is null
                || evaluation.Evaluation.DimensionEvaluations.Count == 0)
            {
                return Invalid("INVALID_V2_EVALUATION");
            }

            var expected = rubric.Dimensions.ToList();
            var dimensions = evaluation.Evaluation.DimensionEvaluations;
            var transcript = NormalizeEvidence(string.Join(" ", answerContext.Select(item => item.Answer)));
            var normalized = new List<TechnicalV2DimensionEvaluation>(expected.Count);
            var invalidCodes = new List<string>();
            var recognizableDimensions = dimensions
                .Where(item => expected.Any(expectedDimension => IsRubricMatch(item.RubricCode, expectedDimension)))
                .ToList();
            if (recognizableDimensions.Count == 0)
            {
                return Invalid("INVALID_V2_DIMENSIONS");
            }

            var partial = false;
            string? firstError = null;
            if (recognizableDimensions.Count != dimensions.Count)
            {
                partial = true;
                firstError ??= "INVALID_V2_DIMENSIONS";
            }

            foreach (var rubricDimension in expected)
            {
                var matches = recognizableDimensions
                    .Where(item => IsRubricMatch(item.RubricCode, rubricDimension))
                    .ToList();
                if (matches.Count != 1)
                {
                    partial = true;
                    var errorCode = matches.Count == 0 ? "MISSING_V2_CRITERION" : "INVALID_V2_DIMENSIONS";
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

            evaluation.Evaluation.DimensionEvaluations = normalized;

            return new TechnicalEvaluationValidationResult(true, firstError)
            {
                IsPartial = partial,
                NormalizedEvaluation = evaluation,
                InvalidCriterionCodes = invalidCodes
            };
        }

        private static bool IsRubricMatch(string? code, TechnicalRubricDimension rubricDimension)
        {
            return !string.IsNullOrWhiteSpace(code)
                && (string.Equals(code.Trim(), rubricDimension.Code, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(code.Trim(), rubricDimension.Name, StringComparison.OrdinalIgnoreCase));
        }

        private static string? ValidateCriterion(
            TechnicalV2DimensionEvaluation dimension,
            TechnicalRubricDefinition rubric,
            string transcript)
        {
            if (dimension.SuggestedScore is null
                || dimension.SuggestedScore < rubric.MinimumScore
                || dimension.SuggestedScore > rubric.MaximumScore)
            {
                return "INVALID_V2_SCORE";
            }
            if (!HasPrecision(dimension.SuggestedScore.Value, rubric.RoundingPrecision))
            {
                return "INVALID_V2_SCORE";
            }

            // Mirror behavioral evaluation: evidence supports the score, but an
            // unusable quote must never erase an otherwise valid assessment.
            dimension.Evidence ??= new List<string>();
            dimension.MissingEvidence ??= new List<string>();
            dimension.Evidence.RemoveAll(string.IsNullOrWhiteSpace);
            dimension.MissingEvidence.RemoveAll(string.IsNullOrWhiteSpace);
            dimension.Evidence.RemoveAll(evidence => !IsGroundedEvidence(evidence, transcript));

            return null;
        }

        private static TechnicalV2DimensionEvaluation BuildInvalidDimension(
            TechnicalRubricDimension rubricDimension,
            string errorCode)
        {
            return new TechnicalV2DimensionEvaluation
            {
                RubricCode = rubricDimension.Code,
                SuggestedScore = 0m,
                Evidence = new List<string>(),
                MissingEvidence = new List<string> { $"AI evaluation unavailable ({errorCode})." }
            };
        }


        private static TechnicalEvaluationValidationResult Invalid(string errorCode)
        {
            return new TechnicalEvaluationValidationResult(
                false,
                errorCode);
        }

        private static bool HasPrecision(decimal value, int precision) =>
            decimal.Round(value, precision, MidpointRounding.AwayFromZero) == value;

        private static bool IsGroundedEvidence(string evidence, string normalizedTranscript)
        {
            if (string.IsNullOrWhiteSpace(evidence) || evidence.Length > 1_000)
                return false;

            var normalizedEvidence = NormalizeEvidence(evidence);
            if (normalizedEvidence.Length == 0)
                return false;
            if (normalizedTranscript.Contains(normalizedEvidence, StringComparison.Ordinal))
                return true;

            var evidenceTokens = normalizedEvidence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var transcriptTokens = normalizedTranscript.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.Ordinal);
            var significantTokens = evidenceTokens
                .Where(token => token.Length > 2 && token is not "the" and not "and" and not "for" and not "with" and not "that" and not "this" and not "from")
                .ToList();
            if (significantTokens.Count == 0)
                significantTokens = evidenceTokens.ToList();
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
                    continue;
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
