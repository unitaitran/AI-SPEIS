using System.Text.Json;
using ai_speis_be.BehaviouralInterviews.AI;
using ai_speis_be.BehaviouralInterviews.Rubrics;
using ai_speis_be.BehaviouralInterviews.Scoring;
using ai_speis_be.BehaviouralInterviews.Validation;

namespace ai_speis_be.Tests.BehaviouralInterviews;

public sealed class BehaviouralRubricContractTests
{
    private const string Answer = "I coordinated the release, owned the rollout plan, and reduced production incidents by 40 percent.";

    [Fact]
    public void ValidEvaluation_PreservesAllFiveCriterionScores()
    {
        var result = Validate(ValidEvaluation());

        Assert.True(result.IsValid);
        Assert.False(result.IsPartial);
        Assert.Null(result.ErrorCode);
        Assert.All(result.NormalizedEvaluation!.DimensionEvaluations, item => Assert.Equal(8m, item.SuggestedScore));
    }

    [Fact]
    public void MissingCriterion_IsolatedWithoutDiscardingOtherScores()
    {
        var evaluation = ValidEvaluation();
        evaluation.DimensionEvaluations.RemoveAll(item => item.RubricCode == "RESULT");

        var result = Validate(evaluation);

        Assert.True(result.IsValid);
        Assert.True(result.IsPartial);
        Assert.Equal("MISSING_BEHAVIOURAL_CRITERION", result.ErrorCode);
        Assert.Equal(new[] { "RESULT" }, result.InvalidCriterionCodes);
        var dimensions = result.NormalizedEvaluation!.DimensionEvaluations;
        Assert.Equal(0m, dimensions.Single(item => item.RubricCode == "RESULT").SuggestedScore);
        Assert.All(dimensions.Where(item => item.RubricCode != "RESULT"), item => Assert.Equal(8m, item.SuggestedScore));
    }

    [Fact]
    public void MalformedScore_IsNotSilentlyAcceptedAsZero()
    {
        const string response = """
            {"dimensionEvaluations":[
              {"rubricCode":"SITUATION_TASK","suggestedScore":8,"evidence":[],"missingEvidence":[]},
              {"rubricCode":"ACTION","suggestedScore":"not-a-number","evidence":[],"missingEvidence":[]},
              {"rubricCode":"RESULT","suggestedScore":8,"evidence":[],"missingEvidence":[]},
              {"rubricCode":"COMPETENCY","suggestedScore":8,"evidence":[],"missingEvidence":[]},
              {"rubricCode":"COMMUNICATION","suggestedScore":8,"evidence":[],"missingEvidence":[]}
            ]}
            """;
        var evaluation = JsonSerializer.Deserialize<BehaviouralAIEvaluationResponse>(
            response,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Null(evaluation.DimensionEvaluations.Single(item => item.RubricCode == "ACTION").SuggestedScore);

        var result = Validate(evaluation);

        Assert.True(result.IsValid);
        Assert.True(result.IsPartial);
        Assert.Equal("INVALID_BEHAVIOURAL_SCORE", result.ErrorCode);
        Assert.Equal(new[] { "ACTION" }, result.InvalidCriterionCodes);
        Assert.Equal(0m, result.NormalizedEvaluation!.DimensionEvaluations.Single(item => item.RubricCode == "ACTION").SuggestedScore);
        Assert.All(result.NormalizedEvaluation.DimensionEvaluations.Where(item => item.RubricCode != "ACTION"), item => Assert.Equal(8m, item.SuggestedScore));
    }

    [Fact]
    public void GenuineZeroScore_RemainsValid()
    {
        var evaluation = ValidEvaluation();
        var result = evaluation.DimensionEvaluations.Single(item => item.RubricCode == "RESULT");
        result.SuggestedScore = 0m;
        result.Evidence = new();
        result.MissingEvidence = new() { "No outcome was provided." };

        var validation = Validate(evaluation);

        Assert.True(validation.IsValid);
        Assert.False(validation.IsPartial);
        Assert.Equal(0m, validation.NormalizedEvaluation!.DimensionEvaluations.Single(item => item.RubricCode == "RESULT").SuggestedScore);
    }

    [Fact]
    public void UnknownOnlyCriteria_RequiresFullFallback()
    {
        var evaluation = ValidEvaluation();
        foreach (var dimension in evaluation.DimensionEvaluations)
        {
            dimension.RubricCode = "UNRECOGNIZED";
        }

        var result = Validate(evaluation);

        Assert.False(result.IsValid);
        Assert.Equal("INVALID_BEHAVIOURAL_DIMENSIONS", result.ErrorCode);
    }

    [Fact]
    public void WeightedScore_UsesOnlyNormalizedDimensionScores()
    {
        var evaluation = ValidEvaluation();
        evaluation.DimensionEvaluations.Single(item => item.RubricCode == "SITUATION_TASK").SuggestedScore = 7m;
        evaluation.DimensionEvaluations.Single(item => item.RubricCode == "ACTION").SuggestedScore = 8m;
        evaluation.DimensionEvaluations.Single(item => item.RubricCode == "RESULT").SuggestedScore = 6m;
        evaluation.DimensionEvaluations.Single(item => item.RubricCode == "COMPETENCY").SuggestedScore = 9m;
        evaluation.DimensionEvaluations.Single(item => item.RubricCode == "COMMUNICATION").SuggestedScore = 8m;

        var validation = Validate(evaluation);
        var score = new BehaviouralRubricScoringService().ScoreQuestion(validation.NormalizedEvaluation!, Rubric());

        Assert.True(validation.IsValid);
        Assert.False(validation.IsPartial);
        Assert.Equal(7.60m, score.FinalOverallScore);
    }

    private static BehaviouralEvaluationValidationResult Validate(BehaviouralAIEvaluationResponse evaluation) =>
        new BehaviouralAIResponseValidator().ValidateEvaluation(evaluation, Rubric(), AnswerContext());

    private static IReadOnlyList<BehaviouralAnswerContext> AnswerContext() =>
        new[] { new BehaviouralAnswerContext("MAIN", "Describe a challenging release.", Answer) };

    private static BehaviouralAIEvaluationResponse ValidEvaluation() => new()
    {
        DimensionEvaluations = Rubric().Dimensions.Select(item => new BehaviouralAIDimensionEvaluation
        {
            RubricCode = item.Code,
            SuggestedScore = 8m,
            Evidence = new() { Answer },
            MissingEvidence = new()
        }).ToList()
    };

    private static BehaviouralRubricDefinition Rubric() => new()
    {
        Version = "behavioural-rubric-test",
        ScoringPolicyVersion = "behavioural-scoring-test",
        MinimumScore = 0m,
        MaximumScore = 10m,
        RoundingPrecision = 2,
        Dimensions = new()
        {
            new() { Code = "SITUATION_TASK", Name = "Situation and Task", Weight = .20m },
            new() { Code = "ACTION", Name = "Action", Weight = .30m },
            new() { Code = "RESULT", Name = "Result", Weight = .20m },
            new() { Code = "COMPETENCY", Name = "Competency", Weight = .20m },
            new() { Code = "COMMUNICATION", Name = "Communication", Weight = .10m }
        },
        Levels = Enumerable.Range(0, 11).Select(score => new BehaviouralRubricLevel { Score = score, Code = $"SCORE_{score}" }).ToList()
    };
}
