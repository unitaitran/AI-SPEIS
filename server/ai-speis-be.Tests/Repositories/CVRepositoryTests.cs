using System;
using System.Linq;
using System.Threading.Tasks;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.CVRepo;
using ai_speis_be.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ai_speis_be.Tests.Repositories
{
    public class CVRepositoryTests : IDisposable
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly CVRepository _sut;

        public CVRepositoryTests()
        {
            _dbContext = TestDbContextFactory.Create();
            _sut = new CVRepository(_dbContext);
        }

        public void Dispose()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
        }

        [Fact]
        public async Task DeleteCVAsync_RemovesCvAndRelatedRowsFromDatabase()
        {
            if (!_dbContext.Roles.Any())
            {
                _dbContext.Roles.Add(new Role { RoleId = 1, RoleName = "user", Description = "User", Status = true });
                await _dbContext.SaveChangesAsync();
            }

            var user = new User
            {
                UserId = 1,
                Email = "test@example.com",
                PasswordHash = "hash",
                FullName = "Test User",
                RoleId = 1,
                Status = true,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var cvFile = new CVFile
            {
                UserId = user.UserId,
                FileName = "test-cv.pdf",
                FilePath = "/uploads/cvs/test.pdf",
                FileSize = 1024,
                FileType = "application/pdf",
                Status = CVFileStatus.Pending,
                UploadedAt = DateTime.UtcNow
            };
            _dbContext.CVFiles.Add(cvFile);
            await _dbContext.SaveChangesAsync();

            var profile = new CVExtractedProfile
            {
                CVFileId = cvFile.CVFileId,
                RoleTarget = "Backend Developer",
                Education = "[]",
                Experience = "[]",
                RawAiOutput = "{}",
                IsConfirmed = false,
                CreatedAt = DateTime.UtcNow
            };
            profile.Skills.Add(new CVSkill { SkillName = "C#", Source = "AI", Category = "Language", CreatedAt = DateTime.UtcNow });
            profile.Projects.Add(new CVProject { ProjectName = "Demo", TechnologyStack = ".NET", CreatedAt = DateTime.UtcNow });

            _dbContext.CVExtractedProfiles.Add(profile);
            await _dbContext.SaveChangesAsync();

            _dbContext.FastCheckResults.Add(new FastCheckResult
            {
                UserId = user.UserId,
                CVFileId = cvFile.CVFileId,
                JDFileId = 1,
                MatchScore = 90,
                SuitabilityLevel = "Good",
                MatchingSkillsJson = "[]",
                MissingSkillsJson = "[]",
                Advice = "Ok"
            });
            await _dbContext.SaveChangesAsync();

            var deleted = await _sut.DeleteCVAsync(cvFile.CVFileId);

            Assert.True(deleted);
            Assert.False(await _dbContext.CVFiles.AnyAsync(x => x.CVFileId == cvFile.CVFileId));
            Assert.False(await _dbContext.CVExtractedProfiles.AnyAsync(x => x.CVFileId == cvFile.CVFileId));
            Assert.False(await _dbContext.CVSkills.AnyAsync(x => x.ExtractedProfileId == profile.ExtractedProfileId));
            Assert.False(await _dbContext.CVProjects.AnyAsync(x => x.ExtractedProfileId == profile.ExtractedProfileId));
            Assert.False(await _dbContext.FastCheckResults.AnyAsync(x => x.CVFileId == cvFile.CVFileId));
        }
    }
}
