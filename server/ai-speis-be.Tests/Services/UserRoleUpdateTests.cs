using ai_speis_be.Models;
using ai_speis_be.Repositories.UserRepo;
using ai_speis_be.Services.FileValidatorService;
using ai_speis_be.Services.NotificationService;
using ai_speis_be.Services.UserService;
using Moq;

namespace ai_speis_be.Tests.Services;

public sealed class UserRoleUpdateTests
{
    [Fact]
    public async Task UpdateUserRoleAsync_AdminToUser_IsForbiddenAndDoesNotPersist()
    {
        var repository = new Mock<IUserRepository>();
        var admin = CreateUser(10, 1, "admin");
        repository
            .Setup(repo => repo.GetUserByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(admin);
        var service = CreateService(repository);

        var result = await service.UpdateUserRoleAsync(10, "user");

        Assert.Equal(UpdateUserRoleOutcome.AdminDemotionForbidden, result.Outcome);
        Assert.Equal(1, admin.RoleId);
        repository.Verify(
            repo => repo.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateUserRoleAsync_UserToAdmin_UpdatesRole()
    {
        var repository = new Mock<IUserRepository>();
        var user = CreateUser(20, 2, "user");
        repository
            .Setup(repo => repo.GetUserByIdAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var service = CreateService(repository);

        var result = await service.UpdateUserRoleAsync(20, "admin");

        Assert.Equal(UpdateUserRoleOutcome.Updated, result.Outcome);
        Assert.Equal(1, user.RoleId);
        repository.Verify(
            repo => repo.UpdateUserAsync(user, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static UserService CreateService(Mock<IUserRepository> repository) =>
        new(
            repository.Object,
            Mock.Of<IFileValidatorService>(),
            Mock.Of<INotificationEventPublisher>());

    private static User CreateUser(int userId, int roleId, string roleName) => new()
    {
        UserId = userId,
        RoleId = roleId,
        Role = new Role
        {
            RoleId = roleId,
            RoleName = roleName,
            Description = roleName,
            Status = true
        },
        FullName = "Test User",
        Email = $"user{userId}@example.com",
        CreatedAt = DateTime.UtcNow
    };
}
