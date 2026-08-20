using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.AiEvaluationFeedbackService;
using ai_speis_be.Services.NotificationService;
using ai_speis_be.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ai_speis_be.Tests;

public sealed class AiEvaluationFeedbackServiceTests
{
    [Fact]
    public async Task CreateTechnicalRoundFeedback_PersistsMetadataWithoutInterviewContent()
    {
        await using var context = TestDbContextFactory.Create();
        SeedTechnicalEvaluation(context);
        var notifications = new Mock<IAdminNotificationPublisher>();
        var service = new AiEvaluationFeedbackService(context, notifications.Object);

        var result = await service.CreateAsync(10, new CreateAiEvaluationFeedbackRequest
        {
            InterviewSessionId = 30,
            EvaluationType = "Technical",
            Reason = AiEvaluationFeedbackReasons.IncorrectScore,
            Explanation = "The recorded evidence supports a higher score."
        }, CancellationToken.None);

        Assert.Equal(AiEvaluationFeedbackOperationStatus.Created, result.Status);
        Assert.Equal(AiEvaluationFeedbackReasons.IncorrectScore, result.Value!.Title);
        var feedbackModel = context.Model.FindEntityType(typeof(AiEvaluationFeedback));
        Assert.NotNull(feedbackModel);
        Assert.DoesNotContain(feedbackModel!.GetProperties(), property => new[]
        {
            "SessionQuestionId",
            "QuestionSnapshotJson",
            "TranscriptSnapshot",
            "ScoreSnapshot",
            "EvaluationSnapshotJson"
        }.Contains(property.Name));
        notifications.Verify(publisher => publisher.PublishAsync(
            It.Is<AdminNotificationEvent>(notification =>
                notification.Type == NotificationType.AI_EVALUATION_REQUIRES_REVIEW
                && notification.EntityId == result.Value.FeedbackId.ToString()
                && notification.ActionUrl == "/admin/ai-feedback"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateFeedback_RejectsUnsupportedReasonAndForeignSession()
    {
        await using var context = TestDbContextFactory.Create();
        SeedTechnicalEvaluation(context);
        var service = new AiEvaluationFeedbackService(context);

        var invalidReason = await service.CreateAsync(10, Request("FREE_TEXT"), CancellationToken.None);
        var foreignSession = await service.CreateAsync(11, Request(AiEvaluationFeedbackReasons.Hallucination), CancellationToken.None);

        Assert.Equal(AiEvaluationFeedbackOperationStatus.BadRequest, invalidReason.Status);
        Assert.Equal("INVALID_FEEDBACK_REASON", invalidReason.ErrorCode);
        Assert.Equal(AiEvaluationFeedbackOperationStatus.Forbidden, foreignSession.Status);
        Assert.Empty(context.AiEvaluationFeedbacks);
    }

    [Fact]
    public async Task GetAdminPageAsync_SupportsHumanReadableReasonSearch()
    {
        await using var context = TestDbContextFactory.Create();
        SeedTechnicalEvaluation(context);
        var service = new AiEvaluationFeedbackService(context);
        await service.CreateAsync(10, Request(AiEvaluationFeedbackReasons.MissingContext), CancellationToken.None);

        var result = await service.GetAdminPageAsync("Thiếu bằng chứng", 1, 10, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(AiEvaluationFeedbackReasons.MissingContext, result.Items[0].Reason);
    }

    private static CreateAiEvaluationFeedbackRequest Request(string reason) => new()
    {
        InterviewSessionId = 30,
        EvaluationType = "Technical",
        Reason = reason,
        Explanation = "This evaluation should be reviewed by an administrator."
    };

    private static void SeedTechnicalEvaluation(ApplicationDbContext context)
    {
        var user = new User
        {
            UserId = 10,
            RoleId = 2,
            FullName = "Test User",
            Email = "user@example.com",
            CreatedAt = DateTime.UtcNow
        };
        var campaign = new InterviewCampaign
        {
            InterviewCampaignId = 20,
            UserId = 10,
            CVExtractedProfileId = 1,
            JDExtractedProfileId = 1,
            Status = InterviewCampaignStatus.Completed,
            User = user
        };
        var session = new InterviewSession
        {
            InterviewSessionId = 30,
            InterviewCampaignId = 20,
            InterviewCampaign = campaign,
            InterviewRoundType = InterviewRoundType.Technical,
            Difficulty = QuestionDifficultyEnum.Medium,
            Status = InterviewSessionStatus.Completed
        };
        var set = new TechnicalQuestionSet
        {
            TechnicalQuestionSetId = 40,
            InterviewSessionId = 30,
            InterviewSession = session,
            QuestionCount = 1
        };
        var question = new TechnicalSessionQuestion
        {
            TechnicalSessionQuestionId = 50,
            TechnicalQuestionSetId = 40,
            TechnicalQuestionSet = set,
            QuestionId = 100,
            QuestionOrder = 1,
            QuestionSnapshotJson = "{\"text\":\"Explain dependency injection\"}",
            Status = TechnicalSessionQuestionStatus.Answered
        };
        var answer = new TechnicalAnswer
        {
            TechnicalAnswerId = 60,
            TechnicalSessionQuestionId = 50,
            TechnicalSessionQuestion = question,
            Transcript = "My original answer",
            FinalQuestionScore = 6.5m,
            AiCriteriaDetailJson = "{\"accuracy\":6.5}",
            EvaluationStatus = TechnicalAnswerEvaluationStatus.Completed
        };
        question.Answer = answer;
        set.Questions.Add(question);
        session.TechnicalQuestionSet = set;
        campaign.InterviewSessions.Add(session);
        var roundResult = new TechnicalRoundResult
        {
            TechnicalRoundResultId = 70,
            InterviewSessionId = 30,
            InterviewSession = session,
            AiExecutiveSummary = "Strong technical reasoning.",
            AiStrengths = "[\"Clear explanation\"]",
            AiGaps = "[\"Add operational metrics\"]",
            FinalFeedbackStatus = "COMPLETED"
        };

        context.Users.Add(user);
        context.InterviewCampaigns.Add(campaign);
        context.InterviewSessions.Add(session);
        context.TechnicalQuestionSets.Add(set);
        context.TechnicalSessionQuestions.Add(question);
        context.TechnicalAnswers.Add(answer);
        context.TechnicalRoundResults.Add(roundResult);
        context.SaveChanges();
    }
}
