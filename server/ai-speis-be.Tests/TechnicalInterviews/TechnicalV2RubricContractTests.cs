using ai_speis_be.Models.DTOs;
using ai_speis_be.Services.InterviewSessionService;
using System.Text.Json;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Rubrics;
using ai_speis_be.TechnicalInterviews.Scoring;
using ai_speis_be.TechnicalInterviews.Validation;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalV2RubricContractTests
{
    private const string Answer = "Dependency injection separates construction from use and improves testability.";

    [Fact]
    public void ValidEvaluation_PreservesAllFiveCriterionScores()
    {
        var result = Validate(ValidEvaluation());

        Assert.True(result.IsValid);
        Assert.False(result.IsPartial);
        Assert.Null(result.ErrorCode);
        Assert.All(result.NormalizedEvaluation!.Evaluation!.DimensionEvaluations!, item => Assert.Equal(8m, item.SuggestedScore));
    }

    [Fact]
    public void NativeAispeisRawResponse_DeserializesValidatesAndScores()
    {
        const string candidateAnswer = "Dependency injection separates object construction from use. A service receives its repository through a constructor, so the service is loosely coupled and can use a mock repository in tests.";
        const string rawResponse = """
            {"evaluation":{"dimensionEvaluations":[{"evidence":["Dependency injection separates object construction from use."],"missingEvidence":[],"rubricCode":"ACCURACY","suggestedScore":8},{"evidence":["A service receives its repository through a constructor, so the service is loosely coupled and can use a mock repository in tests."],"missingEvidence":[],"rubricCode":"TECHNICAL_DEPTH","suggestedScore":7},{"evidence":["Dependency injection separates object construction from use."],"missingEvidence":[],"rubricCode":"REASONING","suggestedScore":7},{"evidence":["A service receives its repository through a constructor, so the service is loosely coupled and can use a mock repository in tests."],"missingEvidence":[],"rubricCode":"APPLICATION","suggestedScore":6},{"evidence":["Dependency injection separates object construction from use."],"missingEvidence":[],"rubricCode":"COMMUNICATION","suggestedScore":7}]}}
            """;

        var evaluation = JsonSerializer.Deserialize<TechnicalV2EvaluationResponse>(
            rawResponse,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var validation = new TechnicalAIResponseValidator().ValidateEvaluationV2(
            evaluation!,
            Rubric(),
            new[] { new TechnicalAnswerContext("MAIN", "Explain dependency injection.", candidateAnswer) });
        var score = new TechnicalRubricScoringService().ScoreQuestionV2(
            validation.NormalizedEvaluation!,
            Rubric());

        Assert.True(validation.IsValid);
        Assert.False(validation.IsPartial);
        Assert.Equal(5, validation.NormalizedEvaluation!.Evaluation!.DimensionEvaluations!.Count);
        Assert.Equal(7.15m, score.FinalOverallScore);
    }

    [Fact]
    public void GeminiObservedPositiveApplicationWithoutEvidence_IsolatedAsZeroCriterion()
    {
        // Captured Gemini shape before the prompt/schema fix: the response was
        // valid JSON, but APPLICATION had a positive score with no evidence.
        const string rawResponse = """
            {"evaluation":{"dimensionEvaluations":[{"rubricCode":"ACCURACY","suggestedScore":6,"evidence":["Java is platform independent because bytecode runs on the JVM."],"missingEvidence":[]},{"rubricCode":"TECHNICAL_DEPTH","suggestedScore":4,"evidence":["concurrency APIs"],"missingEvidence":[]},{"rubricCode":"REASONING","suggestedScore":4,"evidence":["Java is platform independent because bytecode runs on the JVM."],"missingEvidence":[]},{"rubricCode":"APPLICATION","suggestedScore":3,"evidence":[],"missingEvidence":["Real-world examples were not provided."]},{"rubricCode":"COMMUNICATION","suggestedScore":5,"evidence":["It also supports multithreading through the concurrency APIs."],"missingEvidence":[]}]}}
            """;

        var evaluation = JsonSerializer.Deserialize<TechnicalV2EvaluationResponse>(
            rawResponse,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var result = new TechnicalAIResponseValidator().ValidateEvaluationV2(
            evaluation!,
            Rubric(),
            new[]
            {
                new TechnicalAnswerContext(
                    "MAIN",
                    "What are the main features of Java?",
                    "Java is platform independent because bytecode runs on the JVM. It also supports multithreading through the concurrency APIs.")
            });

        Assert.True(result.IsValid);
        Assert.True(result.IsPartial);
        Assert.Equal("INVALID_V2_EVIDENCE", result.ErrorCode);
        var dimensions = result.NormalizedEvaluation!.Evaluation!.DimensionEvaluations!;
        Assert.Equal(0m, dimensions.Single(item => item.RubricCode == "APPLICATION").SuggestedScore);
        Assert.Empty(dimensions.Single(item => item.RubricCode == "APPLICATION").Evidence!);
        Assert.All(
            dimensions.Where(item => item.RubricCode != "APPLICATION"),
            item => Assert.NotEqual(0m, item.SuggestedScore));
    }

    [Fact]
    public void Contract_UsesExactlyTheFiveOfficialCriteriaWithExactWeights()
    {
        var rubric = Rubric();

        Assert.Equal(
            new[] { "ACCURACY", "TECHNICAL_DEPTH", "REASONING", "APPLICATION", "COMMUNICATION" },
            rubric.Dimensions.Select(item => item.Code));
        Assert.Equal(new[] { .30m, .25m, .20m, .15m, .10m }, rubric.Dimensions.Select(item => item.Weight));
        Assert.Equal(1m, rubric.Dimensions.Sum(item => item.Weight));
        Assert.Equal(0m, rubric.MinimumScore);
        Assert.Equal(10m, rubric.MaximumScore);
        Assert.Equal(2, rubric.RoundingPrecision);
    }

    [Theory]
    [InlineData("ACCURACY")]
    [InlineData("TECHNICAL_DEPTH")]
    [InlineData("REASONING")]
    [InlineData("APPLICATION")]
    [InlineData("COMMUNICATION")]
    public void MissingOfficialCriterion_IsolatedAsUnavailable(string rubricCode)
    {
        var evaluation = ValidEvaluation();
        evaluation.Evaluation!.DimensionEvaluations!.RemoveAll(item => item.RubricCode == rubricCode);

        AssertOnlyCriterionUnavailable(Validate(evaluation), rubricCode, "MISSING_V2_CRITERION");
    }

    [Fact]
    public void InvalidCriterionEvidence_ClearsEvidenceWithoutDiscardingScores()
    {
        var evaluation = ValidEvaluation();
        evaluation.Evaluation!.DimensionEvaluations!
            .Single(item => item.RubricCode == "REASONING")
            .Evidence = new List<string> { "The candidate deployed Kubernetes to production." };

        var result = Validate(evaluation);
        Assert.True(result.IsValid);
        var reasoning = result.NormalizedEvaluation!.Evaluation!.DimensionEvaluations!
            .Single(item => item.RubricCode == "REASONING");
        Assert.Empty(reasoning.Evidence!);
        Assert.NotEqual(0m, reasoning.SuggestedScore);
    }

    [Fact]
    public void ApplicationZeroWithNoEvidence_IsAValidCriterionEvaluation()
    {
        var evaluation = ValidEvaluation();
        var application = evaluation.Evaluation!.DimensionEvaluations!
            .Single(item => item.RubricCode == "APPLICATION");
        application.SuggestedScore = 0m;
        application.Evidence = new List<string>();
        application.MissingEvidence = new List<string> { "No concrete real-world application/example was provided." };

        var result = Validate(evaluation);

        Assert.True(result.IsValid);
        Assert.False(result.IsPartial);
        Assert.Empty(result.InvalidCriterionCodes);
        Assert.Equal(0m, result.NormalizedEvaluation!.Evaluation!.DimensionEvaluations!
            .Single(item => item.RubricCode == "APPLICATION").SuggestedScore);
        Assert.Empty(result.NormalizedEvaluation.Evaluation.DimensionEvaluations!
            .Single(item => item.RubricCode == "APPLICATION").Evidence!);
        Assert.Equal("No concrete real-world application/example was provided.",
            Assert.Single(result.NormalizedEvaluation.Evaluation.DimensionEvaluations!
                .Single(item => item.RubricCode == "APPLICATION").MissingEvidence!));
    }

    [Fact]
    public void TwoInvalidCriteria_OnlyZeroThoseCriteria()
    {
        var evaluation = ValidEvaluation();
        evaluation.Evaluation!.DimensionEvaluations!.Single(item => item.RubricCode == "TECHNICAL_DEPTH").SuggestedScore = 8.001m;
        evaluation.Evaluation!.DimensionEvaluations!.Single(item => item.RubricCode == "APPLICATION").Evidence = null;

        var result = Validate(evaluation);

        Assert.True(result.IsValid);
        Assert.True(result.IsPartial);
        Assert.Equal(new[] { "TECHNICAL_DEPTH", "APPLICATION" }, result.InvalidCriterionCodes);
        var dimensions = result.NormalizedEvaluation!.Evaluation!.DimensionEvaluations!;
        Assert.Equal(0m, dimensions.Single(item => item.RubricCode == "TECHNICAL_DEPTH").SuggestedScore);
        Assert.Equal(0m, dimensions.Single(item => item.RubricCode == "APPLICATION").SuggestedScore);
        Assert.All(dimensions.Where(item => item.RubricCode is "ACCURACY" or "REASONING" or "COMMUNICATION"), item => Assert.Equal(8m, item.SuggestedScore));
    }

    [Fact]
    public void AllRecognizedCriteriaIndividuallyInvalid_RemainsPartial()
    {
        var evaluation = ValidEvaluation();
        foreach (var dimension in evaluation.Evaluation!.DimensionEvaluations!)
        {
            dimension.Evidence = new List<string> { "Ungrounded evidence." };
        }

        var result = Validate(evaluation);

        Assert.True(result.IsValid);
        Assert.True(result.IsPartial);
        Assert.Equal(5, result.InvalidCriterionCodes.Count);
        Assert.All(result.NormalizedEvaluation!.Evaluation!.DimensionEvaluations!, item => Assert.Equal(0m, item.SuggestedScore));
    }

    [Fact]
    public void UnknownOnlyCriteria_IsUnusableAndRequiresFullFallback()
    {
        var evaluation = ValidEvaluation();
        foreach (var dimension in evaluation.Evaluation!.DimensionEvaluations!)
        {
            dimension.RubricCode = "UNRECOGNIZED";
        }

        var result = Validate(evaluation);

        Assert.False(result.IsValid);
        Assert.False(result.IsPartial);
        Assert.Equal("INVALID_V2_DIMENSIONS", result.ErrorCode);
    }

    [Fact]
    public void EmptyOrNullEvaluation_IsUnusableAndRequiresFullFallback()
    {
        var empty = new TechnicalV2EvaluationResponse
        {
            Evaluation = new TechnicalV2EvaluationPayload { DimensionEvaluations = new() }
        };

        Assert.False(Validate(empty).IsValid);
        Assert.False(Validate(new TechnicalV2EvaluationResponse { Evaluation = null }).IsValid);
        Assert.False(new TechnicalAIResponseValidator().ValidateEvaluationV2(null!, Rubric(), AnswerContext()).IsValid);
    }

    [Fact]
    public void WeightedScore_IsCalculatedByTheBackend()
    {
        var evaluation = ValidEvaluation();
        var dimensions = evaluation.Evaluation!.DimensionEvaluations!;
        dimensions.Single(item => item.RubricCode == "ACCURACY").SuggestedScore = 8m;
        dimensions.Single(item => item.RubricCode == "TECHNICAL_DEPTH").SuggestedScore = 7.5m;
        dimensions.Single(item => item.RubricCode == "REASONING").Evidence = null;
        dimensions.Single(item => item.RubricCode == "APPLICATION").SuggestedScore = 8m;
        dimensions.Single(item => item.RubricCode == "COMMUNICATION").SuggestedScore = 7m;

        var normalized = Validate(evaluation);
        var score = new TechnicalRubricScoringService().ScoreQuestionV2(normalized.NormalizedEvaluation!, Rubric());

        Assert.True(normalized.IsPartial);
        Assert.Equal(0m, normalized.NormalizedEvaluation!.Evaluation!.DimensionEvaluations!.Single(item => item.RubricCode == "REASONING").SuggestedScore);
        Assert.Equal(6.18m, score.AiSuggestedOverallScore);
        Assert.Equal(6.18m, score.FinalOverallScore);
    }

    [Fact]
    public void TechnicalRoundScore_IsTheAverageOfQuestionScores()
    {
        var score = new TechnicalRubricScoringService().ScoreSession(new[] { 7.10m, 8.20m, 9.30m }, Rubric());

        Assert.Equal(8.20m, score);
    }

    [Fact]
    public void DashboardIndicators_UseTheOfficialLevelFourFormulas()
    {
        var technical = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["ACCURACY"] = 10m,
            ["TECHNICAL_DEPTH"] = 8m,
            ["REASONING"] = 6m,
            ["APPLICATION"] = 4m,
            ["COMMUNICATION"] = 2m
        };
        var behavioural = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["communication"] = 8m,
            ["action"] = 5m
        };
        var rounds = new List<CampaignRoundResultDto>
        {
            new() { RoundType = "Code", Score = 9m }
        };
        var method = typeof(InterviewSessionService).GetMethod(
            "BuildDashboardMetrics",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var metrics = Assert.IsType<List<CampaignDashboardMetricDto>>(method!.Invoke(null, new object[] { technical, behavioural, rounds }));

        Assert.Equal(8.35m, metrics.Single(item => item.Code == "PROFESSIONAL_KNOWLEDGE").Score);
        Assert.Equal(5.60m, metrics.Single(item => item.Code == "COMMUNICATION_SKILLS").Score);
        Assert.Equal(5.00m, metrics.Single(item => item.Code == "CV_UNDERSTANDING").Score);
        Assert.Equal(7.75m, metrics.Single(item => item.Code == "PROBLEM_SOLVING").Score);
    }

    private static void AssertOnlyCriterionUnavailable(
        TechnicalEvaluationValidationResult result,
        string rubricCode,
        string errorCode)
    {
        Assert.True(result.IsValid);
        Assert.True(result.IsPartial);
        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Equal(new[] { rubricCode }, result.InvalidCriterionCodes);

        var dimensions = result.NormalizedEvaluation!.Evaluation!.DimensionEvaluations!;
        var invalid = dimensions.Single(item => item.RubricCode == rubricCode);
        Assert.Equal(0m, invalid.SuggestedScore);
        Assert.Empty(invalid.Evidence!);
        Assert.Equal($"AI evaluation unavailable ({errorCode}).", Assert.Single(invalid.MissingEvidence!));
        Assert.All(dimensions.Where(item => item.RubricCode != rubricCode), item => Assert.Equal(8m, item.SuggestedScore));
    }

    private static TechnicalEvaluationValidationResult Validate(TechnicalV2EvaluationResponse evaluation) =>
        new TechnicalAIResponseValidator().ValidateEvaluationV2(evaluation, Rubric(), AnswerContext());

    private static IReadOnlyList<TechnicalAnswerContext> AnswerContext() =>
        new[] { new TechnicalAnswerContext("MAIN", "Explain dependency injection.", Answer) };

    private static TechnicalV2EvaluationResponse ValidEvaluation() => new()
    {
        Evaluation = new TechnicalV2EvaluationPayload
        {
            DimensionEvaluations = Rubric().Dimensions.Select(item => new TechnicalV2DimensionEvaluation
            {
                RubricCode = item.Code,
                SuggestedScore = 8m,
                Evidence = new() { Answer },
                MissingEvidence = new()
            }).ToList()
        }
    };

    private static TechnicalRubricDefinition Rubric() => new()
    {
        Version = "technical-v2-runtime",
        ScoringPolicyVersion = "technical-v2-scoring",
        MinimumScore = 0m,
        MaximumScore = 10m,
        RoundingPrecision = 2,
        EvidenceRequiredWhenScoreAbove = 0m,
        Dimensions = new()
        {
            new() { Code = "ACCURACY", Name = "Accuracy", Weight = .30m },
            new() { Code = "TECHNICAL_DEPTH", Name = "Technical Depth", Weight = .25m },
            new() { Code = "REASONING", Name = "Reasoning", Weight = .20m },
            new() { Code = "APPLICATION", Name = "Application", Weight = .15m },
            new() { Code = "COMMUNICATION", Name = "Communication", Weight = .10m }
        },
        Levels = Enumerable.Range(0, 11).Select(score => new TechnicalRubricLevel { Score = score, Code = $"SCORE_{score}" }).ToList()
    };
}
