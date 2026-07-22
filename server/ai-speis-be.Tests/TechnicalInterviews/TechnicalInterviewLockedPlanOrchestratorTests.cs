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

public sealed class TechnicalInterviewLockedPlanOrchestratorTests
{
    [Fact]
    public async Task InitializeAsync_LocksExactlyThreeUniqueMainQuestions_AndStartUsesSlotOne()
    {
        using var context = TestDbContextFactory.Create();
        SeedRealSession(context);
        var orchestrator = CreateOrchestrator(context);

        var initialized = await orchestrator.InitializeAsync(
            71,
            new InitializeTechnicalInterviewRequest { InterviewSessionId = 501 },
            CancellationToken.None);
        var started = await orchestrator.StartAsync(71, 501, CancellationToken.None);
        var duplicateStart = await orchestrator.StartAsync(71, 501, CancellationToken.None);

        Assert.NotNull(initialized.Value);
        Assert.Equal(3, initialized.Value!.LockedMainQuestions.Count);
        Assert.Equal(3, initialized.Value.LockedMainQuestions.Select(item => item.SelectedQuestionId).Distinct().Count());
        Assert.Equal(
            initialized.Value.LockedMainQuestions[0].SelectedQuestionId,
            started.Value!.SelectedQuestionId);
        Assert.Equal(started.Value.AttemptId, duplicateStart.Value!.AttemptId);
        Assert.Single(context.TechnicalQuestionAttempts.Where(item => item.QuestionType == TechnicalAttemptType.Main));
    }

    [Fact]
    public async Task InitializeAsync_RepeatedCallReturnsSameLockedQuestionIds()
    {
        using var context = TestDbContextFactory.Create();
        SeedRealSession(context);
        var orchestrator = CreateOrchestrator(context);

        var first = await orchestrator.InitializeAsync(
            71,
            new InitializeTechnicalInterviewRequest { InterviewSessionId = 501 },
            CancellationToken.None);
        var retry = await orchestrator.InitializeAsync(
            71,
            new InitializeTechnicalInterviewRequest { InterviewSessionId = 501 },
            CancellationToken.None);

        Assert.Equal(
            first.Value!.LockedMainQuestions.Select(item => item.SelectedQuestionId),
            retry.Value!.LockedMainQuestions.Select(item => item.SelectedQuestionId));
    }

    [Fact]
    public async Task InitializeAsync_AcceptsParsedJdAllowedByGenericCampaignLifecycle()
    {
        using var context = TestDbContextFactory.Create();
        SeedRealSession(context);
        var jd = context.JDExtractedProfiles.Single(item => item.ExtractedProfileId == 402);
        var jdFile = context.JDFiles.Single(item => item.JDFileId == 301);
        jd.IsConfirmed = false;
        jd.ConfirmedAt = null;
        jd.ConfirmedBy = null;
        jdFile.Status = JDFileStatus.ConfirmationRequired;
        context.SaveChanges();

        var result = await CreateOrchestrator(context).InitializeAsync(
            71,
            new InitializeTechnicalInterviewRequest { InterviewSessionId = 501 },
            CancellationToken.None);

        Assert.NotNull(result.Value);
        Assert.Equal(3, result.Value!.LockedMainQuestions.Count);
    }

    [Fact]
    public async Task InitializeAsync_PracticeBuildsPlanOnlyFromSkillsAvailableAtConfiguredDifficulty()
    {
        using var context = TestDbContextFactory.Create();
        SeedRealSession(context);
        var campaign = context.InterviewCampaigns.Single(item => item.InterviewCampaignId == 450);
        var session = context.InterviewSessions.Single(item => item.InterviewSessionId == 501);
        campaign.Mode = InterviewMode.Practice;
        session.QuestionCount = 1;
        session.Difficulty = QuestionDifficultyEnum.Hard;
        context.SaveChanges();

        var result = await CreateOrchestrator(context).InitializeAsync(
            71,
            new InitializeTechnicalInterviewRequest { InterviewSessionId = 501 },
            CancellationToken.None);

        Assert.NotNull(result.Value);
        Assert.Single(result.Value!.LockedMainQuestions);
        Assert.Equal("HARD", result.Value.LockedMainQuestions[0].Difficulty);
        Assert.Contains(result.Value.LockedMainQuestions[0].SelectedQuestionId, new[] { 902, 903 });
    }

    [Fact]
    public async Task StartAsync_UsesImmutableSnapshotAfterQuestionBankChanges()
    {
        using var context = TestDbContextFactory.Create();
        SeedRealSession(context);
        var questions = CreateQuestions();
        var orchestrator = CreateOrchestrator(context, questions);
        var initialized = await orchestrator.InitializeAsync(
            71,
            new InitializeTechnicalInterviewRequest { InterviewSessionId = 501 },
            CancellationToken.None);
        var firstLocked = initialized.Value!.LockedMainQuestions[0];
        var bankQuestion = questions.Single(item => item.QuestionId == firstLocked.SelectedQuestionId);
        var originalContent = bankQuestion.QuestionContent;
        bankQuestion.QuestionContent = "Changed after plan lock";
        bankQuestion.ExpectedKeyPoints = "Changed key points";

        var started = await orchestrator.StartAsync(71, 501, CancellationToken.None);

        Assert.Equal(firstLocked.SelectedQuestionId, started.Value!.SelectedQuestionId);
        Assert.Equal(originalContent, started.Value.Content);
    }

    [Fact]
    public async Task SubmitAnswerAsync_RetryDoesNotDuplicateAnswerOrChangeRemainingLockedMainQuestions()
    {
        using var context = TestDbContextFactory.Create();
        SeedRealSession(context);
        var orchestrator = CreateOrchestrator(context);
        var initialized = await orchestrator.InitializeAsync(
            71,
            new InitializeTechnicalInterviewRequest { InterviewSessionId = 501 },
            CancellationToken.None);
        var lockedIds = initialized.Value!.LockedMainQuestions
            .Select(item => item.SelectedQuestionId)
            .ToArray();
        var started = await orchestrator.StartAsync(71, 501, CancellationToken.None);
        var request = new SubmitTechnicalAnswerRequest
        {
            AttemptId = started.Value!.AttemptId,
            Transcript = "Dependency injection separates construction from use and improves testability."
        };

        var first = await orchestrator.SubmitAnswerAsync(
            71, 501, request, "technical-submit-1", CancellationToken.None);
        var retry = await orchestrator.SubmitAnswerAsync(
            71, 501, request, "technical-submit-1", CancellationToken.None);
        var refreshed = await orchestrator.GetSessionAsync(71, 501, CancellationToken.None);

        Assert.Equal(lockedIds, refreshed.Value!.LockedMainQuestions.Select(item => item.SelectedQuestionId));
        Assert.Equal(lockedIds[1], first.Value!.NextQuestion!.SelectedQuestionId);
        Assert.Equal(first.Value.NextQuestion.AttemptId, retry.Value!.NextQuestion!.AttemptId);
        Assert.Single(context.TechnicalAnswerEvaluations);
        Assert.Equal(2, context.TechnicalQuestionAttempts.Count(item => item.QuestionType == TechnicalAttemptType.Main));
    }

    [Fact]
    public async Task SubmitAnswerAsync_RubricClarificationUsesVerbatimBankProbeWithPositiveQuestionId()
    {
        using var context = TestDbContextFactory.Create();
        SeedRealSession(context);
        var evaluation = TechnicalParallelTestData.CreateEvaluation(2m, "AMBIGUOUS");
        var orchestrator = CreateOrchestrator(context, evaluation: evaluation);
        await orchestrator.InitializeAsync(
            71,
            new InitializeTechnicalInterviewRequest { InterviewSessionId = 501 },
            CancellationToken.None);
        var started = await orchestrator.StartAsync(71, 501, CancellationToken.None);

        var submitted = await orchestrator.SubmitAnswerAsync(
            71,
            501,
            new SubmitTechnicalAnswerRequest
            {
                AttemptId = started.Value!.AttemptId,
                Transcript = "Không rõ"
            },
            "technical-bank-clarification",
            CancellationToken.None);

        var next = submitted.Value!.NextQuestion!;
        Assert.Equal("CLARIFICATION", next.QuestionType);
        Assert.Equal(next.SelectedQuestionId, next.QuestionId);
        Assert.True(next.QuestionId.GetValueOrDefault() > 0);
        Assert.StartsWith("Clarification for ", next.Content);
        Assert.Contains(context.TechnicalQuestionAttempts, item =>
            item.QuestionType == TechnicalAttemptType.Clarification
            && item.QuestionId == next.QuestionId
            && item.QuestionContentSnapshot == next.Content);
    }

    [Fact]
    public async Task SubmitAnswerAsync_NewIdempotencyKeyRecoversExpiredEvaluatingAttempt()
    {
        using var context = TestDbContextFactory.Create();
        SeedRealSession(context);
        var orchestrator = CreateOrchestrator(context);
        await orchestrator.InitializeAsync(
            71,
            new InitializeTechnicalInterviewRequest { InterviewSessionId = 501 },
            CancellationToken.None);
        var started = await orchestrator.StartAsync(71, 501, CancellationToken.None);
        const string transcript = "Dependency injection separates construction from use and improves testability.";
        const string key = "technical-expired-evaluation";
        var attempt = context.TechnicalQuestionAttempts.Single(item =>
            item.AttemptId == started.Value!.AttemptId);
        var session = context.InterviewSessions.Single(item => item.InterviewSessionId == 501);
        attempt.Status = TechnicalAttemptStatus.Evaluating;
        attempt.AnswerTranscript = transcript;
        attempt.SubmissionIdempotencyKey = "expired-original-key";
        attempt.ProcessingStartedAt = DateTime.UtcNow.AddMinutes(-5);
        session.TechnicalState = TechnicalInterviewState.Evaluating;
        context.SaveChanges();

        var recovered = await orchestrator.GetSessionAsync(71, 501, CancellationToken.None);

        Assert.Equal("QUESTION_READY", recovered.Value!.Status);
        Assert.Equal(TechnicalAttemptStatus.Ready, attempt.Status);

        var result = await orchestrator.SubmitAnswerAsync(
            71,
            501,
            new SubmitTechnicalAnswerRequest
            {
                AttemptId = attempt.AttemptId,
                Transcript = transcript
            },
            key,
            CancellationToken.None);

        Assert.NotNull(result.Value);
        Assert.Single(context.TechnicalAnswerEvaluations.Where(item => item.AttemptId == attempt.AttemptId));
        Assert.NotEqual(TechnicalInterviewState.Evaluating, session.TechnicalState);
    }

    [Fact]
    public async Task InitializeAsync_ConcurrentRequestsConvergeOnOneLockedPlan()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var firstContext = TestDbContextFactory.Create(databaseName);
        SeedRealSession(firstContext);
        using var secondContext = TestDbContextFactory.Create(databaseName);
        var request = new InitializeTechnicalInterviewRequest { InterviewSessionId = 501 };

        var results = await Task.WhenAll(
            CreateOrchestrator(firstContext).InitializeAsync(71, request, CancellationToken.None),
            CreateOrchestrator(secondContext).InitializeAsync(71, request, CancellationToken.None));

        Assert.All(results, result => Assert.NotNull(result.Value));
        Assert.Equal(
            results[0].Value!.LockedMainQuestions.Select(item => item.SelectedQuestionId),
            results[1].Value!.LockedMainQuestions.Select(item => item.SelectedQuestionId));
    }

    [Fact]
    public async Task GetSessionAsync_LazyUpgradesLegacyPartialPlanExactlyOnce()
    {
        using var context = TestDbContextFactory.Create();
        SeedRealSession(context);
        var orchestrator = CreateOrchestrator(context);
        await orchestrator.InitializeAsync(
            71,
            new InitializeTechnicalInterviewRequest { InterviewSessionId = 501 },
            CancellationToken.None);
        var session = context.InterviewSessions.Single(item => item.InterviewSessionId == 501);
        var plan = TechnicalQuestionPlanSerializer.DeserializeRequired(session.TechnicalQuestionPlanJson!);
        session.TechnicalQuestionPlanJson = TechnicalQuestionPlanSerializer.Serialize(plan with
        {
            Slots = plan.Slots.SetItem(2, plan.Slots[2] with { LockedQuestion = null })
        });
        context.SaveChanges();

        var upgraded = await orchestrator.GetSessionAsync(71, 501, CancellationToken.None);
        var retry = await orchestrator.GetSessionAsync(71, 501, CancellationToken.None);

        Assert.Equal(3, upgraded.Value!.LockedMainQuestions.Count);
        Assert.Equal(
            upgraded.Value.LockedMainQuestions.Select(item => item.SelectedQuestionId),
            retry.Value!.LockedMainQuestions.Select(item => item.SelectedQuestionId));
    }

    [Fact]
    public async Task GenericCompletion_ActivatesCodingExactlyOnceOnRetry()
    {
        using var context = TestDbContextFactory.Create();
        SeedRealSession(context);
        context.InterviewSessions.Add(new InterviewSession
        {
            InterviewSessionId = 502,
            InterviewCampaignId = 450,
            InterviewRoundType = InterviewRoundType.Code,
            Difficulty = QuestionDifficultyEnum.Hard,
            QuestionCount = 3,
            Status = InterviewSessionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        context.SaveChanges();
        var lifecycle = new InterviewSessionService(
            new InterviewCampaignRepository(context),
            context,
            NullLogger<InterviewSessionService>.Instance);

        await lifecycle.CompleteSessionAsync(71, 501);
        await lifecycle.CompleteSessionAsync(71, 501);

        var active = context.InterviewSessions.Where(item => item.Status == InterviewSessionStatus.Active).ToList();
        Assert.Single(active);
        Assert.Equal(InterviewRoundType.Code, active[0].InterviewRoundType);
    }

    private static TechnicalInterviewOrchestrator CreateOrchestrator(
        ApplicationDbContext context,
        List<Question>? questionSet = null,
        TechnicalAIEvaluationResponse? evaluation = null)
    {
        var questions = questionSet ?? CreateQuestions();
        var questionRepository = new Mock<IQuestionRepoitory>();
        questionRepository.Setup(item => item.GetTechnicalSkillsAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(questions.Select(item => item.Skill!).ToList());
        questionRepository.Setup(item => item.GetTechnicalCandidatesAsync(
                It.IsAny<TechnicalQuestionCandidateQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TechnicalQuestionCandidateQuery query, CancellationToken _) =>
                questions.Where(question =>
                        (!query.Difficulty.HasValue || question.Difficulty == query.Difficulty.Value)
                        && (query.RoleTargets.Count == 0 || query.RoleTargets.Contains(question.RoleTarget))
                        && (string.IsNullOrWhiteSpace(query.Language) || question.Language == query.Language))
                    .ToList());

        var selection = new Mock<ITechnicalQuestionSelectionService>();
        selection.Setup(item => item.PreparePoolAsync(
                It.IsAny<TechnicalSelectionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TechnicalSelectionContext request, CancellationToken _) =>
                new TechnicalQuestionPoolResult
                {
                    Candidates = questions.Where(question =>
                            TechnicalQuestionMetadata.FuzzyMatches(question.Skill!, request.PlanSlot!.TargetSkill)
                            && question.Difficulty == request.PlanSlot.PlannedDifficulty)
                        .ToList()
                });
        selection.Setup(item => item.SelectBankSubQuestionAsync(
                It.IsAny<TechnicalLockedMainQuestionSnapshot>(),
                It.IsAny<TechnicalAttemptType>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TechnicalLockedMainQuestionSnapshot snapshot, TechnicalAttemptType type, int number, CancellationToken _) =>
                new TechnicalBankSubQuestionResult(
                    true,
                    snapshot.SelectedQuestionId,
                    type == TechnicalAttemptType.Clarification
                        ? snapshot.ClarificationQuestion
                        : number <= 1 ? snapshot.FollowUp1 : snapshot.FollowUp2,
                    null));

        var jd = new Mock<IJDService>();
        jd.Setup(item => item.MatchCvToJdAsync(71, 301, 201))
            .ReturnsAsync(new CvJdMatchResultResponse { Success = true, MatchScore = 80 });

        var rubricProvider = new Mock<ITechnicalRubricProvider>();
        rubricProvider.Setup(item => item.GetRequired(It.IsAny<string>()))
            .Returns(TechnicalTestRubric.Create());

        var options = new TechnicalInterviewOptions();
        var parallel = new Mock<ITechnicalAnswerParallelProcessor>();
        parallel.Setup(item => item.ProcessAsync(
                It.IsAny<TechnicalAnswerProcessingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TechnicalParallelTestData.Results(
                evaluation: evaluation is null
                    ? null
                    : TechnicalParallelTestData.Fulfilled(evaluation)));
        var arbiter = new TechnicalInterviewDecisionArbiter(
            new TechnicalAIResponseValidator(),
            new TechnicalRubricScoringService(),
            new TechnicalFollowUpDecisionEngine(),
            new TechnicalFollowUpBonusCalculator(options),
            options);

        return new TechnicalInterviewOrchestrator(
            context,
            questionRepository.Object,
            selection.Object,
            Mock.Of<ITechnicalInterviewAIProviderResolver>(),
            rubricProvider.Object,
            new TechnicalRubricScoringService(),
            parallel.Object,
            arbiter,
            new TechnicalQuestionPlanBuilder(),
            jd.Object,
            Mock.Of<IInterviewSessionService>(),
            options,
            NullLogger<TechnicalInterviewOrchestrator>.Instance);
    }

    private static List<Question> CreateQuestions() => new()
    {
        Question(901, "Docker", QuestionDifficultyEnum.Medium),
        Question(902, "C#", QuestionDifficultyEnum.Hard),
        Question(903, "Kubernetes", QuestionDifficultyEnum.Hard),
        Question(904, "SQL", QuestionDifficultyEnum.Medium)
    };

    private static Question Question(int id, string skill, QuestionDifficultyEnum difficulty) => new()
    {
        QuestionId = id,
        UserId = 71,
        QuestionType = "Technical",
        Language = "vi",
        RoleTarget = "Backend Developer",
        ExperienceLevel = "Senior",
        Skill = skill,
        Difficulty = difficulty,
        QuestionContent = $"Question for {skill}",
        SuggestedAnswer = $"Expected answer for {skill}",
        ExpectedKeyPoints = $"Key points for {skill}",
        ScoringRubric = "Use the configured technical rubric.",
        ClarificationQuestion = $"Clarification for {skill}",
        FollowUp1 = $"Follow-up one for {skill}",
        FollowUp2 = $"Follow-up two for {skill}",
        Major = "Software Engineering",
        CreatedAt = DateTime.UtcNow
    };

    private static void SeedRealSession(ApplicationDbContext context)
    {
        var user = new User
        {
            UserId = 71,
            RoleId = 1,
            FullName = "Candidate",
            Email = "candidate@example.com",
            CreatedAt = DateTime.UtcNow
        };
        var cvFile = new CVFile
        {
            CVFileId = 201,
            UserId = 71,
            FileName = "cv.pdf",
            FilePath = "cv.pdf",
            FileType = "application/pdf",
            Status = CVFileStatus.Confirmed,
            UploadedAt = DateTime.UtcNow
        };
        var jdFile = new JDFile
        {
            JDFileId = 301,
            UserId = 71,
            RawText = "Backend role",
            Status = JDFileStatus.Confirmed,
            UploadedAt = DateTime.UtcNow
        };
        var cv = new CVExtractedProfile
        {
            ExtractedProfileId = 401,
            CVFileId = 201,
            IsConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            Skills = new List<CVSkill>
            {
                new() { CVSkillId = 1, ExtractedProfileId = 401, SkillName = "C#" },
                new() { CVSkillId = 2, ExtractedProfileId = 401, SkillName = "SQL" }
            }
        };
        var jd = new JDExtractedProfile
        {
            ExtractedProfileId = 402,
            JDFileId = 301,
            JobTitle = "Backend Developer",
            RoleTarget = "Backend Developer",
            ExperienceLevel = "Senior",
            RequiredSkills = "[\"Docker\",\"Kubernetes\"]",
            NiceToHaveSkills = "[]",
            IsConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };
        var campaign = new InterviewCampaign
        {
            InterviewCampaignId = 450,
            UserId = 71,
            CVExtractedProfileId = 401,
            JDExtractedProfileId = 402,
            Language = "vi",
            Mode = InterviewMode.RealTest,
            DurationMinutes = 15,
            Status = InterviewCampaignStatus.Active,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        var session = new InterviewSession
        {
            InterviewSessionId = 501,
            InterviewCampaignId = 450,
            InterviewRoundType = InterviewRoundType.Technical,
            Difficulty = QuestionDifficultyEnum.Hard,
            QuestionCount = 3,
            Status = InterviewSessionStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        context.AddRange(user, cvFile, jdFile, cv, jd, campaign, session);
        context.SaveChanges();
    }
}
