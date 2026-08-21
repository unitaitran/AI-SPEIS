using System.Net;
using System.Text.Json;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.QuestionRepo;
using ai_speis_be.Services.RagService;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.TechnicalInterviews.Selection;
using ai_speis_be.TechnicalInterviews.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace ai_speis_be.Tests.TechnicalInterviews
{
    public sealed class RagQuestionRetrievalClientTests
    {
        [Fact]
        public async Task RetrieveQuestionsAsync_ReturnsMappedQuestions_WhenPythonRagReturnsSuccess()
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            var responseContent = JsonSerializer.Serialize(new
            {
                candidate_id = "test-candidate",
                questions = new[]
                {
                    new
                    {
                        id = "FE001-001",
                        question_text = "What is Dependency Injection in ASP.NET Core?",
                        skill = "C#",
                        subskill = "DI",
                        difficulty = "Medium",
                        language = "vi",
                        expected_answer = "Dependency injection is a software design pattern...",
                        expected_key_points = new[] { "IoC container", "Service lifetime", "Constructor injection" },
                        clarification_question = "Can you name the three service lifetimes?",
                        follow_up_1 = "What is the difference between Transient and Scoped?",
                        follow_up_2 = "What happens if a Singleton resolves a Scoped service?",
                        experience_level = "Middle",
                        is_active = true
                    }
                }
            });

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
                });

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:8000")
            };

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("PythonRAG")).Returns(httpClient);

            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["PythonRAG:BaseUrl"]).Returns("http://localhost:8000");

            var loggerMock = new Mock<ILogger<RagQuestionRetrievalClient>>();

            var client = new RagQuestionRetrievalClient(factoryMock.Object, configMock.Object, loggerMock.Object);

            var result = await client.RetrieveQuestionsAsync(
                jobRole: "Backend Developer",
                experienceLevel: "Middle",
                skills: new[] { "C#" },
                language: "vi",
                count: 1,
                cancellationToken: CancellationToken.None);

            Assert.True(result.Success);
            Assert.Single(result.Questions);
            var q = result.Questions[0];
            Assert.Equal("What is Dependency Injection in ASP.NET Core?", q.QuestionContent);
            Assert.Equal("C#", q.Skill);
            Assert.Equal(QuestionDifficultyEnum.Medium, q.Difficulty);
            Assert.False(q.IsDeleted);
            Assert.NotNull(q.QdrantPayloadJson);

            using var doc = JsonDocument.Parse(q.QdrantPayloadJson);
            Assert.Equal("FE001-001", doc.RootElement.GetProperty("source_id").GetString());
            Assert.Equal("DI", doc.RootElement.GetProperty("subskill").GetString());
            Assert.Equal("Can you name the three service lifetimes?", doc.RootElement.GetProperty("clarification_question").GetString());

            // Case A: Verify Question entity subquestion properties are populated
            Assert.Equal("Can you name the three service lifetimes?", q.ClarificationQuestion);
            Assert.Equal("What is the difference between Transient and Scoped?", q.FollowUp1);
            Assert.Equal("What happens if a Singleton resolves a Scoped service?", q.FollowUp2);
            Assert.Equal("IoC container,Service lifetime,Constructor injection", q.ExpectedKeyPoints);
        }

        [Fact]
        public async Task PreparePoolAsync_WhenProviderIsOllama_CallsRagClientAndDoesNotQuerySqlRepo()
        {
            var sqlRepoMock = new Mock<IQuestionRepoitory>();
            var ragClientMock = new Mock<IRagQuestionRetrievalClient>();
            var options = new TechnicalInterviewOptions();

            var sampleQuestion = new Question
            {
                QuestionId = 202,
                QuestionContent = "Explain garbage collection in .NET",
                Skill = "C#",
                Difficulty = QuestionDifficultyEnum.Hard,
                SuggestedAnswer = "GC manages allocation and release of memory..."
            };

            ragClientMock.Setup(r => r.RetrieveQuestionsAsync(
                    "Backend Developer", "Senior", It.IsAny<IReadOnlyList<string>>(), "vi", 3, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RagRetrievalResult(true, new[] { sampleQuestion }, null, null));

            var aiResolverMock = new Mock<ITechnicalInterviewAIProviderResolver>();
            var validator = new TechnicalAIResponseValidator();
            var loggerMock = new Mock<ILogger<TechnicalQuestionSelectionService>>();

            var service = new TechnicalQuestionSelectionService(
                sqlRepoMock.Object,
                aiResolverMock.Object,
                validator,
                ragClientMock.Object,
                options,
                loggerMock.Object);

            var context = new TechnicalSelectionContext
            {
                JobRole = "Backend Developer",
                ExperienceLevel = "Senior",
                SelectedSkills = new[] { "C#" },
                Language = "vi",
                AiProvider = "ollama"
            };

            var poolResult = await service.PreparePoolAsync(context, CancellationToken.None);

            Assert.Single(poolResult.Candidates);
            Assert.Equal(202, poolResult.Candidates[0].QuestionId);
            Assert.Equal("qdrant-rag", poolResult.Relaxation);

            // Verify SQL repository was NEVER called when AiProvider = ollama
            sqlRepoMock.Verify(r => r.GetQuestionByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PreparePoolAsync_WhenProviderIsOllamaAndRagFails_ReturnsExplicitRagErrorAndNoSqlFallback()
        {
            var sqlRepoMock = new Mock<IQuestionRepoitory>();
            var ragClientMock = new Mock<IRagQuestionRetrievalClient>();
            var options = new TechnicalInterviewOptions();

            ragClientMock.Setup(r => r.RetrieveQuestionsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RagRetrievalResult(false, Array.Empty<Question>(), "RAG_SERVICE_UNAVAILABLE", "Python service connection refused"));

            var aiResolverMock = new Mock<ITechnicalInterviewAIProviderResolver>();
            var validator = new TechnicalAIResponseValidator();
            var loggerMock = new Mock<ILogger<TechnicalQuestionSelectionService>>();

            var service = new TechnicalQuestionSelectionService(
                sqlRepoMock.Object,
                aiResolverMock.Object,
                validator,
                ragClientMock.Object,
                options,
                loggerMock.Object);

            var context = new TechnicalSelectionContext
            {
                JobRole = "Backend Developer",
                ExperienceLevel = "Senior",
                SelectedSkills = new[] { "C#" },
                Language = "vi",
                AiProvider = "ollama"
            };

            var poolResult = await service.PreparePoolAsync(context, CancellationToken.None);

            Assert.Empty(poolResult.Candidates);
            Assert.Equal("RAG_SERVICE_UNAVAILABLE", poolResult.ErrorCode);

            // MUST NOT fall back to SQL
            sqlRepoMock.Verify(r => r.GetQuestionByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PreparePoolAsync_WhenProviderIsGemini_UsesSqlQuestionBankAndDoesNotCallRagClient()
        {
            var sqlRepoMock = new Mock<IQuestionRepoitory>();
            var ragClientMock = new Mock<IRagQuestionRetrievalClient>();
            var options = new TechnicalInterviewOptions();

            var sampleQuestion = new Question
            {
                QuestionId = 303,
                QuestionContent = "What is LINQ in C#?",
                Skill = "C#",
                Difficulty = QuestionDifficultyEnum.Medium,
                RoleTarget = "Software Engineer",
                ExperienceLevel = "Junior"
            };

            sqlRepoMock.Setup(r => r.GetTechnicalCandidatesAsync(
                    It.IsAny<TechnicalQuestionCandidateQuery>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { sampleQuestion });

            var aiResolverMock = new Mock<ITechnicalInterviewAIProviderResolver>();
            var validator = new TechnicalAIResponseValidator();
            var loggerMock = new Mock<ILogger<TechnicalQuestionSelectionService>>();

            var service = new TechnicalQuestionSelectionService(
                sqlRepoMock.Object,
                aiResolverMock.Object,
                validator,
                ragClientMock.Object,
                options,
                loggerMock.Object);

            var context = new TechnicalSelectionContext
            {
                JobRole = "Backend Developer",
                ExperienceLevel = "Junior",
                SelectedSkills = new[] { "C#" },
                Language = "vi",
                AiProvider = "gemini"
            };

            var poolResult = await service.PreparePoolAsync(context, CancellationToken.None);

            Assert.Single(poolResult.Candidates);
            Assert.Equal(303, poolResult.Candidates[0].QuestionId);

            // RAG Client MUST NOT be called for Gemini path
            ragClientMock.Verify(r => r.RetrieveQuestionsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
