using ai_speis_be.Models.DTOs;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.QuestionRepo;
using Microsoft.AspNetCore.Http;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace ai_speis_be.Services.QuestionService
{
    public class QuestionService : IQuestionService
    {
        private static readonly string[] RequiredImportColumns =
        {
            "questionContent",
            "major",
            "difficulty",
            "roleTarget",
            "suggestedAnswer",
            "status"
        };

        private static readonly HashSet<string> AllowedExcelContentTypes = new(
            new[]
            {
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "application/octet-stream",
                "application/zip",
                "application/x-zip-compressed"
            },
            StringComparer.OrdinalIgnoreCase);

        private static readonly XNamespace SpreadsheetNamespace =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        private static readonly XNamespace OfficeRelationshipNamespace =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        private static readonly XNamespace PackageRelationshipNamespace =
            "http://schemas.openxmlformats.org/package/2006/relationships";

        private readonly IQuestionRepoitory _repository;
        public QuestionService (IQuestionRepoitory repository)
        {
            _repository = repository;
        }

        public async Task<PagedResultDto<AdminQuestionListItemDto>> GetAdminQuestionsAsync(
            AdminQuestionQueryDto query,
            CancellationToken cancellationToken = default)
        {
            var questions = await _repository.GetAdminQuestionsAsync(
                query,
                cancellationToken);

            return new PagedResultDto<AdminQuestionListItemDto>
            {
                Items = questions.Items.Select(MapToAdminListItemDto).ToList(),
                PageNumber = questions.PageNumber,
                PageSize = questions.PageSize,
                TotalItems = questions.TotalItems
            };
        }

        public async Task<QuestionOperationResult> CreateAdminQuestionAsync(
            AdminQuestionCreateRequestDto request,
            int actingUserId,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var status = request.GetStatus();
            var isDeleted = status == AdminQuestionStatus.Inactive;

            var question = new Question
            {
                UserId = actingUserId,
                QuestionContent = request.GetQuestionContent(),
                SuggestedAnswer = request.GetSuggestedAnswer(),
                Difficulty = request.Difficulty!.Value,
                RoleTarget = request.GetRoleTarget(),
                Major = request.GetMajor(),
                IsDeleted = isDeleted,
                CreatedAt = now,
                UpdatedAt = null,
                DeletedAt = isDeleted ? now : null,
                DeletedBy = isDeleted ? actingUserId : null
            };

            var createdQuestion = await _repository.CreateQuestionAsync(
                question,
                cancellationToken);

            return new QuestionOperationResult(
                QuestionOperationOutcome.Created,
                MapToDto(createdQuestion));
        }

        public async Task<QuestionOperationResult> UpdateAdminQuestionAsync(
            int questionId,
            AdminQuestionUpdateRequestDto request,
            int actingUserId,
            CancellationToken cancellationToken = default)
        {
            var question = await _repository.GetQuestionByIdAdminAsync(
                questionId,
                cancellationToken);

            if (question is null)
            {
                return new QuestionOperationResult(
                    QuestionOperationOutcome.QuestionNotFound);
            }

            if (question.IsDeleted)
            {
                return new QuestionOperationResult(
                    QuestionOperationOutcome.QuestionDeleted);
            }

            ApplyQuestionMutation(question, request, actingUserId);

            await _repository.UpdateQuestionAsync(question, cancellationToken);

            return new QuestionOperationResult(
                QuestionOperationOutcome.Updated,
                MapToDto(question));
        }

        public async Task<QuestionOperationResult> SoftDeleteAdminQuestionAsync(
            int questionId,
            int actingUserId,
            CancellationToken cancellationToken = default)
        {
            var question = await _repository.GetQuestionByIdAdminAsync(
                questionId,
                cancellationToken);

            if (question is null)
            {
                return new QuestionOperationResult(
                    QuestionOperationOutcome.QuestionNotFound);
            }

            if (question.IsDeleted)
            {
                return new QuestionOperationResult(
                    QuestionOperationOutcome.AlreadyDeleted,
                    MapToDto(question));
            }

            var now = DateTime.UtcNow;

            question.IsDeleted = true;
            question.DeletedAt = now;
            question.DeletedBy = actingUserId;
            question.UpdatedAt = now;

            await _repository.UpdateQuestionAsync(question, cancellationToken);

            return new QuestionOperationResult(
                QuestionOperationOutcome.Deleted,
                MapToDto(question));
        }

        public async Task<QuestionImportOperationResult> ImportAdminQuestionsAsync(
            IFormFile? file,
            int actingUserId,
            CancellationToken cancellationToken = default)
        {
            var fileValidationError = ValidateImportFile(file);
            if (fileValidationError is not null)
            {
                return new QuestionImportOperationResult(
                    QuestionImportOutcome.InvalidFile,
                    ErrorMessage: fileValidationError);
            }

            List<ParsedQuestionImportRow> rows;
            try
            {
                rows = await ReadQuestionImportRowsAsync(
                    file!,
                    cancellationToken);
            }
            catch (InvalidDataException ex)
            {
                return new QuestionImportOperationResult(
                    QuestionImportOutcome.InvalidFile,
                    ErrorMessage: ex.Message);
            }
            catch (XmlException)
            {
                return new QuestionImportOperationResult(
                    QuestionImportOutcome.InvalidFile,
                    ErrorMessage: "File tải lên không phải workbook .xlsx có thể đọc được.");
            }

            var now = DateTime.UtcNow;
            var validQuestions = new List<Question>();
            var rowErrors = new List<QuestionImportRowErrorDto>();

            foreach (var row in rows)
            {
                var errors = ValidateQuestionImportRow(
                    row,
                    out var questionContent,
                    out var major,
                    out var difficulty,
                    out var roleTarget,
                    out var suggestedAnswer);

                if (errors.Count > 0)
                {
                    rowErrors.Add(new QuestionImportRowErrorDto
                    {
                        RowNumber = row.RowNumber,
                        Errors = errors
                    });

                    continue;
                }

                validQuestions.Add(new Question
                {
                    UserId = actingUserId,
                    QuestionContent = questionContent,
                    SuggestedAnswer = suggestedAnswer,
                    Difficulty = difficulty,
                    RoleTarget = roleTarget,
                    Major = major,
                    IsDeleted = false,
                    CreatedAt = now,
                    UpdatedAt = null,
                    DeletedAt = null,
                    DeletedBy = null
                });
            }

            var importedRows = await _repository.CreateQuestionsAsync(
                validQuestions,
                cancellationToken);

            return new QuestionImportOperationResult(
                QuestionImportOutcome.Imported,
                new QuestionImportSummaryDto
                {
                    TotalRows = rows.Count,
                    ImportedRows = importedRows,
                    FailedRows = rowErrors.Count,
                    Errors = rowErrors
                });
        }

        public async Task<QuestionResponseDto?> GetQuestionByIdAdminAsync(int questionId)
        {
            var question =await _repository.GetQuestionByIdAdminAsync(questionId);
            return question != null ? MapToDto(question) : null; 
        }

        public async Task<QuestionResponseDto?> GetQuestionByIdAsync(int questionId)
        {
            var question = await _repository.GetQuestionByIdAsync(questionId);
            return question != null ? MapToDto(question) : null;
        }

        public async Task<PagedResultDto<QuestionResponseDto>> GetQuestionsAsync(
            UserQuestionQueryDto query,
            CancellationToken cancellationToken = default)
        {
            var questions = await _repository.GetQuestionsAsync(
                query,
                cancellationToken);

            return new PagedResultDto<QuestionResponseDto>
            {
                Items = questions.Items.Select(MapToDto).ToList(),
                PageNumber = questions.PageNumber,
                PageSize = questions.PageSize,
                TotalItems = questions.TotalItems
            };
        }

        private static void ApplyQuestionMutation(
            Question question,
            AdminQuestionMutationRequestDto request,
            int actingUserId)
        {
            var now = DateTime.UtcNow;
            var status = request.GetStatus();

            question.QuestionContent = request.GetQuestionContent();
            question.SuggestedAnswer = request.GetSuggestedAnswer();
            question.Difficulty = request.Difficulty!.Value;
            question.RoleTarget = request.GetRoleTarget();
            question.Major = request.GetMajor();
            question.UpdatedAt = now;

            if (status == AdminQuestionStatus.Inactive)
            {
                question.IsDeleted = true;
                question.DeletedAt = now;
                question.DeletedBy = actingUserId;
            }
        }

        private static string? ValidateImportFile(IFormFile? file)
        {
            if (file is null || file.Length == 0)
            {
                return "Vui lòng tải lên file .xlsx không rỗng.";
            }

            if (!string.Equals(
                Path.GetExtension(file.FileName),
                ".xlsx",
                StringComparison.OrdinalIgnoreCase))
            {
                return "Chỉ hỗ trợ file .xlsx.";
            }

            if (!string.IsNullOrWhiteSpace(file.ContentType) &&
                !AllowedExcelContentTypes.Contains(file.ContentType))
            {
                return "Loại file không hợp lệ. Vui lòng tải lên file Excel .xlsx.";
            }

            return null;
        }

        private static async Task<List<ParsedQuestionImportRow>> ReadQuestionImportRowsAsync(
            IFormFile file,
            CancellationToken cancellationToken)
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            using var archive = new ZipArchive(
                memoryStream,
                ZipArchiveMode.Read,
                leaveOpen: false);

            var worksheetEntry = GetFirstWorksheetEntry(archive)
                ?? throw new InvalidDataException(
                    "Workbook tải lên không chứa worksheet.");

            var sharedStrings = ReadSharedStrings(archive);

            using var worksheetStream = worksheetEntry.Open();
            var worksheet = XDocument.Load(worksheetStream);

            return ReadWorksheetRows(worksheet, sharedStrings);
        }

        private static ZipArchiveEntry? GetFirstWorksheetEntry(ZipArchive archive)
        {
            var workbookEntry = archive.GetEntry("xl/workbook.xml");
            var relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");

            if (workbookEntry is not null && relationshipsEntry is not null)
            {
                using var workbookStream = workbookEntry.Open();
                var workbook = XDocument.Load(workbookStream);

                var firstSheet = workbook
                    .Descendants(SpreadsheetNamespace + "sheet")
                    .FirstOrDefault();

                var relationshipId = firstSheet?
                    .Attribute(OfficeRelationshipNamespace + "id")?
                    .Value;

                if (!string.IsNullOrWhiteSpace(relationshipId))
                {
                    using var relationshipsStream = relationshipsEntry.Open();
                    var relationships = XDocument.Load(relationshipsStream);

                    var target = relationships
                        .Descendants(PackageRelationshipNamespace + "Relationship")
                        .FirstOrDefault(r => string.Equals(
                            r.Attribute("Id")?.Value,
                            relationshipId,
                            StringComparison.Ordinal))?
                        .Attribute("Target")?
                        .Value;

                    if (!string.IsNullOrWhiteSpace(target))
                    {
                        var entryName = ResolveWorkbookRelationshipTarget(target);
                        var entry = archive.GetEntry(entryName);

                        if (entry is not null)
                        {
                            return entry;
                        }
                    }
                }
            }

            return archive.GetEntry("xl/worksheets/sheet1.xml");
        }

        private static string ResolveWorkbookRelationshipTarget(string target)
        {
            target = target.Replace('\\', '/');

            if (target.StartsWith('/'))
            {
                return target.TrimStart('/');
            }

            var parts = new Stack<string>();
            foreach (var part in $"xl/{target}".Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries))
            {
                if (part == ".")
                {
                    continue;
                }

                if (part == "..")
                {
                    if (parts.Count > 0)
                    {
                        parts.Pop();
                    }

                    continue;
                }

                parts.Push(part);
            }

            return string.Join("/", parts.Reverse());
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry is null)
            {
                return new List<string>();
            }

            using var stream = entry.Open();
            var sharedStrings = XDocument.Load(stream);

            return sharedStrings
                .Descendants(SpreadsheetNamespace + "si")
                .Select(si => string.Concat(
                    si.Descendants(SpreadsheetNamespace + "t")
                        .Select(t => t.Value)))
                .ToList();
        }

        private static List<ParsedQuestionImportRow> ReadWorksheetRows(
            XDocument worksheet,
            IReadOnlyList<string> sharedStrings)
        {
            var rowElements = worksheet
                .Descendants(SpreadsheetNamespace + "row")
                .ToList();

            if (rowElements.Count == 0)
            {
                throw new InvalidDataException(
                    "Workbook tải lên không chứa dòng dữ liệu nào.");
            }

            Dictionary<string, string>? headerColumns = null;
            var dataRows = new List<ParsedQuestionImportRow>();

            for (var index = 0; index < rowElements.Count; index++)
            {
                var worksheetRow = ReadWorksheetRow(
                    rowElements[index],
                    sharedStrings,
                    index + 1);

                if (worksheetRow.Cells.Values.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                if (headerColumns is null)
                {
                    headerColumns = BuildHeaderColumnMap(worksheetRow);
                    var missingColumns = RequiredImportColumns
                        .Where(column => !headerColumns.ContainsKey(
                            NormalizeHeader(column)))
                        .ToList();

                    if (missingColumns.Count > 0)
                    {
                        throw new InvalidDataException(
                            $"Workbook tải lên thiếu các cột bắt buộc: {string.Join(", ", missingColumns)}.");
                    }

                    continue;
                }

                var values = new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (var column in RequiredImportColumns)
                {
                    var headerKey = NormalizeHeader(column);
                    var sourceColumn = headerColumns[headerKey];

                    values[column] = worksheetRow.Cells.TryGetValue(
                        sourceColumn,
                        out var value)
                        ? value.Trim()
                        : string.Empty;
                }

                if (values.Values.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                dataRows.Add(new ParsedQuestionImportRow(
                    worksheetRow.RowNumber,
                    values));
            }

            if (headerColumns is null)
            {
                throw new InvalidDataException(
                    "Workbook tải lên không chứa dòng tiêu đề.");
            }

            return dataRows;
        }

        private static WorksheetRow ReadWorksheetRow(
            XElement rowElement,
            IReadOnlyList<string> sharedStrings,
            int fallbackRowNumber)
        {
            var rowNumber = int.TryParse(
                rowElement.Attribute("r")?.Value,
                out var parsedRowNumber)
                ? parsedRowNumber
                : fallbackRowNumber;

            var cells = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            var nextColumnIndex = 1;
            foreach (var cell in rowElement.Elements(SpreadsheetNamespace + "c"))
            {
                var reference = cell.Attribute("r")?.Value;
                var columnName = string.IsNullOrWhiteSpace(reference)
                    ? GetColumnName(nextColumnIndex)
                    : ExtractColumnName(reference);

                cells[columnName] = ReadCellValue(cell, sharedStrings).Trim();
                nextColumnIndex = GetColumnIndex(columnName) + 1;
            }

            return new WorksheetRow(rowNumber, cells);
        }

        private static Dictionary<string, string> BuildHeaderColumnMap(
            WorksheetRow headerRow)
        {
            var headerColumns = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var cell in headerRow.Cells)
            {
                var header = NormalizeHeader(cell.Value);
                if (string.IsNullOrWhiteSpace(header) ||
                    headerColumns.ContainsKey(header))
                {
                    continue;
                }

                headerColumns[header] = cell.Key;
            }

            return headerColumns;
        }

        private static string ReadCellValue(
            XElement cell,
            IReadOnlyList<string> sharedStrings)
        {
            var dataType = cell.Attribute("t")?.Value;

            if (string.Equals(dataType, "inlineStr", StringComparison.Ordinal))
            {
                return string.Concat(
                    cell.Descendants(SpreadsheetNamespace + "t")
                        .Select(t => t.Value));
            }

            var value = cell.Element(SpreadsheetNamespace + "v")?.Value;

            if (string.Equals(dataType, "s", StringComparison.Ordinal) &&
                int.TryParse(value, out var sharedStringIndex) &&
                sharedStringIndex >= 0 &&
                sharedStringIndex < sharedStrings.Count)
            {
                return sharedStrings[sharedStringIndex];
            }

            if (string.Equals(dataType, "b", StringComparison.Ordinal))
            {
                return value == "1" ? "TRUE" : "FALSE";
            }

            return value
                ?? string.Concat(
                    cell.Descendants(SpreadsheetNamespace + "t")
                        .Select(t => t.Value));
        }

        private static List<string> ValidateQuestionImportRow(
            ParsedQuestionImportRow row,
            out string questionContent,
            out string major,
            out QuestionDifficultyEnum difficulty,
            out string roleTarget,
            out string suggestedAnswer)
        {
            questionContent = GetRowValue(row, "questionContent");
            major = GetRowValue(row, "major");
            var difficultyValue = GetRowValue(row, "difficulty");
            roleTarget = GetRowValue(row, "roleTarget");
            suggestedAnswer = GetRowValue(row, "suggestedAnswer");
            var statusValue = GetRowValue(row, "status");

            difficulty = default;
            var errors = new List<string>();

            AddRequiredError(errors, "questionContent", questionContent);
            AddRequiredError(errors, "major", major);
            AddRequiredError(errors, "difficulty", difficultyValue);
            AddRequiredError(errors, "roleTarget", roleTarget);
            AddRequiredError(errors, "suggestedAnswer", suggestedAnswer);
            AddRequiredError(errors, "status", statusValue);

            AddMaxLengthError(errors, "questionContent", questionContent, 4000);
            AddMaxLengthError(errors, "major", major, 100);
            AddMaxLengthError(errors, "roleTarget", roleTarget, 100);
            AddMaxLengthError(errors, "suggestedAnswer", suggestedAnswer, 4000);
            AddMaxLengthError(errors, "status", statusValue, 20);

            if (!string.IsNullOrWhiteSpace(difficultyValue) &&
                !TryParseNamedEnum(difficultyValue, out difficulty))
            {
                errors.Add("difficulty phải là Easy, Medium hoặc Hard.");
            }

            if (!string.IsNullOrWhiteSpace(statusValue))
            {
                if (!TryParseNamedEnum<AdminQuestionStatus>(
                    statusValue,
                    out var status))
                {
                    errors.Add("status phải là Active hoặc Inactive.");
                }
                else if (status != AdminQuestionStatus.Active)
                {
                    errors.Add("status phải là Active đối với câu hỏi import.");
                }
            }

            return errors;
        }

        private static string GetRowValue(
            ParsedQuestionImportRow row,
            string column)
        {
            return row.Values.TryGetValue(column, out var value)
                ? value
                : string.Empty;
        }

        private static void AddRequiredError(
            List<string> errors,
            string field,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{field} là bắt buộc.");
            }
        }

        private static void AddMaxLengthError(
            List<string> errors,
            string field,
            string value,
            int maxLength)
        {
            if (value.Length > maxLength)
            {
                errors.Add($"{field} không được vượt quá {maxLength} ký tự.");
            }
        }

        private static bool TryParseNamedEnum<TEnum>(
            string value,
            out TEnum parsedValue)
            where TEnum : struct, Enum
        {
            var enumName = Enum.GetNames<TEnum>()
                .FirstOrDefault(name => string.Equals(
                    name,
                    value,
                    StringComparison.OrdinalIgnoreCase));

            if (enumName is null)
            {
                parsedValue = default;
                return false;
            }

            parsedValue = Enum.Parse<TEnum>(enumName);
            return true;
        }

        private static string ExtractColumnName(string cellReference)
        {
            return new string(cellReference
                .TakeWhile(char.IsLetter)
                .Select(char.ToUpperInvariant)
                .ToArray());
        }

        private static int GetColumnIndex(string columnName)
        {
            var columnIndex = 0;
            foreach (var letter in columnName)
            {
                columnIndex *= 26;
                columnIndex += char.ToUpperInvariant(letter) - 'A' + 1;
            }

            return columnIndex;
        }

        private static string GetColumnName(int columnIndex)
        {
            var columnName = string.Empty;

            while (columnIndex > 0)
            {
                columnIndex--;
                columnName = (char)('A' + columnIndex % 26) + columnName;
                columnIndex /= 26;
            }

            return columnName;
        }

        private static string NormalizeHeader(string value)
        {
            return new string(value
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
        }

        private QuestionResponseDto MapToDto(Question question)
        {
            return new QuestionResponseDto
            {
                QuestionId = question.QuestionId,
                UserId = question.UserId,
                QuestionContent = question.QuestionContent,
                SuggestedAnswer = question.SuggestedAnswer,
                Difficulty = question.Difficulty,
                RoleTarget = question.RoleTarget,
                Major = question.Major,
                IsDeleted = question.IsDeleted,
                Status = GetQuestionStatus(question),
                CreatedAt = question.CreatedAt,
                UpdatedAt = question.UpdatedAt,
                DeletedAt = question.DeletedAt,
                DeletedBy = question.DeletedBy
            };
        }

        private AdminQuestionListItemDto MapToAdminListItemDto(Question question)
        {
            return new AdminQuestionListItemDto
            {
                QuestionId = question.QuestionId,
                UserId = question.UserId,
                QuestionContent = question.QuestionContent,
                SuggestedAnswer = question.SuggestedAnswer,
                Difficulty = question.Difficulty,
                RoleTarget = question.RoleTarget,
                Major = question.Major,
                IsDeleted = question.IsDeleted,
                Status = GetQuestionStatus(question),
                CreatedAt = question.CreatedAt,
                UpdatedAt = question.UpdatedAt,
                DeletedAt = question.DeletedAt,
                DeletedBy = question.DeletedBy
            };
        }

        private static string GetQuestionStatus(Question question)
        {
            return question.IsDeleted
                ? AdminQuestionStatus.Inactive.ToString()
                : AdminQuestionStatus.Active.ToString();
        }

        private sealed record WorksheetRow(
            int RowNumber,
            IReadOnlyDictionary<string, string> Cells);

        private sealed record ParsedQuestionImportRow(
            int RowNumber,
            IReadOnlyDictionary<string, string> Values);
    }
}
