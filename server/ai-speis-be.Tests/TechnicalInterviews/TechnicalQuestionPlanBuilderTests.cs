using ai_speis_be.Models.Enums;
using ai_speis_be.TechnicalInterviews.Planning;
using ai_speis_be.TechnicalInterviews.Scoring;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalQuestionPlanBuilderTests
{
    private readonly TechnicalQuestionPlanBuilder _builder = new();

    [Theory]
    [InlineData(0, TechnicalMatchBand.Low, 2, 1)]
    [InlineData(39, TechnicalMatchBand.Low, 2, 1)]
    [InlineData(40, TechnicalMatchBand.Medium, 1, 2)]
    [InlineData(69, TechnicalMatchBand.Medium, 1, 2)]
    [InlineData(70, TechnicalMatchBand.High, 1, 2)]
    [InlineData(100, TechnicalMatchBand.High, 1, 2)]
    public void Build_UsesDocumentMatchBoundaries(
        int matchScore,
        TechnicalMatchBand expectedBand,
        int expectedCvCount,
        int expectedJdCount)
    {
        var result = _builder.Build(Request(matchScore));

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedBand, result.Plan!.MatchBand);
        Assert.Equal(expectedCvCount, result.Plan.PlannedCvQuestionCount);
        Assert.Equal(expectedJdCount, result.Plan.PlannedJdQuestionCount);
        Assert.Equal(3, result.Plan.Slots.Length);
    }

    [Fact]
    public void Build_MediumCoverageChoosesTwoCvWhenCvCoverageIsHigher()
    {
        var result = _builder.Build(new TechnicalQuestionPlanRequest(
            50,
            new[] { "C#", "SQL", "Redis" },
            new[] { "Docker" },
            Array.Empty<string>(),
            new[] { "C#", "SQL", "Redis", "Docker" },
            "plan-v1"));

        Assert.Equal(2, result.Plan!.PlannedCvQuestionCount);
        Assert.Equal(1, result.Plan.PlannedJdQuestionCount);
    }

    [Fact]
    public void Build_MediumCoverageTiePrefersJd()
    {
        var result = _builder.Build(Request(50));

        Assert.Equal(1, result.Plan!.PlannedCvQuestionCount);
        Assert.Equal(2, result.Plan.PlannedJdQuestionCount);
    }

    [Theory]
    [InlineData(20, QuestionDifficultyEnum.Easy, QuestionDifficultyEnum.Easy, QuestionDifficultyEnum.Medium)]
    [InlineData(50, QuestionDifficultyEnum.Medium, QuestionDifficultyEnum.Medium, QuestionDifficultyEnum.Medium)]
    [InlineData(80, QuestionDifficultyEnum.Medium, QuestionDifficultyEnum.Hard, QuestionDifficultyEnum.Hard)]
    public void Build_AssignsDifficultyProgression(
        int score,
        QuestionDifficultyEnum first,
        QuestionDifficultyEnum second,
        QuestionDifficultyEnum third)
    {
        var plan = _builder.Build(Request(score)).Plan!;

        Assert.Equal(new[] { first, second, third }, plan.Slots.Select(slot => slot.PlannedDifficulty));
    }

    [Fact]
    public void Build_PrioritizesRequiredJdSkillsAndAvoidsDuplicateSkillsOrObjectives()
    {
        var plan = _builder.Build(Request(80)).Plan!;

        Assert.Equal("Docker", plan.Slots[0].TargetSkill);
        Assert.Equal(3, plan.Slots.Select(slot => slot.TargetSkill).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(plan.Slots.Zip(plan.Slots.Skip(1)), pair =>
            Assert.NotEqual(pair.First.EvaluationObjective, pair.Second.EvaluationObjective));
    }

    [Fact]
    public void Build_DoesNotPlanSkillsWithoutQuestionBankCoverage()
    {
        var result = _builder.Build(new TechnicalQuestionPlanRequest(
            80,
            new[] { "UnsupportedCvSkill" },
            new[] { "Docker", "Kubernetes" },
            Array.Empty<string>(),
            new[] { "C#", "SQL", "Docker", "Kubernetes" },
            "plan-v1"));

        Assert.False(result.IsSuccess);
        Assert.Equal("INSUFFICIENT_PLAN_SOURCE_SKILLS", result.ErrorCode);
    }

    [Fact]
    public void MatchScore_ChangesQuestionPlanButNeverChangesEvaluationScore()
    {
        var lowPlan = _builder.Build(Request(0)).Plan!;
        var highPlan = _builder.Build(Request(100)).Plan!;
        var evaluation = TechnicalTestRubric.CreateEvaluation(7m, 7m, 7m, 7m, 7m);
        var scoring = new TechnicalRubricScoringService();

        var scoreBeforePlan = scoring.ScoreQuestion(evaluation, TechnicalTestRubric.Create()).FinalOverallScore;
        var scoreAfterPlan = scoring.ScoreQuestion(evaluation, TechnicalTestRubric.Create()).FinalOverallScore;

        Assert.NotEqual(lowPlan.PlannedCvQuestionCount, highPlan.PlannedCvQuestionCount);
        Assert.Equal(scoreBeforePlan, scoreAfterPlan);
        Assert.Equal(7m, scoreAfterPlan);
    }

    private static TechnicalQuestionPlanRequest Request(int matchScore)
    {
        return new TechnicalQuestionPlanRequest(
            matchScore,
            new[] { "C#", "SQL" },
            new[] { "Docker", "Kubernetes" },
            new[] { "Redis" },
            new[] { "C#", "SQL", "Docker", "Kubernetes", "Redis" },
            "technical-question-plan-v1");
    }
}
