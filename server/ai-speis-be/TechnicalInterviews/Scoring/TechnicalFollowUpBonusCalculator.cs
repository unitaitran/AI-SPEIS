using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.TechnicalInterviews.Rubrics;

namespace ai_speis_be.TechnicalInterviews.Scoring
{
    public sealed record TechnicalFollowUpBonusResult(
        decimal RawFollowUpScore,
        decimal Bonus,
        string Version);

    public interface ITechnicalFollowUpBonusCalculator
    {
        TechnicalFollowUpBonusResult Calculate(
            TechnicalQuestionScore score,
            TechnicalAIEvaluationResponse evaluation,
            TechnicalRubricDefinition rubric);
    }

    public sealed class TechnicalFollowUpBonusCalculator : ITechnicalFollowUpBonusCalculator
    {
        private readonly TechnicalInterviewOptions _options;

        public TechnicalFollowUpBonusCalculator(TechnicalInterviewOptions options)
        {
            _options = options;
        }

        public TechnicalFollowUpBonusResult Calculate(
            TechnicalQuestionScore score,
            TechnicalAIEvaluationResponse evaluation,
            TechnicalRubricDefinition rubric)
        {
            var hasEvidence = evaluation.DimensionEvaluations.Any(dimension =>
                dimension.Evidence.Any(item => !string.IsNullOrWhiteSpace(item)));
            var normalizedRatio = rubric.MaximumScore <= 0m
                ? 0m
                : score.FinalOverallScore / rubric.MaximumScore;
            var bonus = hasEvidence
                ? Math.Clamp(normalizedRatio, 0m, 1m)
                : 0m;
            bonus = Math.Round(bonus, rubric.RoundingPrecision, MidpointRounding.AwayFromZero);

            return new TechnicalFollowUpBonusResult(
                score.FinalOverallScore,
                bonus,
                _options.BonusCalculationVersion);
        }
    }
}
