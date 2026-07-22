using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.QuestionRepo;
using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.TechnicalInterviews.Planning;
using ai_speis_be.TechnicalInterviews.Selection;
using ai_speis_be.TechnicalInterviews.Validation;
using Moq;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalQuestionSelectionServiceTests
{
    [Theory]
    [InlineData(TechnicalAttemptType.Clarification, 1, "Bank clarification")]
    [InlineData(TechnicalAttemptType.FollowUp, 1, "Bank follow-up 1")]
    [InlineData(TechnicalAttemptType.FollowUp, 2, "Bank follow-up 2")]
    public async Task SelectBankSubQuestionAsync_ReturnsVerbatimLockedQuestionBankProbe(
        TechnicalAttemptType attemptType,
        int followUpNumber,
        string expected)
    {
        var repository = new Mock<IQuestionRepoitory>();
        var service = new TechnicalQuestionSelectionService(
            repository.Object,
            new TechnicalInterviewOptions());

        var result = await service.SelectBankSubQuestionAsync(
            CreateLockedSnapshot(),
            attemptType,
            followUpNumber,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.SourceQuestionId);
        Assert.Equal(expected, result.Content);
        repository.Verify(item => item.GetQuestionByIdAsync(
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SelectBankSubQuestionAsync_DoesNotGenerateFallbackWhenBankProbeIsMissing()
    {
        var repository = new Mock<IQuestionRepoitory>();
        repository.Setup(item => item.GetQuestionByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Question?)null);
        var service = new TechnicalQuestionSelectionService(
            repository.Object,
            new TechnicalInterviewOptions());
        var legacySnapshot = CreateLockedSnapshot() with
        {
            ClarificationQuestion = null,
            FollowUp1 = null,
            FollowUp2 = null
        };

        var result = await service.SelectBankSubQuestionAsync(
            legacySnapshot,
            TechnicalAttemptType.FollowUp,
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Content);
        Assert.Equal("QUESTION_BANK_SUBQUESTION_UNAVAILABLE", result.ErrorCode);
    }

    [Fact]
    public async Task PreparePoolAsync_UsesStableDeterministicRankingWithoutCallingAi()
    {
        var candidates = new[]
        {
            CreateQuestion(20, "Database"),
            CreateQuestion(10, "ASP.NET Core")
        };
        var repository = new Mock<IQuestionRepoitory>();
        repository.Setup(item => item.GetTechnicalCandidatesAsync(
                It.IsAny<TechnicalQuestionCandidateQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);
        var service = new TechnicalQuestionSelectionService(
            repository.Object,
            new TechnicalInterviewOptions { CandidatePoolSize = 20 });

        var result = await service.PreparePoolAsync(new TechnicalSelectionContext
        {
            Language = "vi",
            JobRole = "Backend Developer",
            ExperienceLevel = "Junior",
            Difficulty = QuestionDifficultyEnum.Medium
        }, CancellationToken.None);

        Assert.Equal(10, result.Candidates[0].QuestionId);
    }

    [Fact]
    public async Task PreparePoolAsync_RelaxesSourceWithRequiredJdSkillPriorityAndAuditReason()
    {
        var candidates = new[]
        {
            CreateQuestion(20, "Database"),
            CreateQuestion(10, "Docker")
        };
        var repository = new Mock<IQuestionRepoitory>();
        repository.Setup(item => item.GetTechnicalCandidatesAsync(
                It.IsAny<TechnicalQuestionCandidateQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);
        var service = new TechnicalQuestionSelectionService(
            repository.Object,
            new TechnicalInterviewOptions { CandidatePoolSize = 20 });

        var result = await service.PreparePoolAsync(new TechnicalSelectionContext
        {
            Language = "vi",
            JobRole = "Backend Developer",
            ExperienceLevel = "Junior",
            Difficulty = QuestionDifficultyEnum.Medium,
            PlanSlot = new TechnicalQuestionPlanSlot(
                1,
                TechnicalQuestionSourceType.CV,
                "Unavailable CV Skill",
                null,
                QuestionDifficultyEnum.Medium,
                TechnicalEvaluationObjective.CvSkillVerification),
            CvSkills = new[] { "C#" },
            JdSkills = new[] { "Docker", "Database" },
            RequiredJdSkills = new[] { "Docker" },
            AllowedDifficulties = new[] { QuestionDifficultyEnum.Medium }
        }, CancellationToken.None);

        Assert.True(result.PlanDeviation);
        Assert.Equal("RELAX_SOURCE_CONSTRAINT_REQUIRED_SKILL_PRIORITY", result.PlanDeviationReason);
        Assert.Equal(10, result.Candidates[0].QuestionId);
    }

    [Fact]
    public async Task PreparePoolAsync_ExcludesSkillsAlreadyUsedByAnotherMainQuestion()
    {
        var candidates = new[]
        {
            CreateQuestion(10, "Docker"),
            CreateQuestion(20, "Database")
        };
        var repository = new Mock<IQuestionRepoitory>();
        repository.Setup(item => item.GetTechnicalCandidatesAsync(
                It.IsAny<TechnicalQuestionCandidateQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);
        var service = new TechnicalQuestionSelectionService(
            repository.Object,
            new TechnicalInterviewOptions());

        var result = await service.PreparePoolAsync(new TechnicalSelectionContext
        {
            Language = "vi",
            JobRole = "Backend Developer",
            ExperienceLevel = "Junior",
            Difficulty = QuestionDifficultyEnum.Medium,
            PlanSlot = new TechnicalQuestionPlanSlot(
                2,
                TechnicalQuestionSourceType.JD,
                "Docker",
                null,
                QuestionDifficultyEnum.Medium,
                TechnicalEvaluationObjective.JdCoreKnowledge),
            JdSkills = new[] { "Docker", "Database" },
            RequiredJdSkills = new[] { "Docker" },
            AllowedDifficulties = new[] { QuestionDifficultyEnum.Medium },
            SkillUsage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Docker"] = 1
            }
        }, CancellationToken.None);

        Assert.DoesNotContain(result.Candidates, question => question.Skill == "Docker");
        Assert.Contains(result.Candidates, question => question.Skill == "Database");
    }

    [Fact]
    public async Task PreparePoolAsync_RelaxesExperienceButKeepsPlannedSkillAndDifficulty()
    {
        var repository = new Mock<IQuestionRepoitory>();
        repository.Setup(item => item.GetTechnicalCandidatesAsync(
                It.Is<TechnicalQuestionCandidateQuery>(query => query.ExperienceLevels.Count > 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Question>());
        repository.Setup(item => item.GetTechnicalCandidatesAsync(
                It.Is<TechnicalQuestionCandidateQuery>(query => query.ExperienceLevels.Count == 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateQuestion(30, "Docker") });
        var service = new TechnicalQuestionSelectionService(
            repository.Object,
            new TechnicalInterviewOptions());

        var result = await service.PreparePoolAsync(new TechnicalSelectionContext
        {
            Language = "vi",
            JobRole = "Backend Developer",
            ExperienceLevel = "Senior",
            Difficulty = QuestionDifficultyEnum.Medium,
            PlanSlot = new TechnicalQuestionPlanSlot(
                1,
                TechnicalQuestionSourceType.JD,
                "Docker",
                null,
                QuestionDifficultyEnum.Medium,
                TechnicalEvaluationObjective.JdCoreKnowledge),
            JdSkills = new[] { "Docker" },
            RequiredJdSkills = new[] { "Docker" },
            AllowedDifficulties = new[] { QuestionDifficultyEnum.Medium }
        }, CancellationToken.None);

        Assert.Equal("experience", result.Relaxation);
        Assert.False(result.PlanDeviation);
        Assert.Equal("Docker", result.Candidates.Single().Skill);
        Assert.Equal(QuestionDifficultyEnum.Medium, result.Candidates.Single().Difficulty);
    }

    private static Question CreateQuestion(int id, string skill)
    {
        return new Question
        {
            QuestionId = id,
            UserId = 1,
            QuestionType = "Technical",
            Language = "vi",
            RoleTarget = "Backend Developer",
            ExperienceLevel = "Junior",
            Skill = skill,
            Difficulty = QuestionDifficultyEnum.Medium,
            QuestionContent = $"Question {id}",
            SuggestedAnswer = "Expected",
            Major = string.Empty,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static TechnicalLockedMainQuestionSnapshot CreateLockedSnapshot()
    {
        return new TechnicalLockedMainQuestionSnapshot(
            42,
            "Main question",
            "Expected answer",
            "Expected key points",
            "Scoring rubric",
            "{}",
            "{}",
            "ASP.NET Core",
            null,
            QuestionDifficultyEnum.Medium,
            TechnicalQuestionSourceType.JD,
            TechnicalEvaluationObjective.JdCoreKnowledge,
            "vi",
            "plan-v1",
            "bank-v1",
            DateTime.UtcNow,
            "Bank clarification",
            "Bank follow-up 1",
            "Bank follow-up 2");
    }
}
