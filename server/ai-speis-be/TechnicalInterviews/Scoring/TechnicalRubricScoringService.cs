using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Rubrics;

namespace ai_speis_be.TechnicalInterviews.Scoring
{
    public sealed record TechnicalDimensionScore(
        string RubricCode,
        string Name,
        decimal SuggestedScore,
        decimal FinalScore,
        decimal Weight,
        decimal WeightedScore,
        string Level);

    public sealed record TechnicalQuestionScore(
        decimal AiSuggestedOverallScore,
        decimal FinalOverallScore,
        IReadOnlyList<TechnicalDimensionScore> Dimensions);

    public interface ITechnicalRubricScoringService
    {
        TechnicalQuestionScore ScoreQuestionV2(
            TechnicalV2EvaluationResponse evaluation,
            TechnicalRubricDefinition rubric);

        decimal ScoreSession(
            IEnumerable<decimal> finalMainQuestionScores,
            TechnicalRubricDefinition rubric,
            int requiredMainQuestionCount = 3);

        decimal ApplyClarificationRecovery(
            decimal clarificationQuestionScore,
            decimal recoveryFactor,
            TechnicalRubricDefinition rubric);

        decimal Normalize(decimal value, TechnicalRubricDefinition rubric);
    }

    public sealed class TechnicalRubricScoringService : ITechnicalRubricScoringService
    {
        public TechnicalQuestionScore ScoreQuestionV2(
            TechnicalV2EvaluationResponse evaluation,
            TechnicalRubricDefinition rubric)
        {
            var dimensionsByCode = evaluation.Evaluation!.DimensionEvaluations!
                .ToDictionary(item => item.RubricCode!, StringComparer.OrdinalIgnoreCase);

            var dimensionScores = rubric.Dimensions.Select(dimension =>
            {
                var suggested = dimensionsByCode[dimension.Code].SuggestedScore!.Value;
                var final = Normalize(suggested, rubric);
                var weighted = Round(final * dimension.Weight, rubric.RoundingPrecision + 2);
                return new TechnicalDimensionScore(
                    dimension.Code,
                    dimension.Name,
                    suggested,
                    final,
                    dimension.Weight,
                    weighted,
                    rubric.GetLevelCode(final));
            }).ToList();

            var aiSuggested = Normalize(
                rubric.Dimensions.Sum(dimension =>
                    dimensionsByCode[dimension.Code].SuggestedScore!.Value * dimension.Weight),
                rubric);
            var finalOverall = Normalize(
                dimensionScores.Sum(dimension => dimension.WeightedScore),
                rubric);

            return new TechnicalQuestionScore(aiSuggested, finalOverall, dimensionScores);
        }

        public decimal ScoreSession(
            IEnumerable<decimal> finalMainQuestionScores,
            TechnicalRubricDefinition rubric,
            int requiredMainQuestionCount = 3)
        {
            var scores = finalMainQuestionScores.ToList();
            if (scores.Count == 0) return 0m;
            return Normalize(scores.Average(), rubric);
        }

        public decimal ApplyClarificationRecovery(
            decimal clarificationQuestionScore,
            decimal recoveryFactor,
            TechnicalRubricDefinition rubric)
        {
            return Normalize(clarificationQuestionScore * Math.Clamp(recoveryFactor, 0m, 1m), rubric);
        }

        public decimal Normalize(decimal value, TechnicalRubricDefinition rubric)
        {
            var clamped = Math.Clamp(value, rubric.MinimumScore, rubric.MaximumScore);
            return Round(clamped, rubric.RoundingPrecision);
        }

        private static decimal Round(decimal value, int precision)
        {
            return Math.Round(value, precision, MidpointRounding.AwayFromZero);
        }
    }
}
