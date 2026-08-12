using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Models.Enums;
using ai_speis_be.AI.Json;
using ai_speis_be.Services.InterviewSessionService;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Planning;
using ai_speis_be.TechnicalInterviews.Rubrics;
using ai_speis_be.TechnicalInterviews.Scoring;
using ai_speis_be.TechnicalInterviews.Selection;
using ai_speis_be.TechnicalInterviews.V2;
using ai_speis_be.TechnicalInterviews.Validation;
using ai_speis_be.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalV2InterviewOrchestratorTests
{
    private const int UserId = 71;
    private const int V2SessionId = 501;
    private const string Answer = "Dependency injection separates construction from use and improves testability.";

    [Fact]
    public async Task SubmitAnswer_UsesCanonicalStateAndEnforcesIdempotency()
    {
        using var context = TestDbContextFactory.Create();
        Seed(context);
        var (orchestrator, provider) = CreateOrchestrator(context);

        var initialized = await orchestrator.InitializeAsync(UserId, V2SessionId, new InitializeTechnicalV2Request(), CancellationToken.None);
        var started = await orchestrator.StartAsync(UserId, V2SessionId, CancellationToken.None);

        Assert.Equal(TechnicalV2OperationStatus.Created, initialized.Status);
        Assert.Equal(TechnicalV2OperationStatus.Ok, started.Status);
        Assert.Equal(1001, started.Value!.QuestionId);

        var submitted = await orchestrator.SubmitAnswerAsync(
            UserId,
            V2SessionId,
            started.Value.SessionQuestionId,
            new SubmitTechnicalV2AnswerRequest { Transcript = Answer },
            "answer-1",
            CancellationToken.None);

        Assert.Equal(TechnicalV2OperationStatus.Ok, submitted.Status);
        Assert.Equal(TechnicalAnswerEvaluationStatus.Completed.ToString(), submitted.Value!.EvaluationStatus);
        Assert.Equal(1002, submitted.Value.NextQuestion!.QuestionId);
        Assert.Single(context.TechnicalAnswers);
        var persistedAnswer = context.TechnicalAnswers.Single();
        Assert.NotNull(persistedAnswer.AiCriteriaDetailJson);

        var recoveredCurrent = await orchestrator.GetCurrentQuestionAsync(UserId, V2SessionId, CancellationToken.None);
        Assert.Equal(TechnicalV2OperationStatus.Ok, recoveredCurrent.Status);
        Assert.Equal(1002, recoveredCurrent.Value!.QuestionId);

        var duplicate = await orchestrator.SubmitAnswerAsync(
            UserId,
            V2SessionId,
            started.Value.SessionQuestionId,
            new SubmitTechnicalV2AnswerRequest { Transcript = Answer },
            "answer-1",
            CancellationToken.None);
        Assert.Equal(TechnicalV2OperationStatus.Ok, duplicate.Status);
        Assert.Single(context.TechnicalAnswers);

        var payloadMismatch = await orchestrator.SubmitAnswerAsync(
            UserId,
            V2SessionId,
            started.Value.SessionQuestionId,
            new SubmitTechnicalV2AnswerRequest { Transcript = "A different answer." },
            "answer-1",
            CancellationToken.None);
        Assert.Equal(TechnicalV2OperationStatus.Conflict, payloadMismatch.Status);
        Assert.Equal("IDEMPOTENCY_PAYLOAD_MISMATCH", payloadMismatch.ErrorCode);

        var alreadyAnswered = await orchestrator.SubmitAnswerAsync(
            UserId,
            V2SessionId,
            started.Value.SessionQuestionId,
            new SubmitTechnicalV2AnswerRequest { Transcript = Answer },
            "answer-2",
            CancellationToken.None);
        Assert.Equal(TechnicalV2OperationStatus.Conflict, alreadyAnswered.Status);
        Assert.Equal("ALREADY_ANSWERED", alreadyAnswered.ErrorCode);
        provider.Verify(item => item.EvaluateAnswerV2Async(
            It.IsAny<TechnicalV2AnswerProcessingContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitAnswer_LowScoreCreatesClarificationFromQuestionBankWithMainParent()
    {
        using var context = TestDbContextFactory.Create();
        Seed(context);
        context.Questions.Single(question => question.QuestionId == 1001).ClarificationQuestion = "Please clarify the mechanism and trade-offs.";
        context.SaveChanges();
        var (orchestrator, provider) = CreateOrchestrator(context);
        provider.Setup(item => item.EvaluateAnswerV2Async(It.IsAny<TechnicalV2AnswerProcessingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EvaluationWithScore(2m));

        await orchestrator.InitializeAsync(UserId, V2SessionId, new InitializeTechnicalV2Request(), CancellationToken.None);
        var main = (await orchestrator.StartAsync(UserId, V2SessionId, CancellationToken.None)).Value!;
        var submitted = await orchestrator.SubmitAnswerAsync(
            UserId, V2SessionId, main.SessionQuestionId,
            new SubmitTechnicalV2AnswerRequest { Transcript = Answer }, "clarification-1", CancellationToken.None);

        Assert.Equal("CLARIFICATION", submitted.Value!.Decision);
        Assert.NotNull(submitted.Value.NextQuestion);
        Assert.Equal(TechnicalSessionQuestionType.Clarification.ToString(), submitted.Value.NextQuestion!.QuestionType);
        Assert.Equal(main.SessionQuestionId, submitted.Value.NextQuestion.ParentSessionQuestionId);
        Assert.Equal("Please clarify the mechanism and trade-offs.", submitted.Value.NextQuestion.Content);
        Assert.Equal(1002, context.TechnicalSessionQuestions.Single(question => question.QuestionType == TechnicalSessionQuestionType.Main && question.QuestionOrder == 2).QuestionId);
    }

    [Fact]
    public async Task SubmitAnswer_PartialScoreUsesTwoBankFollowUpsThenAdvancesMainOrder()
    {
        using var context = TestDbContextFactory.Create();
        Seed(context);
        var source = context.Questions.Single(question => question.QuestionId == 1001);
        source.FollowUp1 = "Explain the first production trade-off.";
        source.FollowUp2 = "Explain how you would validate the decision.";
        context.SaveChanges();
        var (orchestrator, provider) = CreateOrchestrator(context);
        provider.Setup(item => item.EvaluateAnswerV2Async(It.IsAny<TechnicalV2AnswerProcessingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EvaluationWithScore(4m));

        await orchestrator.InitializeAsync(UserId, V2SessionId, new InitializeTechnicalV2Request(), CancellationToken.None);
        var main = (await orchestrator.StartAsync(UserId, V2SessionId, CancellationToken.None)).Value!;
        var first = await orchestrator.SubmitAnswerAsync(
            UserId, V2SessionId, main.SessionQuestionId,
            new SubmitTechnicalV2AnswerRequest { Transcript = Answer }, "follow-up-main", CancellationToken.None);
        var followUp1 = first.Value!.NextQuestion!;

        Assert.Equal("FOLLOW_UP", first.Value.Decision);
        Assert.Equal(TechnicalSessionQuestionType.FollowUp.ToString(), followUp1.QuestionType);
        Assert.Equal(main.SessionQuestionId, followUp1.ParentSessionQuestionId);
        Assert.Equal("Explain the first production trade-off.", followUp1.Content);

        var second = await orchestrator.SubmitAnswerAsync(
            UserId, V2SessionId, followUp1.SessionQuestionId,
            new SubmitTechnicalV2AnswerRequest { Transcript = Answer }, "follow-up-1", CancellationToken.None);
        var followUp2 = second.Value!.NextQuestion!;

        Assert.Equal("FOLLOW_UP", second.Value.Decision);
        Assert.Equal(TechnicalSessionQuestionType.FollowUp.ToString(), followUp2.QuestionType);
        Assert.Equal(main.SessionQuestionId, followUp2.ParentSessionQuestionId);
        Assert.Equal("Explain how you would validate the decision.", followUp2.Content);

        var third = await orchestrator.SubmitAnswerAsync(
            UserId, V2SessionId, followUp2.SessionQuestionId,
            new SubmitTechnicalV2AnswerRequest { Transcript = Answer }, "follow-up-2", CancellationToken.None);

        Assert.Equal("NEXT_QUESTION", third.Value!.Decision);
        Assert.Equal(1002, third.Value.NextQuestion!.QuestionId);
        Assert.Equal(2, context.TechnicalSessionQuestions.Count(question => question.ParentQuestionId == main.SessionQuestionId && question.QuestionType == TechnicalSessionQuestionType.FollowUp));
    }

    [Fact]
    public async Task SubmitAnswer_ClarificationCanBeFollowedByTwoFollowUpsWithinTotalLimit()
    {
        using var context = TestDbContextFactory.Create();
        Seed(context);
        var source = context.Questions.Single(question => question.QuestionId == 1001);
        source.ClarificationQuestion = "Clarify the mechanism.";
        source.FollowUp1 = "Explain the first trade-off.";
        source.FollowUp2 = "Explain the validation strategy.";
        context.SaveChanges();
        var (orchestrator, provider) = CreateOrchestrator(context);
        var evaluationCount = 0;
        provider.Setup(item => item.EvaluateAnswerV2Async(It.IsAny<TechnicalV2AnswerProcessingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => EvaluationWithScore(++evaluationCount == 1 ? 2m : 4m));

        await orchestrator.InitializeAsync(UserId, V2SessionId, new InitializeTechnicalV2Request(), CancellationToken.None);
        var question = (await orchestrator.StartAsync(UserId, V2SessionId, CancellationToken.None)).Value!;
        for (var index = 0; index < 4; index++)
        {
            var submitted = await orchestrator.SubmitAnswerAsync(
                UserId, V2SessionId, question.SessionQuestionId,
                new SubmitTechnicalV2AnswerRequest { Transcript = Answer }, $"chain-{index}", CancellationToken.None);
            if (submitted.Value!.NextQuestion is not null)
            {
                question = submitted.Value.NextQuestion;
            }
        }

        var main = context.TechnicalSessionQuestions.Single(item => item.QuestionType == TechnicalSessionQuestionType.Main && item.QuestionOrder == 1);
        var children = context.TechnicalSessionQuestions.Where(item => item.ParentQuestionId == main.TechnicalSessionQuestionId).ToList();
        Assert.Equal(3, children.Count);
        Assert.Single(children, item => item.QuestionType == TechnicalSessionQuestionType.Clarification);
        Assert.Equal(2, children.Count(item => item.QuestionType == TechnicalSessionQuestionType.FollowUp));
        Assert.Equal(1002, question.QuestionId);
    }

    [Fact]
    public async Task SubmitAnswer_DuplicateBankFollowUpIsNotPersistedTwice()
    {
        using var context = TestDbContextFactory.Create();
        Seed(context);
        var source = context.Questions.Single(question => question.QuestionId == 1001);
        source.FollowUp1 = "Describe the production incident response.";
        source.FollowUp2 = source.FollowUp1;
        context.SaveChanges();
        var (orchestrator, provider) = CreateOrchestrator(context);
        provider.Setup(item => item.EvaluateAnswerV2Async(It.IsAny<TechnicalV2AnswerProcessingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EvaluationWithScore(4m));

        await orchestrator.InitializeAsync(UserId, V2SessionId, new InitializeTechnicalV2Request(), CancellationToken.None);
        var main = (await orchestrator.StartAsync(UserId, V2SessionId, CancellationToken.None)).Value!;
        var first = await orchestrator.SubmitAnswerAsync(
            UserId, V2SessionId, main.SessionQuestionId,
            new SubmitTechnicalV2AnswerRequest { Transcript = Answer }, "duplicate-main", CancellationToken.None);
        var followUp = first.Value!.NextQuestion!;
        var second = await orchestrator.SubmitAnswerAsync(
            UserId, V2SessionId, followUp.SessionQuestionId,
            new SubmitTechnicalV2AnswerRequest { Transcript = Answer }, "duplicate-follow-up", CancellationToken.None);

        Assert.Equal("NEXT_QUESTION", second.Value!.Decision);
        Assert.Equal(1002, second.Value.NextQuestion!.QuestionId);
        Assert.Single(context.TechnicalSessionQuestions.Where(question => question.ParentQuestionId == main.SessionQuestionId));
    }

    [Fact]
    public async Task Complete_ExposesFollowUpReviewUnderItsMainQuestion()
    {
        using var context = TestDbContextFactory.Create();
        Seed(context);
        var source = context.Questions.Single(question => question.QuestionId == 1001);
        source.FollowUp1 = "Describe the first implementation trade-off.";
        source.FollowUp2 = "Describe the validation strategy.";
        context.SaveChanges();
        var (orchestrator, provider) = CreateOrchestrator(context);
        var evaluationCount = 0;
        provider.Setup(item => item.EvaluateAnswerV2Async(It.IsAny<TechnicalV2AnswerProcessingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => EvaluationWithScore(++evaluationCount <= 3 ? 4m : 8m));

        await orchestrator.InitializeAsync(UserId, V2SessionId, new InitializeTechnicalV2Request(), CancellationToken.None);
        var question = (await orchestrator.StartAsync(UserId, V2SessionId, CancellationToken.None)).Value!;
        for (var index = 0; index < 5; index++)
        {
            var submitted = await orchestrator.SubmitAnswerAsync(
                UserId, V2SessionId, question.SessionQuestionId,
                new SubmitTechnicalV2AnswerRequest { Transcript = Answer }, $"review-{index}", CancellationToken.None);
            if (submitted.Value!.NextQuestion is not null)
            {
                question = submitted.Value.NextQuestion;
            }
        }

        var completed = await orchestrator.CompleteAsync(UserId, V2SessionId, CancellationToken.None);

        Assert.Equal(TechnicalV2OperationStatus.Ok, completed.Status);
        var main = completed.Value!.MainQuestions.Single(item => item.QuestionId == 1001);
        Assert.Equal(2, main.SubQuestions.Count);
        Assert.All(main.SubQuestions, item => Assert.Equal(main.SessionQuestionId, item.ParentSessionQuestionId));
    }

    [Fact]
    public async Task V2Routing_RejectsLegacyAndNonTechnicalSessions()
    {
        using var context = TestDbContextFactory.Create();
        Seed(context);
        context.InterviewSessions.AddRange(
            new InterviewSession
            {
                InterviewSessionId = 502,
                InterviewCampaignId = 450,
                InterviewRoundType = InterviewRoundType.Technical,
                TechnicalRuntimeVersion = "LEGACY",
                Difficulty = QuestionDifficultyEnum.Medium,
                QuestionCount = 3,
                Status = InterviewSessionStatus.Active,
                CreatedAt = DateTime.UtcNow
            },
            new InterviewSession
            {
                InterviewSessionId = 503,
                InterviewCampaignId = 450,
                InterviewRoundType = InterviewRoundType.Behavior,
                TechnicalRuntimeVersion = "V2",
                Difficulty = QuestionDifficultyEnum.Medium,
                QuestionCount = 3,
                Status = InterviewSessionStatus.Active,
                CreatedAt = DateTime.UtcNow
            });
        context.SaveChanges();
        var (orchestrator, _) = CreateOrchestrator(context);

        var legacy = await orchestrator.GetStateAsync(UserId, 502, CancellationToken.None);
        var behavioral = await orchestrator.GetStateAsync(UserId, 503, CancellationToken.None);

        Assert.Equal(TechnicalV2OperationStatus.Conflict, legacy.Status);
        Assert.Equal("RUNTIME_VERSION_REQUIRED", legacy.ErrorCode);
        Assert.Equal(TechnicalV2OperationStatus.BadRequest, behavioral.Status);
        Assert.Equal("WRONG_ROUND_TYPE", behavioral.ErrorCode);
    }

    [Fact]
    public async Task SubmitAnswer_ProviderFailurePersistsValidFiveCriterionFallback()
    {
        using var context = TestDbContextFactory.Create();
        Seed(context);
        var (orchestrator, _) = CreateOrchestrator(context, failEvaluation: true);

        await orchestrator.InitializeAsync(UserId, V2SessionId, new InitializeTechnicalV2Request(), CancellationToken.None);
        var started = await orchestrator.StartAsync(UserId, V2SessionId, CancellationToken.None);
        var submitted = await orchestrator.SubmitAnswerAsync(
            UserId,
            V2SessionId,
            started.Value!.SessionQuestionId,
            new SubmitTechnicalV2AnswerRequest { Transcript = Answer },
            "fallback-1",
            CancellationToken.None);

        Assert.Equal(TechnicalAnswerEvaluationStatus.Fallback.ToString(), submitted.Value!.EvaluationStatus);
        var answer = context.TechnicalAnswers.Single();
        var dimensions = System.Text.Json.JsonSerializer.Deserialize<List<TechnicalV2DimensionEvaluation>>(
            answer.AiCriteriaDetailJson!,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Contains("\"missingEvidence\"", answer.AiCriteriaDetailJson!);
        Assert.DoesNotContain("\"strengths\"", answer.AiCriteriaDetailJson!);
        Assert.DoesNotContain("\"gaps\"", answer.AiCriteriaDetailJson!);
        Assert.DoesNotContain("\"overallScore\"", answer.AiCriteriaDetailJson!);
        Assert.DoesNotContain("\"summary\"", answer.AiCriteriaDetailJson!);
        Assert.Equal(5, dimensions.Count);
        Assert.All(dimensions, item => Assert.Equal(0m, item.SuggestedScore));
    }

    [Fact]
    public async Task SubmitAnswer_MalformedProviderOutputPersistsRawRecoveryDiagnostics()
    {
        using var context = TestDbContextFactory.Create();
        Seed(context);
        var (orchestrator, provider) = CreateOrchestrator(context);
        provider.Setup(item => item.EvaluateAnswerV2Async(It.IsAny<TechnicalV2AnswerProcessingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIProviderResult<TechnicalV2EvaluationResponse>
            {
                Success = false,
                ErrorCode = "MALFORMED_JSON_UNRECOVERABLE",
                RawResponse = "{\"evaluation\": { broken",
                JsonRecovery = new AiJsonRecoveryMetadata
                {
                    RecoveryStatus = "UNRECOVERABLE",
                    RecoveryFlags = new[] { "JSON_RECOVERED_LEADING_TEXT" },
                    ExceptionType = "JsonException",
                    JsonErrorPath = "$.evaluation",
                    JsonErrorOffset = 15
                }
            });

        await orchestrator.InitializeAsync(UserId, V2SessionId, new InitializeTechnicalV2Request(), CancellationToken.None);
        var question = await orchestrator.StartAsync(UserId, V2SessionId, CancellationToken.None);
        await orchestrator.SubmitAnswerAsync(
            UserId,
            V2SessionId,
            question.Value!.SessionQuestionId,
            new SubmitTechnicalV2AnswerRequest { Transcript = Answer },
            "raw-diagnostics",
            CancellationToken.None);

        var log = Assert.Single(context.AIInteractionLogs.Where(item => item.OperationType == AIInteractionOperationType.AnswerEvaluation));
        Assert.Equal("{\"evaluation\": { broken", log.RawResponse);
        Assert.Equal("UNRECOVERABLE", log.RecoveryStatus);
        Assert.Equal("JSON_RECOVERED_LEADING_TEXT", log.RecoveryFlags);
        Assert.Equal("JsonException", log.JsonExceptionType);
        Assert.Equal("$.evaluation", log.JsonErrorPath);
        Assert.Equal(15, log.JsonErrorOffset);
        Assert.Equal("technical-v2-runtime", log.SchemaVersion);
    }

    [Fact]
    public async Task SubmitAnswer_PartialEvaluationPersistsOnlyTheInvalidCriterionAsZero()
    {
        using var context = TestDbContextFactory.Create();
        Seed(context);
        var (orchestrator, _) = CreateOrchestrator(context, invalidEvidenceCriterion: "REASONING");

        await orchestrator.InitializeAsync(UserId, V2SessionId, new InitializeTechnicalV2Request(), CancellationToken.None);
        var started = await orchestrator.StartAsync(UserId, V2SessionId, CancellationToken.None);
        var submitted = await orchestrator.SubmitAnswerAsync(
            UserId,
            V2SessionId,
            started.Value!.SessionQuestionId,
            new SubmitTechnicalV2AnswerRequest { Transcript = Answer },
            "partial-1",
            CancellationToken.None);

        Assert.Equal(TechnicalAnswerEvaluationStatus.Partial.ToString(), submitted.Value!.EvaluationStatus);
        Assert.False(submitted.Value.FallbackUsed);
        var answer = context.TechnicalAnswers.Single();
        Assert.Equal(6.4m, answer.FinalQuestionScore);
        Assert.Equal("INVALID_V2_EVIDENCE", answer.AiErrorCode);
        var dimensions = System.Text.Json.JsonSerializer.Deserialize<List<TechnicalV2DimensionEvaluation>>(
            answer.AiCriteriaDetailJson!,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal(5, dimensions.Count);
        Assert.Equal(0m, dimensions.Single(item => item.RubricCode == "REASONING").SuggestedScore);
        Assert.All(dimensions.Where(item => item.RubricCode != "REASONING"), item => Assert.Equal(8m, item.SuggestedScore));
        var log = Assert.Single(context.AIInteractionLogs.Where(item => item.OperationType == AIInteractionOperationType.AnswerEvaluation));
        Assert.Equal(AIInteractionStatus.Succeeded, log.Status);
        Assert.False(log.FallbackUsed);
        Assert.Equal("INVALID_V2_EVIDENCE", log.ErrorCode);
    }

    [Fact]
    public async Task SubmitAnswer_ApplicationZeroWithoutEvidenceRemainsCompleted()
    {
        using var context = TestDbContextFactory.Create();
        Seed(context);
        var (orchestrator, provider) = CreateOrchestrator(context);
        provider.Setup(item => item.EvaluateAnswerV2Async(It.IsAny<TechnicalV2AnswerProcessingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var evaluation = ValidEvaluation();
                var application = evaluation.Data!.Evaluation!.DimensionEvaluations!
                    .Single(item => item.RubricCode == "APPLICATION");
                application.SuggestedScore = 0m;
                application.Evidence = new List<string>();
                application.MissingEvidence = new List<string> { "No concrete real-world application/example was provided." };
                return evaluation;
            });

        await orchestrator.InitializeAsync(UserId, V2SessionId, new InitializeTechnicalV2Request(), CancellationToken.None);
        var question = await orchestrator.StartAsync(UserId, V2SessionId, CancellationToken.None);
        var submitted = await orchestrator.SubmitAnswerAsync(
            UserId,
            V2SessionId,
            question.Value!.SessionQuestionId,
            new SubmitTechnicalV2AnswerRequest { Transcript = Answer },
            "application-zero",
            CancellationToken.None);

        Assert.Equal(TechnicalAnswerEvaluationStatus.Completed.ToString(), submitted.Value!.EvaluationStatus);
        var dimensions = System.Text.Json.JsonSerializer.Deserialize<List<TechnicalV2DimensionEvaluation>>(
            context.TechnicalAnswers.Single().AiCriteriaDetailJson!,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal(0m, dimensions.Single(item => item.RubricCode == "APPLICATION").SuggestedScore);
        Assert.All(dimensions.Where(item => item.RubricCode != "APPLICATION"), item => Assert.Equal(8m, item.SuggestedScore));
    }

    [Fact]
    public async Task SubmitAnswer_UsesExplicitSessionProviderInAnswerAndInteractionLog()
    {
        using var context = TestDbContextFactory.Create();
        Seed(context);
        context.InterviewSessions.Single(item => item.InterviewSessionId == V2SessionId).TechnicalAiProvider = "ollama";
        var (orchestrator, _) = CreateOrchestrator(context, providerName: "ollama");

        await orchestrator.InitializeAsync(UserId, V2SessionId, new InitializeTechnicalV2Request(), CancellationToken.None);
        var started = await orchestrator.StartAsync(UserId, V2SessionId, CancellationToken.None);
        await orchestrator.SubmitAnswerAsync(
            UserId,
            V2SessionId,
            started.Value!.SessionQuestionId,
            new SubmitTechnicalV2AnswerRequest { Transcript = Answer },
            "ollama-provider-1",
            CancellationToken.None);

        Assert.Equal("ollama", context.TechnicalAnswers.Single().AiProvider);
        Assert.NotEmpty(context.AIInteractionLogs);
        Assert.All(context.AIInteractionLogs, item => Assert.Equal("ollama", item.Provider));
    }

    [Fact]
    public async Task Complete_AggregatesCanonicalQuestionScoresIntoTechnicalRoundResult()
    {
        using var context = TestDbContextFactory.Create();
        Seed(context);
        var (orchestrator, _) = CreateOrchestrator(context);

        await orchestrator.InitializeAsync(UserId, V2SessionId, new InitializeTechnicalV2Request(), CancellationToken.None);
        var question = (await orchestrator.StartAsync(UserId, V2SessionId, CancellationToken.None)).Value!;
        for (var index = 0; index < 3; index++)
        {
            var submitted = await orchestrator.SubmitAnswerAsync(
                UserId,
                V2SessionId,
                question.SessionQuestionId,
                new SubmitTechnicalV2AnswerRequest { Transcript = Answer },
                $"aggregate-{index}",
                CancellationToken.None);
            if (submitted.Value!.NextQuestion is not null)
            {
                question = submitted.Value.NextQuestion;
            }
        }

        var completed = await orchestrator.CompleteAsync(UserId, V2SessionId, CancellationToken.None);

        Assert.Equal(TechnicalV2OperationStatus.Ok, completed.Status);
        Assert.Equal(8m, completed.Value!.OverallScore);
        var result = context.TechnicalRoundResults.Single();
        Assert.Equal(8m, result.OverallScore);
        Assert.NotNull(result.SkillScoresJson);
        Assert.NotNull(result.CriteriaAveragesJson);
        Assert.Equal("GOOD", result.AiLevelAssessment);
        Assert.Equal("NOT_STARTED", result.FinalFeedbackStatus);
    }

    [Fact]
    public async Task GenerateFeedback_PersistsFinalFeedbackSnapshotAndSafeFallbackFields()
    {
        using var context = TestDbContextFactory.Create();
        Seed(context);
        var (orchestrator, _) = CreateOrchestrator(context);

        await orchestrator.InitializeAsync(UserId, V2SessionId, new InitializeTechnicalV2Request(), CancellationToken.None);
        var question = (await orchestrator.StartAsync(UserId, V2SessionId, CancellationToken.None)).Value!;
        for (var index = 0; index < 3; index++)
        {
            var submitted = await orchestrator.SubmitAnswerAsync(
                UserId,
                V2SessionId,
                question.SessionQuestionId,
                new SubmitTechnicalV2AnswerRequest { Transcript = Answer },
                $"feedback-{index}",
                CancellationToken.None);
            if (submitted.Value!.NextQuestion is not null) question = submitted.Value.NextQuestion;
        }

        var feedback = await orchestrator.GenerateFeedbackAsync(UserId, V2SessionId, CancellationToken.None);

        Assert.Equal(TechnicalV2OperationStatus.Ok, feedback.Status);
        var result = context.TechnicalRoundResults.Single();
        Assert.Equal("FALLBACK", result.FinalFeedbackStatus);
        Assert.NotNull(result.FinalFeedbackJson);
        Assert.False(string.IsNullOrWhiteSpace(result.AiExecutiveSummary));
        Assert.False(string.IsNullOrWhiteSpace(result.AiLevelAssessment));
        Assert.NotNull(feedback.Value!.Summary);
        Assert.False(string.IsNullOrWhiteSpace(feedback.Value.Summary.LevelAssessment));
    }

    [Fact]
    public void V2Model_UsesCanonicalCascadeAndUniqueRelationships()
    {
        using var context = TestDbContextFactory.Create();

        var questionSet = context.Model.FindEntityType(typeof(TechnicalQuestionSet))!;
        var sessionQuestion = context.Model.FindEntityType(typeof(TechnicalSessionQuestion))!;
        var answer = context.Model.FindEntityType(typeof(TechnicalAnswer))!;
        var result = context.Model.FindEntityType(typeof(TechnicalRoundResult))!;

        Assert.Equal(DeleteBehavior.Cascade, questionSet.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(InterviewSession)).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, sessionQuestion.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(TechnicalQuestionSet)).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, sessionQuestion.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(Question)).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, answer.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(TechnicalSessionQuestion)).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, result.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(InterviewSession)).DeleteBehavior);
        Assert.Contains(questionSet.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(TechnicalQuestionSet.InterviewSessionId) }));
        Assert.Contains(answer.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(TechnicalAnswer.TechnicalSessionQuestionId) }));
    }

    private static (TechnicalV2InterviewOrchestrator Orchestrator, Mock<ITechnicalInterviewAIProvider> Provider) CreateOrchestrator(
        ApplicationDbContext context,
        bool failEvaluation = false,
        string? invalidEvidenceCriterion = null,
        string providerName = "test-ai")
    {
        var questions = context.Questions.OrderBy(question => question.QuestionId).ToList();
        var selection = new Mock<ITechnicalQuestionSelectionService>();
        selection.Setup(item => item.PreparePoolAsync(It.IsAny<TechnicalSelectionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TechnicalQuestionPoolResult { Candidates = questions });
        selection.Setup(item => item.SelectMainQuestionsWithAIAsync(It.IsAny<TechnicalSelectionContext>(), It.IsAny<IReadOnlyList<Question>>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Question>?)null);
        selection.Setup(item => item.SelectBankSubQuestionAsync(
                It.IsAny<TechnicalLockedMainQuestionSnapshot>(),
                It.IsAny<TechnicalSessionQuestionType>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TechnicalLockedMainQuestionSnapshot main, TechnicalSessionQuestionType type, int followUpNumber, CancellationToken _) =>
            {
                var content = type == TechnicalSessionQuestionType.Clarification
                    ? main.ClarificationQuestion
                    : followUpNumber == 1
                        ? main.FollowUp1
                        : followUpNumber == 2
                            ? main.FollowUp2
                            : null;
                return string.IsNullOrWhiteSpace(content)
                    ? new TechnicalBankSubQuestionResult(false, main.SelectedQuestionId, null, "QUESTION_BANK_SUBQUESTION_UNAVAILABLE")
                    : new TechnicalBankSubQuestionResult(true, main.SelectedQuestionId, content, null);
            });

        var provider = new Mock<ITechnicalInterviewAIProvider>();
        provider.SetupGet(item => item.ProviderName).Returns(providerName);
        provider.Setup(item => item.EvaluateAnswerV2Async(It.IsAny<TechnicalV2AnswerProcessingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (failEvaluation)
                    return new AIProviderResult<TechnicalV2EvaluationResponse> { Success = false, ErrorCode = "AI_TIMEOUT", Model = "test-model" };

                var evaluation = ValidEvaluation();
                if (!string.IsNullOrWhiteSpace(invalidEvidenceCriterion))
                {
                    evaluation.Data!.Evaluation!.DimensionEvaluations!
                        .Single(item => item.RubricCode == invalidEvidenceCriterion)
                        .Evidence = null;
                }

                return evaluation;
            });
        provider.Setup(item => item.GenerateFinalSummaryAsync(It.IsAny<TechnicalAIFinalSummaryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIProviderResult<TechnicalAIFinalSummaryResponse>
            {
                Success = false,
                ErrorCode = "AI_FEEDBACK_FAILED",
                Model = "test-model"
            });
        var resolver = new Mock<ITechnicalInterviewAIProviderResolver>();
        resolver.Setup(item => item.Resolve()).Returns(provider.Object);
        resolver.Setup(item => item.ResolveFor(It.IsAny<string>())).Returns(provider.Object);
        var rubricProvider = new Mock<ITechnicalRubricProvider>();
        rubricProvider.Setup(item => item.GetRequired("technical-v2-runtime")).Returns(TestRubric());

        var lifecycle = new Mock<IInterviewSessionService>();
        lifecycle.Setup(item => item.StartSessionAsync(UserId, V2SessionId))
            .ReturnsAsync((true, (string?)null, (InterviewCampaignDto?)null));

        return (
            new TechnicalV2InterviewOrchestrator(
                context,
                selection.Object,
                resolver.Object,
                new TechnicalAIResponseValidator(),
                rubricProvider.Object,
                new TechnicalRubricScoringService(),
                lifecycle.Object,
                NullLogger<TechnicalV2InterviewOrchestrator>.Instance),
            provider);
    }

    private static AIProviderResult<TechnicalV2EvaluationResponse> ValidEvaluation() => new()
    {
        Success = true,
        Model = "test-model",
        Data = new TechnicalV2EvaluationResponse
        {
            Evaluation = new TechnicalV2EvaluationPayload
            {
                DimensionEvaluations = new List<TechnicalV2DimensionEvaluation>
                {
                    new() { RubricCode = "ACCURACY", SuggestedScore = 8m, Evidence = new List<string> { Answer }, MissingEvidence = new List<string>() },
                    new() { RubricCode = "TECHNICAL_DEPTH", SuggestedScore = 8m, Evidence = new List<string> { Answer }, MissingEvidence = new List<string>() },
                    new() { RubricCode = "REASONING", SuggestedScore = 8m, Evidence = new List<string> { Answer }, MissingEvidence = new List<string>() },
                    new() { RubricCode = "APPLICATION", SuggestedScore = 8m, Evidence = new List<string> { Answer }, MissingEvidence = new List<string>() },
                    new() { RubricCode = "COMMUNICATION", SuggestedScore = 8m, Evidence = new List<string> { Answer }, MissingEvidence = new List<string>() }
                }
            }
        }
    };

    private static AIProviderResult<TechnicalV2EvaluationResponse> EvaluationWithScore(decimal score)
    {
        var evaluation = ValidEvaluation();
        foreach (var dimension in evaluation.Data!.Evaluation!.DimensionEvaluations!)
        {
            dimension.SuggestedScore = score;
        }
        return evaluation;
    }

    private static TechnicalRubricDefinition TestRubric() => new()
    {
        Version = "technical-v2-runtime",
        ScoringPolicyVersion = "technical-v2-scoring",
        MinimumScore = 0m,
        MaximumScore = 10m,
        RoundingPrecision = 2,
        EvidenceRequiredWhenScoreAbove = 0m,
        Dimensions = new List<TechnicalRubricDimension>
        {
            new() { Code = "ACCURACY", Name = "Accuracy", Weight = .30m },
            new() { Code = "TECHNICAL_DEPTH", Name = "Technical Depth", Weight = .25m },
            new() { Code = "REASONING", Name = "Reasoning", Weight = .20m },
            new() { Code = "APPLICATION", Name = "Application", Weight = .15m },
            new() { Code = "COMMUNICATION", Name = "Communication", Weight = .10m }
        },
        Levels = Enumerable.Range(0, 11).Select(score => new TechnicalRubricLevel { Score = score, Code = $"SCORE_{score}" }).ToList(),
        PerformanceBands = new List<TechnicalPerformanceBand>
        {
            new() { Code = "EXCELLENT", Minimum = 8.5m, Maximum = 10m },
            new() { Code = "GOOD", Minimum = 6.5m, Maximum = 8.5m, MaximumExclusive = true },
            new() { Code = "DEVELOPING", Minimum = 4m, Maximum = 6.5m, MaximumExclusive = true },
            new() { Code = "NEEDS_IMPROVEMENT", Minimum = 0m, Maximum = 4m, MaximumExclusive = true }
        },
        Limits = new TechnicalQuestionLimits
        {
            MaxClarificationsPerMainQuestion = 1,
            MaxFollowUpsPerMainQuestion = 2,
            MaxTotalSubQuestionsPerMainQuestion = 3
        }
    };

    private static void Seed(ApplicationDbContext context)
    {
        var now = DateTime.UtcNow;
        context.Users.Add(new User { UserId = UserId, RoleId = 1, FullName = "Candidate", Email = "candidate@example.com", CreatedAt = now });
        context.CVFiles.Add(new CVFile { CVFileId = 201, UserId = UserId, FileName = "cv.pdf", FilePath = "cv.pdf", FileType = "application/pdf", Status = CVFileStatus.Confirmed, UploadedAt = now });
        context.JDFiles.Add(new JDFile { JDFileId = 301, UserId = UserId, RawText = "Backend role", Status = JDFileStatus.Confirmed, UploadedAt = now });
        context.CVExtractedProfiles.Add(new CVExtractedProfile { ExtractedProfileId = 401, CVFileId = 201, IsConfirmed = true, CreatedAt = now });
        context.JDExtractedProfiles.Add(new JDExtractedProfile { ExtractedProfileId = 402, JDFileId = 301, JobTitle = "Backend Developer", RoleTarget = "Backend Developer", ExperienceLevel = "Senior", RequiredSkills = "[\"C#\",\"Docker\",\"Kubernetes\"]", IsConfirmed = true, CreatedAt = now });
        context.InterviewCampaigns.Add(new InterviewCampaign { InterviewCampaignId = 450, UserId = UserId, CVExtractedProfileId = 401, JDExtractedProfileId = 402, Language = "vi", Mode = InterviewMode.RealTest, Status = InterviewCampaignStatus.Active, CreatedAt = now });
        context.InterviewSessions.Add(new InterviewSession { InterviewSessionId = V2SessionId, InterviewCampaignId = 450, InterviewRoundType = InterviewRoundType.Technical, TechnicalRuntimeVersion = "V2", Difficulty = QuestionDifficultyEnum.Hard, QuestionCount = 3, Status = InterviewSessionStatus.Active, CreatedAt = now });
        context.Questions.AddRange(
            CreateQuestion(1001, "C#"),
            CreateQuestion(1002, "Docker"),
            CreateQuestion(1003, "Kubernetes"));
        context.SaveChanges();
    }

    private static Question CreateQuestion(int questionId, string skill) => new()
    {
        QuestionId = questionId,
        UserId = UserId,
        QuestionType = "Technical",
        Language = "vi",
        RoleTarget = "Backend Developer",
        ExperienceLevel = "Senior",
        Skill = skill,
        Difficulty = QuestionDifficultyEnum.Hard,
        QuestionContent = $"Explain {skill}.",
        SuggestedAnswer = $"Expected {skill} answer.",
        ExpectedKeyPoints = "key point",
        ScoringRubric = "rubric",
        Major = "Software Engineering",
        CreatedAt = DateTime.UtcNow
    };
}
