using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.UserRepo;
using ai_speis_be.Tests.Helpers;

namespace ai_speis_be.Tests.Repositories
{
    public sealed class UserRepositoryTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly UserRepository _repository;

        public UserRepositoryTests()
        {
            _context = TestDbContextFactory.Create();
            _repository = new UserRepository(_context);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Fact]
        public async Task GetAdminUserDetailAsync_WithProfile_ReturnsDetail()
        {
            var user = await AddUserAsync();
            var profile = new UserProfile
            {
                UserId = user.UserId,
                School = "FPT University",
                Major = "Software Engineering",
                Gpa = 3.5m,
                TargetPosition = "Backend Developer",
                Gender = Gender.Female,
                CreatedAt = DateTime.UtcNow
            };

            _context.UserProfiles.Add(profile);
            await _context.SaveChangesAsync();

            var result = await _repository.GetAdminUserDetailAsync(user.UserId);

            Assert.NotNull(result);
            Assert.Equal(user.UserId, result.UserId);
            Assert.Equal("user", result.Role);
            Assert.Equal("LOCKED", result.AccountStatus);
            Assert.True(result.HasPassword);
            Assert.Equal("Policy violation", result.LockReason);
            Assert.NotNull(result.Profile);
            Assert.Equal("FPT University", result.Profile.School);
            Assert.Equal(Gender.Female, result.Profile.Gender);
        }

        [Fact]
        public async Task GetAdminUserDetailAsync_WithoutProfile_ReturnsNullProfile()
        {
            var user = await AddUserAsync();

            var result = await _repository.GetAdminUserDetailAsync(user.UserId);

            Assert.NotNull(result);
            Assert.Null(result.Profile);
        }

        [Fact]
        public async Task GetAdminUserDetailAsync_UserDoesNotExist_ReturnsNull()
        {
            var result = await _repository.GetAdminUserDetailAsync(999_999);

            Assert.Null(result);
        }

        private async Task<User> AddUserAsync()
        {
            var user = new User
            {
                RoleId = 2,
                FullName = "Test User",
                Email = $"{Guid.NewGuid():N}@example.com",
                PhoneNumber = "0123456789",
                PasswordHash = "hashed-password",
                Status = false,
                IsLocked = true,
                LockReason = "Policy violation",
                LockedAt = DateTime.UtcNow,
                LockedByUserId = 1,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }
    }
}
