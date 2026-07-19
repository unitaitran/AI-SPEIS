using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Rubrics;
using ai_speis_be.TechnicalInterviews.Scoring;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalRubricScoringServiceTests
{
    [Fact]
    public void ScoreQuestion_AppliesDocumentWeightsAndBackendRounding()
    {
        var rubric = TechnicalTestRubric.Create();
        var evaluation = TechnicalTestRubric.CreateEvaluation(5m, 4m, 3m, 2m, 1m);
        var service = new TechnicalRubricScoringService();

        var result = service.ScoreQuestion(evaluation, rubric);

        Assert.Equal(3.50m, result.FinalOverallScore);
        Assert.Equal(5, result.Dimensions.Count);
        Assert.Equal(1.50m, result.Dimensions[0].WeightedScore);
        Assert.Equal("SCORE_5", result.Dimensions[0].Level);
    }

    [Fact]
    public void ScoreSession_AveragesOnlyFinalMainQuestionScores()
    {
        var service = new TechnicalRubricScoringService();

        var result = service.ScoreSession(new[] { 4.11m, 3.22m, 2.33m }, TechnicalTestRubric.Create());

        Assert.Equal(3.22m, result);
    }
}

internal static class TechnicalTestRubric
{
    internal static readonly string[] Codes =
    {
        "TECHNICAL_ACCURACY",
        "TECHNICAL_DEPTH",
        "EXPLANATION_REASONING",
        "PRACTICAL_APPLICATION",
        "COMMUNICATION"
    };

    public static TechnicalRubricDefinition Create()
    {
        var weights = new[] { 0.30m, 0.25m, 0.20m, 0.15m, 0.10m };
        return new TechnicalRubricDefinition
        {
            Version = "technical-rubric-v1",
            ScoringPolicyVersion = "technical-scoring-v1",
            MinimumScore = 0,
            MaximumScore = 5,
            RoundingPrecision = 2,
            EvidenceRequiredWhenScoreAbove = 0,
            Dimensions = Codes.Select((code, index) => new TechnicalRubricDimension
            {
                Code = code,
                Name = code,
                Weight = weights[index]
            }).ToList(),
            Levels = Enumerable.Range(0, 6).Select(score => new TechnicalRubricLevel
            {
                Code = $"SCORE_{score}",
                Score = score
            }).ToList(),
            PerformanceBands = new List<TechnicalPerformanceBand>
            {
                new() { Code = "EXCELLENT", Name = "Xuất sắc", Minimum = 4.50m, Maximum = 5.00m },
                new() { Code = "GOOD", Name = "Tốt", Minimum = 3.50m, Maximum = 4.49m },
                new() { Code = "FAIR", Name = "Khá", Minimum = 2.50m, Maximum = 3.49m },
                new() { Code = "WEAK", Name = "Yếu", Minimum = 1.50m, Maximum = 2.49m },
                new() { Code = "POOR", Name = "Kém", Minimum = 0m, Maximum = 1.49m }
            }
        };
    }

    public static TechnicalAIEvaluationResponse CreateEvaluation(params decimal[] scores)
    {
        return new TechnicalAIEvaluationResponse
        {
            Decision = "NEXT_QUESTION",
            Confidence = 0.9m,
            DimensionEvaluations = Codes.Select((code, index) => new TechnicalAIDimensionEvaluation
            {
                RubricCode = code,
                SuggestedScore = scores[index],
                SuggestedLevel = $"SCORE_{Math.Round(scores[index], 0, MidpointRounding.AwayFromZero)}",
                Evidence = new List<string> { "dependency injection" },
                ReasonSummary = "Short validated reason."
            }).ToList()
        };
    }
}
