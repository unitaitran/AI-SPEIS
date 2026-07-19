using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.QuestionRepo;
using ai_speis_be.Services.QuestionService;
using Microsoft.AspNetCore.Http;
using Moq;
using System.IO.Compression;
using System.Xml.Linq;

namespace ai_speis_be.Tests.Services;

public class QuestionImportTests
{
    private static readonly string[] Headers =
    {
        "questionContent", "major", "difficulty", "roleTarget",
        "suggestedAnswer", "status", "questionType", "language", "skill",
        "experienceLevel", "levelTags", "companyCategory", "companySubcategory",
        "expectedKeyPoints", "scoringRubricJson", "clarificationQuestion",
        "followUp1", "followUp2", "timeLimitSeconds", "keywordTags",
        "embeddingText", "qdrantPayloadJson"
    };

    [Fact]
    public async Task ImportAdminQuestionsAsync_MapsNewQuestionFields()
    {
        var row = new[]
        {
            "Hãy kể về một lần bạn giải quyết xung đột.", "Software Engineering",
            "Medium", "Backend Developer", "Ứng viên nên dùng mô hình STAR.",
            "Active", "Behavioral", "vi", "Communication", "Fresher/Junior",
            "Fresher,Junior", "Product Company", "SaaS", "bối cảnh,hành động,kết quả",
            "{\"0\":\"Không trả lời\",\"5\":\"Rõ ràng\"}", "Bạn có thể nói rõ hơn không?",
            "Bạn đã làm gì?", "Bạn học được gì?", "180", "conflict,communication",
            "Behavioral Communication conflict", "{\"language\":\"vi\"}"
        };

        IReadOnlyCollection<Question>? savedQuestions = null;
        var repository = new Mock<IQuestionRepoitory>();
        repository
            .Setup(item => item.CreateQuestionsAsync(
                It.IsAny<IReadOnlyCollection<Question>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<Question>, CancellationToken>(
                (questions, _) => savedQuestions = questions)
            .ReturnsAsync(1);

        var service = new QuestionService(repository.Object);
        using var workbook = CreateWorkbook(Headers, row);
        var file = CreateFormFile(workbook);

        var result = await service.ImportAdminQuestionsAsync(file, 42);

        Assert.Equal(QuestionImportOutcome.Imported, result.Outcome);
        Assert.NotNull(result.Summary);
        Assert.Equal(1, result.Summary.ImportedRows);
        var question = Assert.Single(savedQuestions!);
        Assert.Equal(42, question.UserId);
        Assert.Equal(QuestionDifficultyEnum.Medium, question.Difficulty);
        Assert.Equal("Behavioral", question.QuestionType);
        Assert.Equal("vi", question.Language);
        Assert.Equal("Communication", question.Skill);
        Assert.Equal("Fresher/Junior", question.ExperienceLevel);
        Assert.Equal("Fresher,Junior", question.LevelTags);
        Assert.Equal("Product Company", question.CompanyCategory);
        Assert.Equal("SaaS", question.CompanySubcategory);
        Assert.Equal("bối cảnh,hành động,kết quả", question.ExpectedKeyPoints);
        Assert.Equal(row[14], question.ScoringRubricJson);
        Assert.Equal(row[15], question.ClarificationQuestion);
        Assert.Equal(row[16], question.FollowUp1);
        Assert.Equal(row[17], question.FollowUp2);
        Assert.Equal(180, question.TimeLimitSeconds);
        Assert.Equal(row[19], question.KeywordTags);
        Assert.Equal(row[20], question.EmbeddingText);
        Assert.Equal(row[21], question.QdrantPayloadJson);
    }

    [Fact]
    public async Task ImportAdminQuestionsAsync_InvalidNewFields_ReturnsRowErrors()
    {
        var row = new[]
        {
            "Question", "IT", "Easy", "Developer", "Answer", "Active",
            "Unknown", "fr", "", "", "", "", "", "", "not-json", "", "",
            "", "zero", "", "", "{invalid}"
        };

        var repository = new Mock<IQuestionRepoitory>();
        repository
            .Setup(item => item.CreateQuestionsAsync(
                It.IsAny<IReadOnlyCollection<Question>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var service = new QuestionService(repository.Object);
        using var workbook = CreateWorkbook(Headers, row);
        var file = CreateFormFile(workbook);

        var result = await service.ImportAdminQuestionsAsync(file, 42);

        Assert.Equal(0, result.Summary!.ImportedRows);
        var error = Assert.Single(result.Summary.Errors);
        Assert.Contains(error.Errors, item => item.StartsWith("questionType"));
        Assert.Contains(error.Errors, item => item.StartsWith("language"));
        Assert.Contains(error.Errors, item => item.StartsWith("timeLimitSeconds"));
        Assert.Contains(error.Errors, item => item.StartsWith("scoringRubricJson"));
        Assert.Contains(error.Errors, item => item.StartsWith("qdrantPayloadJson"));
    }

    [Fact]
    public async Task ImportAdminQuestionsAsync_MissingColumns_UsesEmptyStringsAndDefaults()
    {
        IReadOnlyCollection<Question>? savedQuestions = null;
        var repository = new Mock<IQuestionRepoitory>();
        repository
            .Setup(item => item.CreateQuestionsAsync(
                It.IsAny<IReadOnlyCollection<Question>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<Question>, CancellationToken>(
                (questions, _) => savedQuestions = questions)
            .ReturnsAsync(1);

        var service = new QuestionService(repository.Object);
        using var workbook = CreateWorkbook(new[] { "skill" }, new[] { "Communication" });
        var file = CreateFormFile(workbook);

        var result = await service.ImportAdminQuestionsAsync(file, 42);

        Assert.Equal(QuestionImportOutcome.Imported, result.Outcome);
        Assert.Equal(1, result.Summary!.ImportedRows);
        var question = Assert.Single(savedQuestions!);
        Assert.Equal(string.Empty, question.QuestionContent);
        Assert.Equal(string.Empty, question.Major);
        Assert.Equal(string.Empty, question.RoleTarget);
        Assert.Equal(string.Empty, question.SuggestedAnswer);
        Assert.Equal(string.Empty, question.QuestionType);
        Assert.Equal(string.Empty, question.ScoringRubricJson);
        Assert.Equal(QuestionDifficultyEnum.Easy, question.Difficulty);
        Assert.Equal(120, question.TimeLimitSeconds);
        Assert.False(question.IsDeleted);
    }

    [Fact]
    public async Task ImportAdminQuestionsAsync_CvDeepDiveQuestionType_IsAccepted()
    {
        IReadOnlyCollection<Question>? savedQuestions = null;
        var repository = new Mock<IQuestionRepoitory>();
        repository
            .Setup(item => item.CreateQuestionsAsync(
                It.IsAny<IReadOnlyCollection<Question>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<Question>, CancellationToken>(
                (questions, _) => savedQuestions = questions)
            .ReturnsAsync(1);

        var service = new QuestionService(repository.Object);
        using var workbook = CreateWorkbook(
            new[] { "questionType" },
            new[] { "CV Deep Dive" });
        var file = CreateFormFile(workbook);

        var result = await service.ImportAdminQuestionsAsync(file, 42);

        Assert.Equal(1, result.Summary!.ImportedRows);
        Assert.Empty(result.Summary.Errors);
        Assert.Equal("CV Deep Dive", Assert.Single(savedQuestions!).QuestionType);
    }

    private static MemoryStream CreateWorkbook(
        IReadOnlyList<string> headers,
        IReadOnlyList<string> rowValues)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("xl/worksheets/sheet1.xml");
            using var entryStream = entry.Open();
            XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var worksheet = new XDocument(
                new XElement(spreadsheet + "worksheet",
                    new XElement(spreadsheet + "sheetData",
                        CreateRow(spreadsheet, 1, headers),
                        CreateRow(spreadsheet, 2, rowValues))));
            worksheet.Save(entryStream);
        }

        stream.Position = 0;
        return stream;
    }

    private static FormFile CreateFormFile(Stream workbook)
    {
        return new FormFile(workbook, 0, workbook.Length, "file", "questions.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
    }

    private static XElement CreateRow(
        XNamespace spreadsheet,
        int rowNumber,
        IReadOnlyList<string> values)
    {
        return new XElement(
            spreadsheet + "row",
            new XAttribute("r", rowNumber),
            values.Select((value, index) =>
                new XElement(
                    spreadsheet + "c",
                    new XAttribute("r", $"{GetColumnName(index + 1)}{rowNumber}"),
                    new XAttribute("t", "inlineStr"),
                    new XElement(
                        spreadsheet + "is",
                        new XElement(spreadsheet + "t", value)))));
    }

    private static string GetColumnName(int columnIndex)
    {
        var name = string.Empty;
        while (columnIndex > 0)
        {
            columnIndex--;
            name = (char)('A' + columnIndex % 26) + name;
            columnIndex /= 26;
        }

        return name;
    }
}
