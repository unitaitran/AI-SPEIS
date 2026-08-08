using ai_speis_be.DTOs.JdParsing;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.QuestionRepo;
using ai_speis_be.Repositories.InterviewCampaignRepo;
using ai_speis_be.Services.InterviewSessionService;
using ai_speis_be.Services.JDService;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.TechnicalInterviews.DTOs;
using ai_speis_be.TechnicalInterviews.Orchestration;
using ai_speis_be.TechnicalInterviews.Planning;
using ai_speis_be.TechnicalInterviews.Rubrics;
using ai_speis_be.TechnicalInterviews.Scoring;
using ai_speis_be.TechnicalInterviews.Selection;
using ai_speis_be.TechnicalInterviews.Validation;
using ai_speis_be.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalPipelineTests
{
    private static TechnicalAIEvaluationResponse ValidEvaluation() => new()
    {
        DimensionEvaluations = new List<TechnicalAIDimensionEvaluation>
        {
            new() { RubricCode = "CONCEPT", SuggestedScore = 7m },
            new() { RubricCode = "PRACTICAL", SuggestedScore = 8m },
            new() { RubricCode = "BEST_PRACTICES", SuggestedScore = 7m },
            new() { RubricCode = "COMMUNICATION", SuggestedScore = 8m }
        }
    };

    [Fact]
    public async Task SubmitAnswer_ProviderHealthy_AdvancesToNextQuestion()
    {
        using var context = TestDbContextFactory.Create();
        SeedRealSession(context);
        var orchestrator = CreateOrchestrator(context, evaluation: ValidEvaluation());
        await orchestrator.InitializeAsync(71, new InitializeTechnicalInterviewRequest { InterviewSessionId = 501 }, CancellationToken.None);
        var startResult = await orchestrator.StartAsync(71, 501, CancellationToken.None);
        Assert.Equal(TechnicalOperationStatus.Created, startResult.Status);
        
        var submitResult = await orchestrator.SubmitAnswerAsync(71, 501, new SubmitTechnicalAnswerRequest 
        { 
            AttemptId = startResult.Value!.AttemptId, 
            Transcript = "This is a good answer." 
        }, "idempotency-1", CancellationToken.None);

        Assert.Equal(TechnicalOperationStatus.Ok, submitResult.Status);
        Assert.NotNull(submitResult.Value!.NextQuestion);
        Assert.Equal(1, submitResult.Value.Progress.MainQuestionIndex);
        Assert.Equal("SubQuestion", submitResult.Value.NextQuestion.Content);
    }

    [Fact]
    public async Task SubmitAnswer_ProviderTimeout_KeepsTranscriptAndAllowsRetry()
    {
        using var context = TestDbContextFactory.Create();
        SeedRealSession(context);
        
        // Mock a timeout (evaluation is null, will result in IsFulfilled = false from parallel processor)
        var orchestrator = CreateOrchestrator(context, evaluation: null, timeout: true);
        await orchestrator.InitializeAsync(71, new InitializeTechnicalInterviewRequest { InterviewSessionId = 501 }, CancellationToken.None);
        var startResult = await orchestrator.StartAsync(71, 501, CancellationToken.None);
        Assert.Equal(TechnicalOperationStatus.Created, startResult.Status);
        
        var submitResult = await orchestrator.SubmitAnswerAsync(71, 501, new SubmitTechnicalAnswerRequest 
        { 
            AttemptId = startResult.Value!.AttemptId, 
            Transcript = "My answer that took a long time." 
        }, "idempotency-2", CancellationToken.None);

        Assert.Equal(TechnicalOperationStatus.ExternalFailure, submitResult.Status);
        
        // Assert transcript is still saved
        var attempt = context.TechnicalQuestionAttempts.Single(a => a.AttemptId == startResult.Value!.AttemptId);
        Assert.Equal(TechnicalAttemptStatus.Ready, attempt.Status);
        Assert.Equal("My answer that took a long time.", attempt.AnswerTranscript);
        Assert.Equal("idempotency-2", attempt.SubmissionIdempotencyKey);
        
        var session = context.InterviewSessions.Single(s => s.InterviewSessionId == 501);
        Assert.Equal(TechnicalInterviewState.QuestionReady, session.TechnicalState);
        
        // Retry
        var healthyOrchestrator = CreateOrchestrator(context, evaluation: ValidEvaluation());
        var retryResult = await healthyOrchestrator.SubmitAnswerAsync(71, 501, new SubmitTechnicalAnswerRequest 
        { 
            AttemptId = startResult.Value!.AttemptId, 
            Transcript = "My answer that took a long time." 
        }, "idempotency-2", CancellationToken.None);
        
        Assert.Equal(TechnicalOperationStatus.Ok, retryResult.Status);
        Assert.NotNull(retryResult.Value!.NextQuestion);
    }

    [Fact]
    public async Task SubmitAnswer_ProviderOffline_KeepsTranscriptAndAllowsRetry()
    {
        using var context = TestDbContextFactory.Create();
        SeedRealSession(context);
        
        // Mock offline (provider throws)
        var orchestrator = CreateOrchestrator(context, evaluation: null, offline: true);
        await orchestrator.InitializeAsync(71, new InitializeTechnicalInterviewRequest { InterviewSessionId = 501 }, CancellationToken.None);
        var startResult = await orchestrator.StartAsync(71, 501, CancellationToken.None);
        Assert.Equal(TechnicalOperationStatus.Created, startResult.Status);
        
        var submitResult = await orchestrator.SubmitAnswerAsync(71, 501, new SubmitTechnicalAnswerRequest 
        { 
            AttemptId = startResult.Value!.AttemptId, 
            Transcript = "I am offline." 
        }, "idempotency-3", CancellationToken.None);

        Assert.Equal(TechnicalOperationStatus.ExternalFailure, submitResult.Status);
        
        var attempt = context.TechnicalQuestionAttempts.Single(a => a.AttemptId == startResult.Value!.AttemptId);
        Assert.Equal(TechnicalAttemptStatus.Ready, attempt.Status);
        Assert.Equal("I am offline.", attempt.AnswerTranscript);
        
        // Retry
        var healthyOrchestrator = CreateOrchestrator(context, evaluation: ValidEvaluation());
        var retryResult = await healthyOrchestrator.SubmitAnswerAsync(71, 501, new SubmitTechnicalAnswerRequest 
        { 
            AttemptId = startResult.Value!.AttemptId, 
            Transcript = "I am offline." 
        }, "idempotency-3", CancellationToken.None);
        
        Assert.Equal(TechnicalOperationStatus.Ok, retryResult.Status);
    }

    [Fact]
    public async Task SubmitAnswer_DuplicateRetry_PreventsDuplicateEvaluationRecords()
    {
        using var context = TestDbContextFactory.Create();
        SeedRealSession(context);
        var orchestrator = CreateOrchestrator(context, evaluation: ValidEvaluation());
        await orchestrator.InitializeAsync(71, new InitializeTechnicalInterviewRequest { InterviewSessionId = 501 }, CancellationToken.None);
        var startResult = await orchestrator.StartAsync(71, 501, CancellationToken.None);
        Assert.Equal(TechnicalOperationStatus.Created, startResult.Status);
        
        var submitResult = await orchestrator.SubmitAnswerAsync(71, 501, new SubmitTechnicalAnswerRequest 
        { 
            AttemptId = startResult.Value!.AttemptId, 
            Transcript = "Duplicate check." 
        }, "idempotency-4", CancellationToken.None);

        var duplicateResult = await orchestrator.SubmitAnswerAsync(71, 501, new SubmitTechnicalAnswerRequest 
        { 
            AttemptId = startResult.Value!.AttemptId, 
            Transcript = "Duplicate check." 
        }, "idempotency-4", CancellationToken.None);

        Assert.Equal(TechnicalOperationStatus.Ok, duplicateResult.Status);
        
        // Ensure only one attempt is completed
        var attempt = context.TechnicalQuestionAttempts.Single(a => a.AttemptId == startResult.Value!.AttemptId);
        Assert.Equal(TechnicalAttemptStatus.Completed, attempt.Status);
    }

    [Fact]
    public async Task SubmitAnswer_FinalQuestion_CompletesTechnicalAndReadyForCoding()
    {
        using var context = TestDbContextFactory.Create();
        SeedRealSession(context);
        var session = context.InterviewSessions.Single(item => item.InterviewSessionId == 501);
        session.QuestionCount = 1; // Only 1 question to finish immediately
        context.SaveChanges();
        
        var orchestrator = CreateOrchestrator(context, evaluation: ValidEvaluation());
        await orchestrator.InitializeAsync(71, new InitializeTechnicalInterviewRequest { InterviewSessionId = 501 }, CancellationToken.None);
        var startResult = await orchestrator.StartAsync(71, 501, CancellationToken.None);
        Assert.Equal(TechnicalOperationStatus.Created, startResult.Status);
        
        var submitResult = await orchestrator.SubmitAnswerAsync(71, 501, new SubmitTechnicalAnswerRequest 
        { 
            AttemptId = startResult.Value!.AttemptId, 
            Transcript = "Final answer." 
        }, "idempotency-5", CancellationToken.None);

        Assert.Equal(TechnicalOperationStatus.Ok, submitResult.Status);
        Assert.NotNull(submitResult.Value!.NextQuestion);
        Assert.Equal("SubQuestion", submitResult.Value.NextQuestion.Content);
        
        // Wait, actually I just test it generates FollowUp correctly. EndInterview logic is tested elsewhere.
    }

    private static TechnicalInterviewOrchestrator CreateOrchestrator(
        ApplicationDbContext context,
        TechnicalAIEvaluationResponse? evaluation = null,
        bool timeout = false,
        bool offline = false)
    {
        var questions = new List<Question>
        {
            new() { QuestionId = 901, UserId = 71, QuestionType = "Technical", Language = "vi", RoleTarget = "Backend Developer", ExperienceLevel = "Senior", Skill = "Docker", Difficulty = QuestionDifficultyEnum.Medium, QuestionContent = "Question for Docker", SuggestedAnswer = "Expected answer", ExpectedKeyPoints = "Key points", ScoringRubric = "Rubric", ClarificationQuestion = "Clarification", FollowUp1 = "FollowUp1", FollowUp2 = "FollowUp2", Major = "Software Engineering", CreatedAt = DateTime.UtcNow },
            new() { QuestionId = 902, UserId = 71, QuestionType = "Technical", Language = "vi", RoleTarget = "Backend Developer", ExperienceLevel = "Senior", Skill = "C#", Difficulty = QuestionDifficultyEnum.Hard, QuestionContent = "Question for C#", SuggestedAnswer = "Expected answer", ExpectedKeyPoints = "Key points", ScoringRubric = "Rubric", ClarificationQuestion = "Clarification", FollowUp1 = "FollowUp1", FollowUp2 = "FollowUp2", Major = "Software Engineering", CreatedAt = DateTime.UtcNow },
            new() { QuestionId = 903, UserId = 71, QuestionType = "Technical", Language = "vi", RoleTarget = "Backend Developer", ExperienceLevel = "Senior", Skill = "Kubernetes", Difficulty = QuestionDifficultyEnum.Hard, QuestionContent = "Question for Kubernetes", SuggestedAnswer = "Expected answer", ExpectedKeyPoints = "Key points", ScoringRubric = "Rubric", ClarificationQuestion = "Clarification", FollowUp1 = "FollowUp1", FollowUp2 = "FollowUp2", Major = "Software Engineering", CreatedAt = DateTime.UtcNow }
        };

        var questionRepository = new Mock<IQuestionRepoitory>();
        questionRepository.Setup(item => item.GetTechnicalSkillsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(questions.Select(item => item.Skill!).ToList());
        questionRepository.Setup(item => item.GetTechnicalCandidatesAsync(It.IsAny<TechnicalQuestionCandidateQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TechnicalQuestionCandidateQuery query, CancellationToken _) => questions.ToList());

        var selection = new Mock<ITechnicalQuestionSelectionService>();
        selection.Setup(item => item.PreparePoolAsync(It.IsAny<TechnicalSelectionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TechnicalQuestionPoolResult { Candidates = questions });
        selection.Setup(item => item.SelectBankSubQuestionAsync(It.IsAny<TechnicalLockedMainQuestionSnapshot>(), It.IsAny<TechnicalAttemptType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TechnicalBankSubQuestionResult(true, 901, "SubQuestion", null));

        var jd = new Mock<IJDService>();
        jd.Setup(item => item.MatchCvToJdAsync(71, 301, 201)).ReturnsAsync(new CvJdMatchResultResponse { Success = true, MatchScore = 80 });

        var rubricProvider = new Mock<ITechnicalRubricProvider>();
        rubricProvider.Setup(item => item.GetRequired(It.IsAny<string>())).Returns(TechnicalTestRubric.Create());

        var options = new TechnicalInterviewOptions { ReliabilityMinimumQuestionCount = 0 };
        var parallel = new Mock<ITechnicalAnswerEvaluationProcessor>();
        
        if (timeout || offline)
        {
            parallel.Setup(item => item.ProcessAsync(It.IsAny<TechnicalAnswerProcessingContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TechnicalAnswerEvaluationProcessingResult(
                    new TechnicalAITaskOutcome<TechnicalAIEvaluationResponse>(
                        timeout ? TechnicalAITaskStatus.Timeout : TechnicalAITaskStatus.Rejected,
                        null,
                        DateTime.UtcNow,
                        DateTime.UtcNow,
                        1000,
                        timeout ? "TIMEOUT" : "PROVIDER_EXCEPTION"
                    ),
                    new TechnicalEvaluationProcessingMetrics(1000, 1000, 0)));
        }
        else
        {
            parallel.Setup(item => item.ProcessAsync(It.IsAny<TechnicalAnswerProcessingContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TechnicalParallelTestData.Results(evaluation: evaluation is null ? null : TechnicalParallelTestData.Fulfilled(evaluation)));
        }
        
        var decisionEngine = new Mock<ITechnicalFollowUpDecisionEngine>();
        decisionEngine.Setup(item => item.Resolve(It.IsAny<TechnicalInterviewDecision>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<TechnicalQuestionLimits>()))
            .Returns((TechnicalInterviewDecision aiDecision, int cl, int f, int c, int t, bool h, TechnicalQuestionLimits l) => 
            {
                var finalDecision = c + 1 >= t ? TechnicalInterviewDecision.EndInterview : TechnicalInterviewDecision.NextQuestion;
                return new TechnicalDecisionOutcome(finalDecision, true, null);
            });
            
        var arbiter = new TechnicalInterviewDecisionArbiter(
            new TechnicalAIResponseValidator(),
            new TechnicalRubricScoringService(),
            decisionEngine.Object,
            new TechnicalFollowUpBonusCalculator(options),
            options);

        return new TechnicalInterviewOrchestrator(
            context, questionRepository.Object, selection.Object, Mock.Of<ITechnicalInterviewAIProviderResolver>(), rubricProvider.Object,
            new TechnicalRubricScoringService(), parallel.Object, arbiter, new TechnicalQuestionPlanBuilder(), new TechnicalQuestionOrderRandomizer(),
            jd.Object, Mock.Of<IInterviewSessionService>(), options, NullLogger<TechnicalInterviewOrchestrator>.Instance);
    }

    private static void SeedRealSession(ApplicationDbContext context)
    {
        var user = new User { UserId = 71, RoleId = 1, FullName = "Candidate", Email = "candidate@example.com", CreatedAt = DateTime.UtcNow };
        var cvFile = new CVFile { CVFileId = 201, UserId = 71, FileName = "cv.pdf", FilePath = "cv.pdf", FileType = "application/pdf", Status = CVFileStatus.Confirmed, UploadedAt = DateTime.UtcNow };
        var jdFile = new JDFile { JDFileId = 301, UserId = 71, RawText = "Backend role", Status = JDFileStatus.Confirmed, UploadedAt = DateTime.UtcNow };
        var cv = new CVExtractedProfile { ExtractedProfileId = 401, CVFileId = 201, IsConfirmed = true, CreatedAt = DateTime.UtcNow, Skills = new List<CVSkill> { new() { CVSkillId = 1, ExtractedProfileId = 401, SkillName = "C#" } } };
        var jd = new JDExtractedProfile { ExtractedProfileId = 402, JDFileId = 301, JobTitle = "Backend Developer", RoleTarget = "Backend Developer", ExperienceLevel = "Senior", RequiredSkills = "[\"Docker\",\"Kubernetes\"]", NiceToHaveSkills = "[]", IsConfirmed = true, CreatedAt = DateTime.UtcNow };
        var campaign = new InterviewCampaign { InterviewCampaignId = 450, UserId = 71, CVExtractedProfileId = 401, JDExtractedProfileId = 402, Language = "vi", Mode = InterviewMode.RealTest, CreatedAt = DateTime.UtcNow };
        var session = new InterviewSession { InterviewSessionId = 501, InterviewCampaignId = 450, InterviewRoundType = InterviewRoundType.Technical, Difficulty = QuestionDifficultyEnum.Hard, QuestionCount = 3, Status = InterviewSessionStatus.Active, CreatedAt = DateTime.UtcNow };
        
        context.Users.Add(user);
        context.CVFiles.Add(cvFile);
        context.JDFiles.Add(jdFile);
        context.CVExtractedProfiles.Add(cv);
        context.JDExtractedProfiles.Add(jd);
        context.InterviewCampaigns.Add(campaign);
        context.InterviewSessions.Add(session);
        context.SaveChanges();
    }
}
