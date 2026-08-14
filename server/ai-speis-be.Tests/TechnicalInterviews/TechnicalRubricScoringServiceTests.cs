using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Rubrics;
using ai_speis_be.TechnicalInterviews.Scoring;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalRubricScoringServiceTests
{
    [Fact]
    public void ScoreQuestionV2_AppliesDocumentWeightsAndBackendRounding()
    {
        var rubric = TechnicalTestRubric.Create();
        var evaluation = TechnicalTestRubric.CreateEvaluation(10m, 8m, 6m, 4m, 2m);
        var service = new TechnicalRubricScoringService();

        var result = service.ScoreQuestionV2(evaluation, rubric);

        Assert.Equal(7.00m, result.FinalOverallScore);
        Assert.Equal(5, result.Dimensions.Count);
        Assert.Equal(3.00m, result.Dimensions[0].WeightedScore);
        Assert.Equal("SCORE_10", result.Dimensions[0].Level);
    }

    [Fact]
    public void ScoreSession_AveragesExactlyThreeFinalMainQuestionScores()
    {
        var service = new TechnicalRubricScoringService();

        var result = service.ScoreSession(
            new[] { 8.11m, 7.22m, 6.33m },
            TechnicalTestRubric.Create());

        Assert.Equal(7.22m, result);
    }

    [Fact]
    public void ScoreSession_AllowsIncompleteOfficialTechnicalScoreByAveragingAvailableScores()
    {
        var service = new TechnicalRubricScoringService();

        var result = service.ScoreSession(
            new[] { 8m, 7m },
            TechnicalTestRubric.Create());

        Assert.Equal(7.50m, result);
    }

    [Fact]
    public void ApplyClarificationRecovery_UsesSeventyFivePercentAndClamps()
    {
        var service = new TechnicalRubricScoringService();

        var result = service.ApplyClarificationRecovery(10m, 0.75m, TechnicalTestRubric.Create());

        Assert.Equal(7.50m, result);
        Assert.Equal(10m, service.Normalize(15m, TechnicalTestRubric.Create()));
    }
}

internal static class TechnicalTestRubric
{
    internal static readonly string[] Codes =
    {
        "ACCURACY",
        "TECHNICAL_DEPTH",
        "REASONING",
        "APPLICATION",
        "COMMUNICATION"
    };

    public static TechnicalRubricDefinition Create()
    {
        var weights = new[] { 0.30m, 0.25m, 0.20m, 0.15m, 0.10m };
        return new TechnicalRubricDefinition
        {
            Version = "technical-rubric-v2",
            ScoringPolicyVersion = "technical-scoring-v2",
            MinimumScore = 0,
            MaximumScore = 10,
            RoundingPrecision = 2,
            EvidenceRequiredWhenScoreAbove = 0,
            Dimensions = Codes.Select((code, index) => new TechnicalRubricDimension
            {
                Code = code,
                Name = code,
                Weight = weights[index]
            }).ToList(),
            Levels = Enumerable.Range(0, 11).Select(score => new TechnicalRubricLevel
            {
                Code = $"SCORE_{score}",
                Score = score
            }).ToList(),
            PerformanceBands = new List<TechnicalPerformanceBand>
            {
                new() { Code = "EXCELLENT", Name = "Excellent", Minimum = 9m, Maximum = 10m },
                new() { Code = "VERY_GOOD", Name = "Very Good", Minimum = 8m, Maximum = 9m, MaximumExclusive = true },
                new() { Code = "GOOD", Name = "Good", Minimum = 6.5m, Maximum = 8m, MaximumExclusive = true },
                new() { Code = "MINIMUM_REQUIREMENT_MET", Name = "Minimum Requirement Met", Minimum = 5m, Maximum = 6.5m, MaximumExclusive = true },
                new() { Code = "WEAK", Name = "Weak", Minimum = 3m, Maximum = 5m, MaximumExclusive = true },
                new() { Code = "VERY_WEAK", Name = "Very Weak", Minimum = 0m, Maximum = 3m, MaximumExclusive = true }
            }
        };
    }

    public static TechnicalV2EvaluationResponse CreateEvaluation(params decimal[] scores)
    {
        return new TechnicalV2EvaluationResponse
        {
            Evaluation = new TechnicalV2EvaluationPayload
            {
                DimensionEvaluations = Codes.Select((code, index) => new TechnicalV2DimensionEvaluation
                {
                    RubricCode = code,
                    SuggestedScore = scores[index],
                    Evidence = new List<string> { "dependency injection" }
                }).ToList()
            }
        };
    }
}
