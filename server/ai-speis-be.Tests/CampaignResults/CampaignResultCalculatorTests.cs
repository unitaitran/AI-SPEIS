using ai_speis_be.CampaignResults;
using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Tests.CampaignResults;

public class CampaignResultCalculatorTests
{
    [Fact]
    public void ApplyRoundWeights_UsesRubricWeightsForFullCampaign()
    {
        var rounds = new List<CampaignRoundResultDto>
        {
            new() { RoundType = "Behavior", Score = 8m },
            new() { RoundType = "Technical", Score = 7m },
            new() { RoundType = "Code", Score = 9m }
        };

        var result = CampaignResultCalculator.ApplyRoundWeights(rounds);

        Assert.Equal(8m, result);
        Assert.Equal(0.20m, rounds[0].AppliedWeight);
        Assert.Equal(0.40m, rounds[1].AppliedWeight);
        Assert.Equal(0.40m, rounds[2].AppliedWeight);
    }

    [Fact]
    public void ApplyRoundWeights_NormalizesWeightsWhenPracticeCampaignOmitsRounds()
    {
        var rounds = new List<CampaignRoundResultDto>
        {
            new() { RoundType = "Behavior", Score = 8m },
            new() { RoundType = "Technical", Score = 5m }
        };

        var result = CampaignResultCalculator.ApplyRoundWeights(rounds);

        Assert.Equal(6m, result);
        Assert.Equal(0.3333m, rounds[0].AppliedWeight);
        Assert.Equal(0.6667m, rounds[1].AppliedWeight);
    }

    [Theory]
    [InlineData(10, 10, 10)]
    [InlineData(9, 10, 9)]
    [InlineData(8, 10, 8)]
    [InlineData(1, 10, 1)]
    [InlineData(0, 10, 0)]
    [InlineData(9, 11, 8)]
    [InlineData(0, 0, 0)]
    public void GetCodingScore_FollowsPassedTestCaseRubric(int passed, int total, decimal expected)
    {
        Assert.Equal(expected, CampaignResultCalculator.GetCodingScore(passed, total));
    }

    [Fact]
    public void CalculateMetric_ReweightsOnlyAvailableSources()
    {
        var score = CampaignResultCalculator.CalculateMetric(
            (8m, 0.35m, "Accuracy"),
            (6m, 0.25m, "Depth"),
            ((decimal?)null, 0.40m, "Unavailable"));

        Assert.Equal(7.17m, score);
    }
}
