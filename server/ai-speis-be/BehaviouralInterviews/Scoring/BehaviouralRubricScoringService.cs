using ai_speis_be.BehaviouralInterviews.AI;
using ai_speis_be.BehaviouralInterviews.Rubrics;

namespace ai_speis_be.BehaviouralInterviews.Scoring
{
    public sealed record BehaviouralDimensionScore(
        string RubricCode,
        string Name,
        decimal SuggestedScore,
        decimal FinalScore,
        decimal Weight,
        decimal WeightedScore,
        string Level);

    public sealed record BehaviouralQuestionScore(
        decimal AiSuggestedOverallScore,
        decimal FinalOverallScore,
        IReadOnlyList<BehaviouralDimensionScore> Dimensions);

    public interface IBehaviouralRubricScoringService
    {
        BehaviouralQuestionScore ScoreQuestion(
            BehaviouralAIEvaluationResponse evaluation,
            BehaviouralRubricDefinition rubric);

        decimal CombineMainQuestionScore(
            decimal? mainScore,
            decimal? clarificationScore,
            IReadOnlyList<decimal> followUpScores);

        decimal ScoreSession(IEnumerable<decimal> finalMainQuestionScores, BehaviouralRubricDefinition rubric);
    }

    public sealed class BehaviouralRubricScoringService : IBehaviouralRubricScoringService
    {
        // Evaluation Framework Level 1 (thang 0–10)
        private const decimal ClarificationWeight = 0.75m;  // Điểm cuối = 75% × điểm clarification
        private const decimal MaxBonusPerFollowUp = 1m;     // Mỗi follow-up cộng tối đa +1
        private const decimal MaxTotalFollowUpBonus = 2m;   // Tổng bonus follow-up tối đa +2
        private const decimal MaxQuestionScore = 10m;

        public BehaviouralQuestionScore ScoreQuestion(
            BehaviouralAIEvaluationResponse evaluation,
            BehaviouralRubricDefinition rubric)
        {
            var dimensionsByCode = evaluation.DimensionEvaluations.ToDictionary(
                item => item.RubricCode,
                StringComparer.OrdinalIgnoreCase);

            var dimensionScores = rubric.Dimensions.Select(dimension =>
            {
                var suggested = dimensionsByCode.TryGetValue(dimension.Code, out var dimensionEval) 
                    ? dimensionEval.SuggestedScore ?? 0m
                    : 0m;
                
                var final = Round(suggested, rubric.RoundingPrecision);
                var weighted = Round(final * dimension.Weight, rubric.RoundingPrecision + 2);
                return new BehaviouralDimensionScore(
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
                {
                    var suggested = dimensionsByCode.TryGetValue(dimension.Code, out var eval) ? eval.SuggestedScore ?? 0m : 0m;
                    return suggested * dimension.Weight;
                }),
                rubric.RoundingPrecision);
                
            var finalOverall = Round(
                dimensionScores.Sum(dimension => dimension.WeightedScore),
                rubric.RoundingPrecision);

            return new BehaviouralQuestionScore(aiSuggested, finalOverall, dimensionScores);
        }

        /// <summary>
        /// Gộp điểm câu chính theo Evaluation Framework Level 1 (thang 0–10):
        /// có clarification (main &lt;3) → điểm gốc = 75% × điểm clarification, ngược lại = điểm main;
        /// mỗi follow-up cộng bonus = điểm FU / 10 (tối đa +1), tổng bonus tối đa +2; kết quả ≤ 10.
        /// </summary>
        public decimal CombineMainQuestionScore(
            decimal? mainScore,
            decimal? clarificationScore,
            IReadOnlyList<decimal> followUpScores)
        {
            var baseScore = clarificationScore is not null
                ? ClarificationWeight * clarificationScore.Value
                : mainScore ?? 0m;

            var followUpBonus = followUpScores
                .Select(score => Math.Min(score / MaxQuestionScore * MaxBonusPerFollowUp, MaxBonusPerFollowUp))
                .Sum();
            followUpBonus = Math.Min(followUpBonus, MaxTotalFollowUpBonus);

            var final = Math.Min(baseScore + followUpBonus, MaxQuestionScore);
            return Round(final, 2);
        }

        public decimal ScoreSession(
            IEnumerable<decimal> finalMainQuestionScores,
            BehaviouralRubricDefinition rubric)
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
