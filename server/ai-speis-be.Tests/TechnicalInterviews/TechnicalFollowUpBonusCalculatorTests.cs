using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.TechnicalInterviews.Scoring;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalFollowUpBonusCalculatorTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 0.5)]
    [InlineData(10, 1)]
    public void Calculate_MapsValidatedZeroToTenScoreToZeroToOneBonus(
        double rawScore,
        double expectedBonus)
    {
        var rubric = TechnicalTestRubric.Create();
        var evaluation = TechnicalTestRubric.CreateEvaluation(
            (decimal)rawScore,
            (decimal)rawScore,
            (decimal)rawScore,
            (decimal)rawScore,
            (decimal)rawScore);
        var score = new TechnicalRubricScoringService().ScoreQuestion(evaluation, rubric);
        var calculator = new TechnicalFollowUpBonusCalculator(new TechnicalInterviewOptions());

        var result = calculator.Calculate(score, evaluation, rubric);

        Assert.Equal((decimal)expectedBonus, result.Bonus);
        Assert.InRange(result.Bonus, 0m, 1m);
    }
}
