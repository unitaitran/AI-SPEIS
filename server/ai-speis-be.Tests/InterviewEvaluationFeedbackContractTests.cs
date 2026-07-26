using ai_speis_be.BehaviouralInterviews.AI;
using ai_speis_be.BehaviouralInterviews.DTOs;
using ai_speis_be.Models;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.DTOs;
using ai_speis_be.Tests.Helpers;

namespace ai_speis_be.Tests;

public sealed class InterviewEvaluationFeedbackContractTests
{
    [Fact]
    public void PerAnswerAiContractsContainEvaluationSignalsButNoFeedbackDraft()
    {
        var technicalProviderMethods = typeof(ITechnicalInterviewAIProvider)
            .GetMethods()
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);
        var technicalPayload = typeof(TechnicalAIEvaluationPayload)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var behaviouralPayload = typeof(BehaviouralAIEvaluationResponse)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("GenerateFeedbackDraftAsync", technicalProviderMethods);
        Assert.Contains("DimensionEvaluations", technicalPayload);
        Assert.Contains("Evidence", technicalPayload);
        Assert.DoesNotContain("Strengths", technicalPayload);
        Assert.DoesNotContain("ImprovementSuggestions", technicalPayload);

        Assert.Contains("Evidence", behaviouralPayload);
        Assert.Contains("MissingAspects", behaviouralPayload);
        Assert.DoesNotContain("Strengths", behaviouralPayload);
        Assert.DoesNotContain("MissingPoints", behaviouralPayload);
        Assert.DoesNotContain("Recommendations", behaviouralPayload);
    }

    [Fact]
    public void SubmitContractsDoNotExposePerQuestionFeedback()
    {
        var technicalSubmit = typeof(TechnicalSubmitAnswerResponseDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var behaviouralDecision = typeof(BehaviouralEvaluationDecisionDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Feedback", technicalSubmit);
        Assert.DoesNotContain("Strengths", technicalSubmit);
        Assert.DoesNotContain("Feedback", behaviouralDecision);
        Assert.DoesNotContain("Strengths", behaviouralDecision);
        Assert.DoesNotContain("Recommendations", behaviouralDecision);
    }

    [Fact]
    public void FinalFeedbackContractsContainRoundLevelAssessments()
    {
        var technical = typeof(TechnicalFinalSummaryDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var behavioural = typeof(BehaviouralFinalSummaryDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("OverallTechnicalAssessment", technical);
        Assert.Contains("KnowledgeGaps", technical);
        Assert.Contains("RecommendationsForImprovement", technical);

        Assert.Contains("OverallBehavioralAssessment", behavioural);
        Assert.Contains("Strengths", behavioural);
        Assert.Contains("Weaknesses", behavioural);
        Assert.Contains("RecommendationsForImprovement", behavioural);
    }

    [Fact]
    public void EfModelHasSubmissionAndFinalFeedbackConcurrencyGuards()
    {
        using var context = TestDbContextFactory.Create();
        var answer = context.Model.FindEntityType(typeof(BehaviourAnswer))!;
        var behaviouralResult = context.Model.FindEntityType(typeof(BehaviourRoundResult))!;
        var session = context.Model.FindEntityType(typeof(InterviewSession))!;

        Assert.Contains(answer.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(BehaviourAnswer.SubmissionIdempotencyKey) }));
        Assert.True(behaviouralResult
            .FindProperty(nameof(BehaviourRoundResult.FeedbackConcurrencyVersion))!
            .IsConcurrencyToken);
        Assert.Contains(behaviouralResult.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(BehaviourRoundResult.InterviewSessionId) }));
        Assert.True(session
            .FindProperty(nameof(InterviewSession.TechnicalConcurrencyVersion))!
            .IsConcurrencyToken);
    }
}
