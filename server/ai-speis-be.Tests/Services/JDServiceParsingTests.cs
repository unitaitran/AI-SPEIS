using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ai_speis_be.DTOs.JdParsing;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.JDRepo;
using ai_speis_be.Services.BackgroundWorker;
using ai_speis_be.Services.FileValidatorService;
using ai_speis_be.Services.JDService;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ai_speis_be.Tests.Services
{
    public class JDServiceParsingTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IJDRepository> _mockJdRepo;
        private readonly Mock<IFileValidatorService> _mockFileValidator;
        private readonly Mock<IJdParseQueue> _mockJdQueue;
        private readonly JDService _jdService;

        public JDServiceParsingTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _mockJdRepo = new Mock<IJDRepository>();
            _mockFileValidator = new Mock<IFileValidatorService>();
            _mockJdQueue = new Mock<IJdParseQueue>();

            _jdService = new JDService(
                _mockJdRepo.Object,
                _mockFileValidator.Object,
                _context,
                _mockJdQueue.Object
            );
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Fact]
        public async Task TriggerParseAsync_ValidJd_ShouldUpdateStatusToProcessingAndEnqueue()
        {
            // Arrange
            int userId = 1;
            int jdId = 100;
            var jdFile = new JDFile
            {
                JDFileId = jdId,
                UserId = userId,
                Status = JDFileStatus.Pending
            };
            _context.JDFiles.Add(jdFile);
            await _context.SaveChangesAsync();

            // Act
            var result = await _jdService.TriggerParseAsync(userId, jdId);

            // Assert
            Assert.True(result);
            var updatedJd = await _context.JDFiles.FindAsync(new object[] { jdId });
            Assert.NotNull(updatedJd);
            Assert.Equal(JDFileStatus.Processing, updatedJd.Status);
            _mockJdQueue.Verify(q => q.QueueJdForParsingAsync(jdId), Times.Once);
        }

        [Fact]
        public async Task TriggerParseAsync_AlreadyProcessing_ShouldReturnFalse()
        {
            // Arrange
            int userId = 1;
            int jdId = 101;
            var jdFile = new JDFile
            {
                JDFileId = jdId,
                UserId = userId,
                Status = JDFileStatus.Processing
            };
            _context.JDFiles.Add(jdFile);
            await _context.SaveChangesAsync();

            // Act
            var result = await _jdService.TriggerParseAsync(userId, jdId);

            // Assert
            Assert.False(result);
            _mockJdQueue.Verify(q => q.QueueJdForParsingAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetParsedDataAsync_ValidId_ShouldReturnParsedData()
        {
            // Arrange
            int userId = 1;
            int jdId = 102;
            _context.JDFiles.Add(new JDFile { JDFileId = jdId, UserId = userId, Status = JDFileStatus.ConfirmationRequired });
            
            _context.JDExtractedProfiles.Add(new JDExtractedProfile
            {
                ExtractedProfileId = 1,
                JDFileId = jdId,
                JobTitle = "Backend Dev",
                ExperienceLevel = "Senior",
                RequiredSkills = JsonSerializer.Serialize(new List<string> { "C#", ".NET" }),
                NiceToHaveSkills = JsonSerializer.Serialize(new List<string> { "Docker" }),
                CompanyCharacteristics = "Agile Startup",
                ConfidenceScore = 0.9m
            });
            await _context.SaveChangesAsync();

            // Act
            var result = await _jdService.GetParsedDataAsync(userId, jdId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Backend Dev", result.JobTitle);
            Assert.Equal("Senior", result.ExperienceLevel);
            Assert.Equal("Agile Startup", result.CompanyCharacteristics);
            Assert.Contains("C#", result.RequiredSkills);
            Assert.Contains("Docker", result.NiceToHaveSkills);
            Assert.Null(result.WarningMessage);
        }

        [Fact]
        public async Task ConfirmParsedDataAsync_ValidData_ShouldUpdateProfileAndStatus()
        {
            // Arrange
            int userId = 1;
            int jdId = 103;
            var jdFile = new JDFile { JDFileId = jdId, UserId = userId, Status = JDFileStatus.ConfirmationRequired };
            _context.JDFiles.Add(jdFile);
            
            var profile = new JDExtractedProfile
            {
                ExtractedProfileId = 2,
                JDFileId = jdId,
                IsConfirmed = false
            };
            _context.JDExtractedProfiles.Add(profile);
            await _context.SaveChangesAsync();

            var request = new JdConfirmRequest
            {
                JobTitle = "Updated Title",
                ExperienceLevel = "Junior",
                RequiredSkills = new List<string> { "Java" },
                NiceToHaveSkills = new List<string>(),
                Responsibilities = "Coding",
                CompanyCharacteristics = "Big Corp"
            };

            // Act
            var result = await _jdService.ConfirmParsedDataAsync(userId, jdId, request);

            // Assert
            Assert.True(result);
            Assert.Equal(JDFileStatus.Confirmed, jdFile.Status);
            Assert.True(profile.IsConfirmed);
            Assert.Equal(userId, profile.ConfirmedBy);
            Assert.Equal("Updated Title", profile.JobTitle);
            Assert.Equal("Big Corp", profile.CompanyCharacteristics);
            Assert.Contains("Java", JsonSerializer.Deserialize<List<string>>(profile.RequiredSkills)!);
        }
    }
}
