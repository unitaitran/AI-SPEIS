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
        string? ErrorCode);

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

            var transcript = NormalizeEvidence(string.Join(" ", answerContext.Select(item => item.Answer)));
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

                if (dimension.Evidence.Any(evidence => !IsGroundedEvidence(evidence, transcript)))
                {
                    return Invalid("EVIDENCE_NOT_IN_ANSWER");
                }
            }

            return new TechnicalEvaluationValidationResult(true, null);
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
            return normalizedEvidence.Length > 0
                && normalizedTranscript.Contains(normalizedEvidence, StringComparison.Ordinal);
        }

        private static string NormalizeEvidence(string value)
        {
            // AI providers and speech-to-text engines may return canonically
            // equivalent Unicode, omit Vietnamese diacritics, or change only
            // punctuation around an otherwise verbatim quote. Ground evidence
            // against the candidate transcript after removing those formatting
            // differences; word order and content must still be a contiguous
            // match, so invented or paraphrased evidence remains invalid.
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
