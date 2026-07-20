using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.QuestionRepo;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
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
