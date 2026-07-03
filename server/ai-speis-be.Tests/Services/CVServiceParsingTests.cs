using Xunit;
using Moq;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.DTOs.CvParsing;
using ai_speis_be.Services.CVService;
using ai_speis_be.Services.BackgroundWorker;
using ai_speis_be.Repositories.CVRepo;
using ai_speis_be.Services.FileValidatorService;
using ai_speis_be.Tests.Helpers;

namespace ai_speis_be.Tests.Services
{
    public class CVServiceParsingTests : IDisposable
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly Mock<ICVRepository> _cvRepoMock;
        private readonly Mock<IFileValidatorService> _fileValidatorMock;
        private readonly ICvParseQueue _queue;
        private readonly CVService _sut; // System Under Test

        public CVServiceParsingTests()
        {
            _dbContext = TestDbContextFactory.Create();
            _cvRepoMock = new Mock<ICVRepository>();
            _fileValidatorMock = new Mock<IFileValidatorService>();
            _queue = new CvParseQueue();
            _sut = new CVService(_cvRepoMock.Object, _fileValidatorMock.Object, _queue, _dbContext);
        }

        public void Dispose()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
        }

        // ---- Helpers ----
        private async Task<CVFile> SeedCVFile(CVFileStatus status = CVFileStatus.Pending, int userId = 1)
        {
            // Seed a Role first
            if (!_dbContext.Roles.Any())
            {
                _dbContext.Roles.Add(new Role { RoleId = 1, RoleName = "user", Description = "User", Status = true });
                await _dbContext.SaveChangesAsync();
            }

            // Seed a User
            var user = _dbContext.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                user = new User
                {
                    UserId = userId,
                    Email = $"test{userId}@test.com",
                    PasswordHash = "hash",
                    FullName = "Test User",
                    RoleId = 1,
                    Status = true,
                    CreatedAt = DateTime.Now
                };
                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();
            }

            var cvFile = new CVFile
            {
                UserId = userId,
                FileName = "test-cv.pdf",
                FilePath = "/uploads/cvs/test.pdf",
                FileSize = 1024,
                FileType = "application/pdf",
                Status = status,
                UploadedAt = DateTime.Now
            };
            _dbContext.CVFiles.Add(cvFile);
            await _dbContext.SaveChangesAsync();
            return cvFile;
        }

        private async Task<CVExtractedProfile> SeedProfile(int cvFileId)
        {
            var profile = new CVExtractedProfile
            {
                CVFileId = cvFileId,
                RoleTarget = "Backend Developer",
                Education = "[{\"School\":\"FPT\",\"Major\":\"SE\",\"Gpa\":\"3.5\",\"GraduationYear\":\"2026\"}]",
                Experience = "[]",
                RawAiOutput = "{}",
                IsConfirmed = false,
                CreatedAt = DateTime.UtcNow
            };
            profile.Skills.Add(new CVSkill { SkillName = "Java", Source = "AI", Category = "Language", CreatedAt = DateTime.UtcNow });
            profile.Skills.Add(new CVSkill { SkillName = "Spring Boot", Source = "AI", Category = "Framework", CreatedAt = DateTime.UtcNow });
            profile.Projects.Add(new CVProject { ProjectName = "Shoe Shop", TechnologyStack = "Java", CreatedAt = DateTime.UtcNow });

            _dbContext.CVExtractedProfiles.Add(profile);
            await _dbContext.SaveChangesAsync();
            return profile;
        }

        // ===================== U2: TriggerParse — CV not found =====================
        [Fact]
        public async Task TriggerParse_CVNotFound_ReturnsError()
        {
            // Act
            var (success, error) = await _sut.TriggerParseAsync(999, 1);

            // Assert
            Assert.False(success);
            Assert.Equal("Không tìm thấy file CV.", error);
        }

        // ===================== U3: TriggerParse — Wrong user =====================
        [Fact]
        public async Task TriggerParse_WrongUser_ReturnsError()
        {
            // Arrange
            var cvFile = await SeedCVFile(CVFileStatus.Pending, userId: 1);

            // Act: userId=999 tries to parse userId=1's CV
            var (success, error) = await _sut.TriggerParseAsync(cvFile.CVFileId, 999);

            // Assert
            Assert.False(success);
            Assert.Contains("không có quyền", error);
        }

        // ===================== U4: TriggerParse — Already processing =====================
        [Fact]
        public async Task TriggerParse_AlreadyProcessing_ReturnsError()
        {
            // Arrange
            var cvFile = await SeedCVFile(CVFileStatus.Processing);

            // Act
            var (success, error) = await _sut.TriggerParseAsync(cvFile.CVFileId, cvFile.UserId);

            // Assert
            Assert.False(success);
            Assert.Contains("không thể parse lại", error);
        }

        // ===================== U6: GetParseStatus — Existing CV =====================
        [Fact]
        public async Task GetParseStatus_ExistingCV_ReturnsCorrectStatus()
        {
            // Arrange
            var cvFile = await SeedCVFile(CVFileStatus.ConfirmationRequired);

            // Act
            var result = await _sut.GetParseStatusAsync(cvFile.UserId, cvFile.CVFileId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(cvFile.CVFileId, result.CVFileId);
            Assert.Equal("ConfirmationRequired", result.Status);
            Assert.Equal("test-cv.pdf", result.FileName);
        }

        // ===================== U7: GetParseStatus — Non-existent =====================
        [Fact]
        public async Task GetParseStatus_NonExistent_ReturnsNull()
        {
            var result = await _sut.GetParseStatusAsync(1, 999);
            Assert.Null(result);
        }

        // ===================== U8: GetParsedData — With profile returns clean DTO =====================
        [Fact]
        public async Task GetParsedData_WithProfile_ReturnsCleanDTO()
        {
            // Arrange
            var cvFile = await SeedCVFile(CVFileStatus.ConfirmationRequired);
            await SeedProfile(cvFile.CVFileId);

            // Act
            var result = await _sut.GetParsedDataAsync(cvFile.UserId, cvFile.CVFileId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Backend Developer", result.RoleTarget);
            Assert.False(result.IsConfirmed);
            Assert.Equal(2, result.Skills.Count);
            Assert.Single(result.Projects);
            Assert.Single(result.Education);

            // Item 2: Verify DTO is clean — no rawAiOutput, no navigation props
            var skill = result.Skills.First();
            Assert.Equal("Java", skill.SkillName);
            Assert.Equal("Language", skill.Category);
        }

        // ===================== U9: GetParsedData — No profile =====================
        [Fact]
        public async Task GetParsedData_NoProfile_ReturnsNull()
        {
            // Arrange: CV exists but no ExtractedProfile
            var cvFile = await SeedCVFile(CVFileStatus.Pending);

            // Act
            var result = await _sut.GetParsedDataAsync(cvFile.UserId, cvFile.CVFileId);

            // Assert
            Assert.Null(result);
        }

        // ===================== U10: Confirm — Valid data =====================
        [Fact]
        public async Task Confirm_ValidData_UpdatesStatusToConfirmed()
        {
            // Arrange
            var cvFile = await SeedCVFile(CVFileStatus.ConfirmationRequired);
            await SeedProfile(cvFile.CVFileId);

            var request = new CvConfirmRequest
            {
                RoleTarget = "Fullstack Developer",
                Education = new List<EducationDto> { new EducationDto { School = "FPT", Major = "SE" } },
                Experience = new List<ExperienceDto>(),
                Projects = new List<ProjectDto>
                {
                    new ProjectDto { ProjectName = "My Project", TechnologyStack = "C#, React" }
                },
                Skills = new List<SkillDto>
                {
                    new SkillDto { SkillName = "C#", Category = "Language" },
                    new SkillDto { SkillName = "React", Category = "Framework" }
                }
            };

            // Act
            var (success, error) = await _sut.ConfirmParsedDataAsync(cvFile.CVFileId, cvFile.UserId, request);

            // Assert
            Assert.True(success);
            Assert.Null(error);

            // Verify DB state
            var updatedCv = await _dbContext.CVFiles.FindAsync(cvFile.CVFileId);
            Assert.Equal(CVFileStatus.Confirmed, updatedCv!.Status);

            var profile = _dbContext.CVExtractedProfiles.First(p => p.CVFileId == cvFile.CVFileId);
            Assert.True(profile.IsConfirmed);
            Assert.Equal("Fullstack Developer", profile.RoleTarget);
            Assert.Equal(cvFile.UserId, profile.ConfirmedBy);
        }

        // ===================== U11: Confirm — Empty skills (BR-27) =====================
        [Fact]
        public async Task Confirm_EmptySkills_ReturnsError()
        {
            // Arrange
            var cvFile = await SeedCVFile(CVFileStatus.ConfirmationRequired);
            await SeedProfile(cvFile.CVFileId);

            var request = new CvConfirmRequest
            {
                RoleTarget = "Backend Developer",
                Skills = new List<SkillDto>() // Empty!
            };

            // Act
            var (success, error) = await _sut.ConfirmParsedDataAsync(cvFile.CVFileId, cvFile.UserId, request);

            // Assert
            Assert.False(success);
            Assert.Contains("ít nhất 1 skill", error);
        }

        // ===================== U12: Confirm — Wrong status =====================
        [Fact]
        public async Task Confirm_WrongStatus_ReturnsError()
        {
            // Arrange: Status=Pending, not ConfirmationRequired
            var cvFile = await SeedCVFile(CVFileStatus.Pending);

            var request = new CvConfirmRequest
            {
                Skills = new List<SkillDto> { new SkillDto { SkillName = "Java" } }
            };

            // Act
            var (success, error) = await _sut.ConfirmParsedDataAsync(cvFile.CVFileId, cvFile.UserId, request);

            // Assert
            Assert.False(success);
            Assert.Contains("không thể xác nhận", error);
        }

        // ===================== U13: Confirm — Replaces old skills and projects =====================
        [Fact]
        public async Task Confirm_ReplacesOldSkillsAndProjects()
        {
            // Arrange
            var cvFile = await SeedCVFile(CVFileStatus.ConfirmationRequired);
            var profile = await SeedProfile(cvFile.CVFileId);

            // Before: 2 skills (Java, Spring Boot) + 1 project (Shoe Shop)
            Assert.Equal(2, _dbContext.CVSkills.Count(s => s.ExtractedProfileId == profile.ExtractedProfileId));
            Assert.Single(_dbContext.CVProjects.Where(p => p.ExtractedProfileId == profile.ExtractedProfileId));

            var request = new CvConfirmRequest
            {
                RoleTarget = "Frontend Developer",
                Education = new List<EducationDto>(),
                Experience = new List<ExperienceDto>(),
                Projects = new List<ProjectDto>
                {
                    new ProjectDto { ProjectName = "New Project 1", TechnologyStack = "React" },
                    new ProjectDto { ProjectName = "New Project 2", TechnologyStack = "Vue" }
                },
                Skills = new List<SkillDto>
                {
                    new SkillDto { SkillName = "React", Category = "Framework" },
                    new SkillDto { SkillName = "TypeScript", Category = "Language" },
                    new SkillDto { SkillName = "CSS", Category = "Other" }
                }
            };

            // Act
            var (success, _) = await _sut.ConfirmParsedDataAsync(cvFile.CVFileId, cvFile.UserId, request);

            // Assert
            Assert.True(success);

            // After: 3 skills (React, TypeScript, CSS) + 2 projects (New Project 1, 2)
            var newSkills = _dbContext.CVSkills.Where(s => s.ExtractedProfileId == profile.ExtractedProfileId).ToList();
            Assert.Equal(3, newSkills.Count);
            Assert.Contains(newSkills, s => s.SkillName == "React");
            Assert.Contains(newSkills, s => s.SkillName == "TypeScript");
            Assert.DoesNotContain(newSkills, s => s.SkillName == "Java"); // Old skill removed

            var newProjects = _dbContext.CVProjects.Where(p => p.ExtractedProfileId == profile.ExtractedProfileId).ToList();
            Assert.Equal(2, newProjects.Count);
            Assert.DoesNotContain(newProjects, p => p.ProjectName == "Shoe Shop"); // Old project removed

            // Verify source changed to "USER"
            Assert.All(newSkills, s => Assert.Equal("USER", s.Source));
        }
    }
}
