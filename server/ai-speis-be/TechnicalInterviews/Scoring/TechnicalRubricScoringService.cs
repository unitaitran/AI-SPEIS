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
        TechnicalQuestionScore ScoreQuestion(
            TechnicalAIEvaluationResponse evaluation,
            TechnicalRubricDefinition rubric);

        decimal ScoreSession(IEnumerable<decimal> finalMainQuestionScores, TechnicalRubricDefinition rubric);
    }

    public sealed class TechnicalRubricScoringService : ITechnicalRubricScoringService
    {
        public TechnicalQuestionScore ScoreQuestion(
            TechnicalAIEvaluationResponse evaluation,
            TechnicalRubricDefinition rubric)
        {
            var dimensionsByCode = evaluation.DimensionEvaluations.ToDictionary(
                item => item.RubricCode,
                StringComparer.OrdinalIgnoreCase);

            var dimensionScores = rubric.Dimensions.Select(dimension =>
            {
                var suggested = dimensionsByCode[dimension.Code].SuggestedScore;
                var final = Round(suggested, rubric.RoundingPrecision);
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

            var aiSuggested = Round(
                rubric.Dimensions.Sum(dimension =>
                    dimensionsByCode[dimension.Code].SuggestedScore * dimension.Weight),
                rubric.RoundingPrecision);
            var finalOverall = Round(
                dimensionScores.Sum(dimension => dimension.WeightedScore),
                rubric.RoundingPrecision);

            return new TechnicalQuestionScore(aiSuggested, finalOverall, dimensionScores);
        }

        public decimal ScoreSession(
            IEnumerable<decimal> finalMainQuestionScores,
            TechnicalRubricDefinition rubric)
        {
            var scores = finalMainQuestionScores.ToList();
            if (scores.Count == 0)
            {
                return 0m;
            }

            return Round(scores.Average(), rubric.RoundingPrecision);
        }

        private static decimal Round(decimal value, int precision)
        {
            return Math.Round(value, precision, MidpointRounding.AwayFromZero);
        }
    }
}
