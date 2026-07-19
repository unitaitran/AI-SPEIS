using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.QuestionRepo;
using ai_speis_be.Tests.Helpers;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalQuestionRepositoryTests
{
    [Fact]
    public async Task GetTechnicalCandidatesAsync_FiltersTypeActiveRoleSkillLevelLanguageAndAskedIds()
    {
        await using var context = TestDbContextFactory.Create();
        context.Users.Add(new User
        {
            UserId = 1,
            Email = "admin@example.com",
            PasswordHash = "test",
            Status = true,
            RoleId = 1
        });
        context.Questions.AddRange(
            CreateQuestion(1, "Technical", false, "vi", "Backend Developer", "ASP.NET Core", "Junior", QuestionDifficultyEnum.Medium),
            CreateQuestion(2, "CV Deep Dive", false, "vi", "Backend Developer", "ASP.NET Core", "Junior", QuestionDifficultyEnum.Medium),
            CreateQuestion(3, "Technical", true, "vi", "Backend Developer", "ASP.NET Core", "Junior", QuestionDifficultyEnum.Medium),
            CreateQuestion(4, "Technical", false, "en", "Backend Developer", "ASP.NET Core", "Junior", QuestionDifficultyEnum.Medium),
            CreateQuestion(5, "Technical", false, "vi", "Frontend Developer", "React", "Junior", QuestionDifficultyEnum.Medium));
        await context.SaveChangesAsync();
        var repository = new QuestionRepository(context);

        var result = await repository.GetTechnicalCandidatesAsync(new TechnicalQuestionCandidateQuery
        {
            Language = "vi",
            RoleTargets = new[] { "Backend Developer" },
            ExperienceLevels = new[] { "Junior" },
            Skills = new[] { "ASP.NET Core" },
            Difficulty = QuestionDifficultyEnum.Medium,
            ExcludedQuestionIds = Array.Empty<int>()
        });

        Assert.Single(result);
        Assert.Equal(1, result[0].QuestionId);
    }

    private static Question CreateQuestion(
        int id,
        string type,
        bool deleted,
        string language,
        string role,
        string skill,
        string level,
        QuestionDifficultyEnum difficulty)
    {
        return new Question
        {
            QuestionId = id,
            UserId = 1,
            QuestionContent = $"Question {id}",
            SuggestedAnswer = "Expected",
            Difficulty = difficulty,
            RoleTarget = role,
            Major = string.Empty,
            QuestionType = type,
            Language = language,
            Skill = skill,
            ExperienceLevel = level,
            IsDeleted = deleted,
            CreatedAt = DateTime.UtcNow
        };
    }
}
