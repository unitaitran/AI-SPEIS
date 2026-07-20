using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.QuestionRepo;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.TechnicalInterviews.Planning;
using ai_speis_be.TechnicalInterviews.Selection;
using ai_speis_be.TechnicalInterviews.Validation;
using Moq;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalQuestionSelectionServiceTests
{
    [Fact]
    public async Task SelectAsync_UsesStableFallbackWhenAiReturnsIdOutsideCandidatePool()
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
        repository.Setup(item => item.GetQuestionByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates[1]);

        var provider = new Mock<ITechnicalInterviewAIProvider>();
        provider.SetupGet(item => item.ProviderName).Returns("mock");
        provider.Setup(item => item.SelectQuestionAsync(
                It.IsAny<TechnicalAISelectionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIProviderResult<TechnicalAISelectionResponse>
            {
                Success = true,
                Data = new TechnicalAISelectionResponse { SelectedQuestionId = 999 },
                Model = "mock-model"
            });
        var resolver = new Mock<ITechnicalInterviewAIProviderResolver>();
        resolver.Setup(item => item.Resolve()).Returns(provider.Object);

        var service = new TechnicalQuestionSelectionService(
            repository.Object,
            resolver.Object,
            new TechnicalAIResponseValidator(),
            new TechnicalInterviewOptions { CandidatePoolSize = 20 });

        var result = await service.SelectAsync(new TechnicalSelectionContext
        {
            Language = "vi",
            JobRole = "Backend Developer",
            ExperienceLevel = "Junior",
            Difficulty = QuestionDifficultyEnum.Medium
        }, CancellationToken.None);

        Assert.True(result.FallbackUsed);
        Assert.Equal(10, result.Question!.QuestionId);
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
        var resolver = new Mock<ITechnicalInterviewAIProviderResolver>();

        var service = new TechnicalQuestionSelectionService(
            repository.Object,
            resolver.Object,
            new TechnicalAIResponseValidator(),
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
        var resolver = new Mock<ITechnicalInterviewAIProviderResolver>();
        var service = new TechnicalQuestionSelectionService(
            repository.Object,
            resolver.Object,
            new TechnicalAIResponseValidator(),
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
}
