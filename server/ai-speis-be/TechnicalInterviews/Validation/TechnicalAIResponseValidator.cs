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

        TechnicalEvaluationValidationResult ValidateEvaluation(
            TechnicalAIEvaluationResponse evaluation,
            TechnicalRubricDefinition rubric,
            IReadOnlyList<TechnicalAnswerContext> answerContext);

        TechnicalEvaluationValidationResult ValidateEvaluationV2(
            TechnicalV2EvaluationResponse evaluation,
            TechnicalRubricDefinition rubric,
            IReadOnlyList<TechnicalAnswerContext> answerContext);
    }

    public sealed class TechnicalAIResponseValidator : ITechnicalAIResponseValidator
    {
        private static readonly HashSet<string> AnswerQualities = new(
            new[] { "COMPLETE", "PARTIAL", "AMBIGUOUS", "NON_RESPONSIVE", "INCORRECT", "UNVERIFIED" },
            StringComparer.OrdinalIgnoreCase);

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

        public TechnicalEvaluationValidationResult ValidateEvaluation(
            TechnicalAIEvaluationResponse evaluation,
            TechnicalRubricDefinition rubric,
            IReadOnlyList<TechnicalAnswerContext> answerContext)
        {
            if (evaluation?.DimensionEvaluations == null || evaluation.DimensionEvaluations.Count == 0)
            {
                return Invalid("INVALID_RUBRIC_CODES");
            }

            var expectedDimensions = rubric.Dimensions.ToList();
            var expectedCodes = expectedDimensions
                .Select(item => item.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 1. Chuẩn hóa & ánh xạ RubricCode từ AI
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

                if (matched != null)
                {
                    dim.RubricCode = matched.Code;
                }
            }

            // 2. Khử trùng lặp & Tự động bổ sung các dimension bị thiếu
            var existingByCode = evaluation.DimensionEvaluations
                .Where(d => expectedCodes.Contains(d.RubricCode))
                .GroupBy(d => d.RubricCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var normalizedEvaluations = new List<TechnicalAIDimensionEvaluation>();
            foreach (var dim in expectedDimensions)
            {
                if (existingByCode.TryGetValue(dim.Code, out var existing))
                {
                    normalizedEvaluations.Add(existing);
                }
                else
                {
                    normalizedEvaluations.Add(new TechnicalAIDimensionEvaluation
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

                if (dimension.SuggestedScore > rubric.EvidenceRequiredWhenScoreAbove
                    && dimension.Evidence.Count == 0)
                {
                    dimension.SuggestedScore = 0m;
                    dimension.MissingEvidence.Add("No grounded evidence returned by AI.");
                }

                if (dimension.Evidence.Any(evidence =>
                    !string.IsNullOrWhiteSpace(evidence)
                    && !IsGroundedEvidence(evidence, transcript)))
                {
                    // Filter out non-verbatim dimension evidence snippets instead of failing whole evaluation,
                    // matching Behavioural's resilient validation behavior.
                    dimension.Evidence.RemoveAll(evidence => string.IsNullOrWhiteSpace(evidence) || !IsGroundedEvidence(evidence, transcript));
                }
            }

            return new TechnicalEvaluationValidationResult(true, null);
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
                || dimension.SuggestedScore > rubric.MaximumScore
                || !HasPrecision(dimension.SuggestedScore.Value, rubric.RoundingPrecision))
            {
                return "INVALID_V2_SCORE";
            }

            dimension.Evidence ??= new List<string>();
            dimension.MissingEvidence ??= new List<string>();
            dimension.Evidence.RemoveAll(string.IsNullOrWhiteSpace);
            dimension.MissingEvidence.RemoveAll(string.IsNullOrWhiteSpace);

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

        private static bool IsGroundedEvidence(string evidence, string normalizedTranscript)
        {
            if (string.IsNullOrWhiteSpace(evidence) || evidence.Length > 1_000)
            {
                return false;
            }

            var normalizedEvidence = NormalizeEvidence(evidence);
            if (normalizedEvidence.Length == 0) return false;

            // 1. Direct contiguous substring match
            if (normalizedTranscript.Contains(normalizedEvidence, StringComparison.Ordinal))
            {
                return true;
            }

            // 2. Token overlap fallback for local AI models (e.g., Ollama / aispeis) that summarize/paraphrase evidence
            var evidenceTokens = normalizedEvidence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (evidenceTokens.Length == 0) return false;

            var transcriptTokens = normalizedTranscript.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.Ordinal);

            var significantEvidenceTokens = evidenceTokens
                .Where(t => t.Length > 2 && t is not "the" and not "and" and not "for" and not "with" and not "that" and not "this" and not "from")
                .ToList();

            if (significantEvidenceTokens.Count == 0)
            {
                significantEvidenceTokens = evidenceTokens.ToList();
            }

            int matchedCount = significantEvidenceTokens.Count(token => transcriptTokens.Contains(token));
            return (double)matchedCount / significantEvidenceTokens.Count >= 0.40;
        }

        private static bool HasPrecision(decimal value, int precision) =>
            decimal.Round(value, precision, MidpointRounding.AwayFromZero) == value;

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
