using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.CodingRepo;
using ai_speis_be.Services.Judge0Service;
using Microsoft.EntityFrameworkCore;
using ai_speis_be.Services.CodingService.Helpers;
using System.Text.Json;
using ai_speis_be.Services.CodingService.Selection;

namespace ai_speis_be.Services.CodingService
{
    public class CodingService : ICodingService
    {
        private readonly ICodingRepository _repository;
        private readonly IJudge0Service _judge0Service;
        private readonly ICodingQuestionSelectionService _selectionService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CodingService> _logger;

        public CodingService(
            ICodingRepository repository,
            IJudge0Service judge0Service,
            ICodingQuestionSelectionService selectionService,
            ApplicationDbContext context,
            ILogger<CodingService> logger)
        {
            _repository = repository;
            _judge0Service = judge0Service;
            _selectionService = selectionService;
            _context = context;
            _logger = logger;
        }

        public async Task<(bool Success, string? ErrorMessage, SubmissionResponseDto? Data)> SubmitCodeAsync(
            int userId,
            SubmitCodeRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (request.InterviewSessionId > 0)
            {
                var session = await _context.InterviewSessions
                    .Include(s => s.InterviewCampaign)
                    .FirstOrDefaultAsync(
                        s => s.InterviewSessionId == request.InterviewSessionId,
                        cancellationToken);

                if (session == null)
                    return (false, "Không tìm thấy phiên phỏng vấn.", null);

                if (session.InterviewCampaign.UserId != userId)
                    return (false, "Bạn không có quyền truy cập phiên phỏng vấn này.", null);

                if (session.Status == InterviewSessionStatus.Pending)
                {
                    session.Status = InterviewSessionStatus.Active;
                    if (session.InterviewCampaign.Status == InterviewCampaignStatus.Pending)
                    {
                        session.InterviewCampaign.Status = InterviewCampaignStatus.Active;
                        session.InterviewCampaign.StartedAt = DateTime.UtcNow;
                        session.InterviewCampaign.ExpiresAt = DateTime.UtcNow.AddMinutes(session.InterviewCampaign.DurationMinutes);
                    }
                    await _context.SaveChangesAsync(cancellationToken);
                }
                else if (session.Status != InterviewSessionStatus.Active)
                {
                    return (false, "Phiên phỏng vấn chưa bắt đầu hoặc đã kết thúc.", null);
                }
            }

            bool isTestRun = request.IsTestRun || request.InterviewSessionId <= 0;

            // 2. Lấy câu hỏi + test cases
            var question = await _repository.GetCodingQuestionWithTestCasesAsync(
                request.CodingQuestionId, cancellationToken);

            if (question == null)
                return (false, "Không tìm thấy câu hỏi coding.", null);

            var testCases = isTestRun
                ? question.TestCases.Where(tc => tc.IsSample).ToList()
                : question.TestCases.ToList();

            if (testCases.Count == 0 && isTestRun)
            {
                // Fallback to all test cases if no sample test cases defined
                testCases = question.TestCases.ToList();
            }

            if (testCases.Count == 0)
                return (false, "Câu hỏi chưa có test case nào.", null);

            string wrappedSourceCode = WrapCodeWithHarness(request.SourceCode, request.LanguageId, question);

            int targetMemory = request.LanguageId == 62 ? Math.Max(question.MemoryLimit, 256000) : question.MemoryLimit;
            int safeMemoryLimit = Math.Clamp(targetMemory <= 0 ? 256000 : targetMemory, 1000, 512000);

            // 3. Tạo batch submissions gửi đến Judge0
            var judge0Requests = testCases.Select(tc => new Judge0SubmissionRequest
            {
                source_code = wrappedSourceCode,
                language_id = request.LanguageId,
                stdin = tc.Input ?? "",
                cpu_time_limit = question.TimeLimit,
                memory_limit = safeMemoryLimit,
                command_line_arguments = request.LanguageId == 62 ? "-Xms64m -Xmx128m" : null
            }).ToList();

            List<Judge0SubmissionResponse> judge0Results;
            try
            {
                judge0Results = await _judge0Service.SubmitBatchAsync(
                    judge0Requests, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi Judge0 API");
                return (false, "Hệ thống chấm code đang gặp sự cố. Vui lòng thử lại sau.", null);
            }

            if (judge0Results.Count != testCases.Count)
            {
                _logger.LogError(
                    "Judge0 trả về {ResultCount} kết quả, nhưng có {TestCount} test cases",
                    judge0Results.Count, testCases.Count);
                return (false, "Kết quả từ Judge0 không khớp số lượng test cases.", null);
            }

            // 4. So sánh kết quả và tạo records
            var submission = new CodingSubmission
            {
                InterviewSessionId = request.InterviewSessionId,
                CodingQuestionId = request.CodingQuestionId,
                SourceCode = request.SourceCode,
                LanguageId = request.LanguageId,
                Status = "Processing",
                TotalTestCases = testCases.Count,
                CreatedAt = DateTime.UtcNow
            };

            int passedCount = 0;
            double maxTimeMs = 0;
            int maxMemoryKb = 0;
            string overallStatus = "Accepted";

            var testCaseResults = new List<SubmissionTestCaseResult>();

            for (int i = 0; i < testCases.Count; i++)
            {
                var tc = testCases[i];
                var result = judge0Results[i];

                var statusDescription = result.status?.description ?? "Unknown";
                var actualOutput = result.stdout?.TrimEnd() ?? "";
                var expectedOutput = tc.ExpectedOutput.TrimEnd();

                // Parse time (Judge0 trả về dạng string "0.001")
                double timeMs = 0;
                if (!string.IsNullOrEmpty(result.time) &&
                    double.TryParse(result.time, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedTime))
                {
                    timeMs = parsedTime * 1000; // Convert seconds to ms
                }

                int memoryKb = result.memory ?? 0;

                // Xác định status cho test case này
                string tcStatus;
                if (result.status?.id == 3) // Accepted từ Judge0
                {
                    // Judge0 nói Accepted, kiểm tra output thông minh (JSON / String)
                    tcStatus = CompareOutputs(actualOutput, expectedOutput)
                        ? "Accepted"
                        : "Wrong Answer";
                }
                else
                {
                    tcStatus = statusDescription;
                }

                if (tcStatus == "Accepted")
                {
                    passedCount++;
                }
                else if (overallStatus == "Accepted")
                {
                    // Lấy status lỗi đầu tiên làm overall status
                    overallStatus = tcStatus;
                }

                // Track max time/memory
                maxTimeMs = Math.Max(maxTimeMs, timeMs);
                maxMemoryKb = Math.Max(maxMemoryKb, memoryKb);

                testCaseResults.Add(new SubmissionTestCaseResult
                {
                    TestCaseId = tc.TestCaseId,
                    ActualOutput = result.stdout,
                    Stderr = result.stderr,
                    CompileOutput = result.compile_output,
                    TimeMs = timeMs,
                    MemoryKb = memoryKb,
                    Status = tcStatus
                });
            }

            // Nếu tất cả pass thì overall là Accepted
            if (passedCount == testCases.Count)
            {
                overallStatus = "Accepted";
            }

            submission.PassedTestCases = passedCount;
            submission.MaxTimeMs = maxTimeMs;
            submission.MaxMemoryKb = maxMemoryKb;
            submission.Status = overallStatus;
            submission.SubmissionTestCaseResults = testCaseResults;

            // 5. Lưu vào database (bỏ qua nếu là test mode hoặc isTestRun)
            if (request.InterviewSessionId > 0 && !isTestRun)
            {
                await _repository.CreateSubmissionAsync(submission, cancellationToken);
                _logger.LogInformation(
                    "Submission {SubmissionId} cho câu hỏi {QuestionId}: {Passed}/{Total} passed — {Status}",
                    submission.CodingSubmissionId, request.CodingQuestionId,
                    passedCount, testCases.Count, overallStatus);
            }
            else
            {
                // Dummy ID cho test mode / Run Code
                submission.CodingSubmissionId = 9999;
                _logger.LogInformation(
                    "Test Mode / Run Code cho câu hỏi {QuestionId}: {Passed}/{Total} passed — {Status}",
                    request.CodingQuestionId, passedCount, testCases.Count, overallStatus);
            }

            // 6. Map sang response DTO
            var responseDto = MapToSubmissionResponseDto(submission, testCases);

            return (true, null, responseDto);
        }

        public async Task<(bool Success, string? ErrorMessage, List<CodingQuestionResponseDto>? Data)> GetCodingQuestionsAsync(
            int userId,
            int sessionId,
            CancellationToken cancellationToken = default)
        {
            List<CodingQuestion> questions = new List<CodingQuestion>();

            if (sessionId > 0)
            {
                // Validate session thuộc về user
                var session = await _context.InterviewSessions
                    .Include(s => s.InterviewCampaign)
                        .ThenInclude(c => c.CVExtractedProfile)
                            .ThenInclude(p => p.Skills)
                    .Include(s => s.InterviewCampaign)
                        .ThenInclude(c => c.JDExtractedProfile)
                    .FirstOrDefaultAsync(
                        s => s.InterviewSessionId == sessionId,
                        cancellationToken);

                if (session == null)
                    return (false, "Không tìm thấy phiên phỏng vấn.", null);

                if (session.InterviewCampaign.UserId != userId)
                    return (false, "Bạn không có quyền truy cập phiên phỏng vấn này.", null);

                questions = await _selectionService.SelectCodingQuestionsAsync(session, cancellationToken);
            }
            else
            {
                // TEST MODE: SessionId = 0 -> Bypass check, load active questions
                questions = await _context.CodingQuestions
                    .Where(q => q.IsActive && !q.IsDeleted)
                    .Include(q => q.CodingQuestionTemplates)
                    .Include(q => q.TestCases)
                    .Take(3)
                    .ToListAsync(cancellationToken);
            }

            var dtos = questions.Select(q => new CodingQuestionResponseDto
            {
                CodingQuestionId = q.CodingQuestionId,
                Title = q.Title,
                Description = q.Description,
                TimeLimit = q.TimeLimit,
                MemoryLimit = q.MemoryLimit,
                JobRole = q.JobRole,
                Skill = q.Skill,
                Subskill = q.Subskill,
                Difficulty = q.Difficulty,
                InputDescription = q.InputDescription,
                OutputDescription = q.OutputDescription,
                Constraints = q.Constraints,
                Examples = q.Examples,
                FunctionName = q.FunctionName,
                FunctionParameters = q.FunctionParameters,
                ReturnType = q.ReturnType,
                FunctionSignature = q.FunctionSignature,
                SupportedProgrammingLanguages = q.SupportedProgrammingLanguages,
                ExpectedTimeComplexity = q.ExpectedTimeComplexity,
                ExpectedSpaceComplexity = q.ExpectedSpaceComplexity,
                Templates = q.CodingQuestionTemplates.Select(t => new CodingQuestionTemplateDto
                {
                    TemplateId = t.TemplateId,
                    LanguageId = t.LanguageId,
                    TemplateCode = t.TemplateCode
                }).ToList(),
                SampleTestCases = q.TestCases
                    .Where(tc => tc.IsSample)
                    .Select(tc => new SampleTestCaseDto
                    {
                        TestCaseId = tc.TestCaseId,
                        Input = tc.Input,
                        ExpectedOutput = tc.ExpectedOutput
                    }).ToList()
            }).ToList();

            return (true, null, dtos);
        }

        public async Task<(bool Success, string? ErrorMessage, SubmissionResponseDto? Data)> GetSubmissionAsync(
            int userId,
            int submissionId,
            CancellationToken cancellationToken = default)
        {
            var submission = await _repository.GetSubmissionByIdAsync(
                submissionId, cancellationToken);

            if (submission == null)
                return (false, "Không tìm thấy submission.", null);

            // Validate user owns the session
            var session = await _context.InterviewSessions
                .Include(s => s.InterviewCampaign)
                .FirstOrDefaultAsync(
                    s => s.InterviewSessionId == submission.InterviewSessionId,
                    cancellationToken);

            if (session == null || session.InterviewCampaign.UserId != userId)
                return (false, "Bạn không có quyền truy cập submission này.", null);

            // Lấy test cases để biết IsSample/IsHidden
            var testCases = await _context.TestCases
                .Where(tc => tc.CodingQuestionId == submission.CodingQuestionId)
                .ToListAsync(cancellationToken);

            var dto = MapToSubmissionResponseDto(submission, testCases);
            return (true, null, dto);
        }

        public async Task<(bool Success, string? ErrorMessage, List<SubmissionSummaryDto>? Data)> GetSubmissionHistoryAsync(
            int userId,
            int sessionId,
            int questionId,
            CancellationToken cancellationToken = default)
        {
            // Validate session thuộc về user
            var session = await _context.InterviewSessions
                .Include(s => s.InterviewCampaign)
                .FirstOrDefaultAsync(
                    s => s.InterviewSessionId == sessionId,
                    cancellationToken);

            if (session == null)
                return (false, "Không tìm thấy phiên phỏng vấn.", null);

            if (session.InterviewCampaign.UserId != userId)
                return (false, "Bạn không có quyền truy cập phiên phỏng vấn này.", null);

            var submissions = await _repository.GetSubmissionsBySessionAndQuestionAsync(
                sessionId, questionId, cancellationToken);

            var dtos = submissions.Select(s => new SubmissionSummaryDto
            {
                CodingSubmissionId = s.CodingSubmissionId,
                CodingQuestionId = s.CodingQuestionId,
                LanguageId = s.LanguageId,
                Status = s.Status,
                TotalTestCases = s.TotalTestCases,
                PassedTestCases = s.PassedTestCases,
                MaxTimeMs = s.MaxTimeMs,
                MaxMemoryKb = s.MaxMemoryKb,
                CreatedAt = s.CreatedAt
            }).ToList();

            return (true, null, dtos);
        }

        // =============================================
        // Private helpers
        // =============================================

        /// <summary>
        /// Map CodingSubmission entity sang SubmissionResponseDto.
        /// Hidden test cases sẽ không trả ActualOutput và ExpectedOutput.
        /// </summary>
        private static SubmissionResponseDto MapToSubmissionResponseDto(
            CodingSubmission submission,
            List<TestCase> testCases)
        {
            var testCaseLookup = testCases.ToDictionary(tc => tc.TestCaseId);

            string? firstCompileOutput = submission.SubmissionTestCaseResults
                .FirstOrDefault(r => !string.IsNullOrEmpty(r.CompileOutput))?.CompileOutput;
            string? firstStderr = submission.SubmissionTestCaseResults
                .FirstOrDefault(r => !string.IsNullOrEmpty(r.Stderr))?.Stderr;

            return new SubmissionResponseDto
            {
                CodingSubmissionId = submission.CodingSubmissionId,
                CodingQuestionId = submission.CodingQuestionId,
                Status = submission.Status,
                TotalTestCases = submission.TotalTestCases,
                PassedTestCases = submission.PassedTestCases,
                MaxTimeMs = submission.MaxTimeMs,
                MaxMemoryKb = submission.MaxMemoryKb,
                CompileOutput = firstCompileOutput,
                Stderr = firstStderr,
                CreatedAt = submission.CreatedAt,
                TestCaseResults = submission.SubmissionTestCaseResults.Select(r =>
                {
                    var isSample = testCaseLookup.TryGetValue(r.TestCaseId, out var tc)
                        && tc.IsSample;

                    return new TestCaseResultDto
                    {
                        TestCaseId = r.TestCaseId,
                        IsSample = isSample,
                        Status = r.Status,
                        Input = isSample && tc != null ? tc.Input : null,
                        ActualOutput = isSample ? r.ActualOutput : null,
                        ExpectedOutput = isSample && tc != null ? tc.ExpectedOutput : null,
                        Stderr = r.Stderr,
                        CompileOutput = r.CompileOutput,
                        TimeMs = r.TimeMs,
                        MemoryKb = r.MemoryKb
                    };
                }).ToList()
            };
        }

        /// <summary>
        /// Tự động bọc (wrap) code của thí sinh bằng Test Harness Driver tương ứng với ngôn ngữ.
        /// Đảm bảo hàm tự động đọc JSON stdin, gọi fnName(...) và in kết quả stdout.
        /// </summary>
        private static string WrapCodeWithHarness(string sourceCode, int languageId, CodingQuestion question)
        {
            if (string.IsNullOrWhiteSpace(sourceCode) || question == null)
                return sourceCode;

            string fnName = question.FunctionName ?? "solution";

            // Python (71)
            if (languageId == 71)
            {
                if (sourceCode.Contains("__main__") || sourceCode.Contains("sys.stdin.read"))
                    return sourceCode;

                var harness = $@"

# --- AUTOMATIC TEST HARNESS ---
if __name__ == '__main__':
    import sys, json
    __raw_input = sys.stdin.read().strip()
    if __raw_input:
        try:
            __data = json.loads(__raw_input)
            if isinstance(__data, dict):
                __res = {fnName}(**__data)
            elif isinstance(__data, list):
                __res = {fnName}(*__data)
            else:
                __res = {fnName}(__raw_input)
            
            if isinstance(__res, (dict, list)):
                print(json.dumps(__res, separators=(',', ':')))
            else:
                print(__res)
        except Exception as __e:
            import traceback
            sys.stderr.write(str(__e) + '\n' + traceback.format_exc())
";
                return sourceCode + harness;
            }

            // JavaScript / Node.js (63)
            if (languageId == 63)
            {
                if (sourceCode.Contains("process.stdin") || sourceCode.Contains("readFileSync"))
                    return sourceCode;

                var harness = $@"

// --- AUTOMATIC TEST HARNESS ---
if (typeof process !== 'undefined') {{
  try {{
    const fs = require('fs');
    const inputStr = fs.readFileSync(0, 'utf-8').trim();
    if (inputStr) {{
      const data = JSON.parse(inputStr);
      let res;
      if (typeof data === 'object' && !Array.isArray(data) && data !== null) {{
        res = {fnName}(...Object.values(data));
      }} else if (Array.isArray(data)) {{
        res = {fnName}(...data);
      }} else {{
        res = {fnName}(data);
      }}
      console.log(typeof res === 'object' ? JSON.stringify(res) : res);
    }}
  }} catch (err) {{
    console.error(err);
  }}
}}
";
                return sourceCode + harness;
            }

            return sourceCode;
        }

        /// <summary>
        /// So sánh kết quả thực tế (actual) và kết quả kỳ vọng (expected) một cách thông minh:
        /// Hỗ trợ cả string thô, JSON DeepEquals (bỏ qua khoảng trắng), và loại bỏ xuống dòng thừa.
        /// </summary>
        private static bool CompareOutputs(string actual, string expected)
        {
            if (actual == null && expected == null) return true;
            if (actual == null || expected == null) return false;

            string normActual = actual.Trim();
            string normExpected = expected.Trim();

            if (string.Equals(normActual, normExpected, StringComparison.OrdinalIgnoreCase))
                return true;

            // Thử so sánh JSON DeepEquals
            try
            {
                using var docActual = JsonDocument.Parse(normActual);
                using var docExpected = JsonDocument.Parse(normExpected);
                return JsonElement.DeepEquals(docActual.RootElement, docExpected.RootElement);
            }
            catch
            {
                // Không phải JSON, so sánh chuỗi đã chuẩn hóa khoảng trắng
                var cleanActual = string.Join(" ", normActual.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                var cleanExpected = string.Join(" ", normExpected.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                return string.Equals(cleanActual, cleanExpected, StringComparison.OrdinalIgnoreCase);
            }
        }

        public async Task<(bool Success, string? ErrorMessage, int ImportedCount)> ImportAdminCodingQuestionsAsync(
            IFormFile file,
            CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0)
                return (false, "File tải lên không hợp lệ.", 0);

            var expectedColumns = new[]
            {
                "language", "job_role", "skill", "subskill", "difficulty", "experience_level", 
                "level_tags", "company_category", "company_subcategory", "question_type", 
                "title", "problem_statement", "input_description", "output_description", 
                "constraints", "examples", "function_name", "function_parameters", "return_type", 
                "function_signature", "starter_code", "reference_solution", "public_test_cases", 
                "hidden_test_cases", "supported_programming_languages", "time_limit_seconds", 
                "memory_limit_mb", "expected_time_complexity", "expected_space_complexity", 
                "solution_explanation", "evaluation_criteria", "keywords", "keyword_tags", 
                "is_active", "embedding_text", "qdrant_payload_json"
            };

            List<Dictionary<string, string>> rows;
            try
            {
                rows = await CodingExcelParser.ParseExcelAsync(file, expectedColumns, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi parse Excel file coding question.");
                return (false, $"Lỗi xử lý file Excel: {ex.Message}", 0);
            }

            int importedCount = 0;
            var newQuestions = new List<CodingQuestion>();

            foreach (var row in rows)
            {
                // Title & Problem Statement are required
                if (string.IsNullOrWhiteSpace(row.GetValueOrDefault("title")) ||
                    string.IsNullOrWhiteSpace(row.GetValueOrDefault("problem_statement")))
                {
                    continue; // Skip invalid
                }

                double timeLimit = 2.0;
                if (double.TryParse(row.GetValueOrDefault("time_limit_seconds"), out var parsedTime))
                    timeLimit = parsedTime;

                int memoryLimit = 256000;
                if (int.TryParse(row.GetValueOrDefault("memory_limit_mb"), out var parsedMem))
                    memoryLimit = Math.Clamp(parsedMem * 1024, 1000, 512000);

                var q = new CodingQuestion
                {
                    Title = row.GetValueOrDefault("title")!,
                    Description = row.GetValueOrDefault("problem_statement")!,
                    Language = row.GetValueOrDefault("language"),
                    JobRole = row.GetValueOrDefault("job_role"),
                    Skill = row.GetValueOrDefault("skill"),
                    Subskill = row.GetValueOrDefault("subskill"),
                    Difficulty = row.GetValueOrDefault("difficulty"),
                    ExperienceLevel = row.GetValueOrDefault("experience_level"),
                    LevelTags = row.GetValueOrDefault("level_tags"),
                    CompanyCategory = row.GetValueOrDefault("company_category"),
                    CompanySubcategory = row.GetValueOrDefault("company_subcategory"),
                    QuestionType = "Coding",
                    InputDescription = row.GetValueOrDefault("input_description"),
                    OutputDescription = row.GetValueOrDefault("output_description"),
                    Constraints = row.GetValueOrDefault("constraints"),
                    Examples = row.GetValueOrDefault("examples"),
                    FunctionName = row.GetValueOrDefault("function_name"),
                    FunctionParameters = row.GetValueOrDefault("function_parameters"),
                    ReturnType = row.GetValueOrDefault("return_type"),
                    FunctionSignature = row.GetValueOrDefault("function_signature"),
                    StarterCode = row.GetValueOrDefault("starter_code"),
                    ReferenceSolution = row.GetValueOrDefault("reference_solution"),
                    PublicTestCases = row.GetValueOrDefault("public_test_cases"),
                    HiddenTestCases = row.GetValueOrDefault("hidden_test_cases"),
                    SupportedProgrammingLanguages = row.GetValueOrDefault("supported_programming_languages"),
                    TimeLimit = timeLimit,
                    MemoryLimit = memoryLimit,
                    ExpectedTimeComplexity = row.GetValueOrDefault("expected_time_complexity"),
                    ExpectedSpaceComplexity = row.GetValueOrDefault("expected_space_complexity"),
                    SolutionExplanation = row.GetValueOrDefault("solution_explanation"),
                    EvaluationCriteria = row.GetValueOrDefault("evaluation_criteria"),
                    Keywords = row.GetValueOrDefault("keywords"),
                    KeywordTags = row.GetValueOrDefault("keyword_tags"),
                    IsActive = string.Equals(row.GetValueOrDefault("is_active"), "TRUE", StringComparison.OrdinalIgnoreCase) || row.GetValueOrDefault("is_active") == "1",
                    EmbeddingText = row.GetValueOrDefault("embedding_text"),
                    QdrantPayloadJson = row.GetValueOrDefault("qdrant_payload_json"),
                    CreatedAt = DateTime.UtcNow
                };

                if (!string.IsNullOrWhiteSpace(q.PublicTestCases))
                {
                    ParseAndAddTestCases(q, q.PublicTestCases, isSample: true, isHidden: false, logger: _logger);
                }
                if (!string.IsNullOrWhiteSpace(q.HiddenTestCases))
                {
                    ParseAndAddTestCases(q, q.HiddenTestCases, isSample: false, isHidden: true, logger: _logger);
                }
                if (!string.IsNullOrWhiteSpace(q.StarterCode))
                {
                    ParseAndAddTemplates(q, q.StarterCode, logger: _logger);
                }

                newQuestions.Add(q);
            }

            if (newQuestions.Count > 0)
            {
                await _context.CodingQuestions.AddRangeAsync(newQuestions, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                importedCount = newQuestions.Count;
            }

            return (true, null, importedCount);
        }

        private static void ParseAndAddTestCases(CodingQuestion q, string jsonText, bool isSample, bool isHidden, ILogger logger)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonText);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return;

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    string inputStr = "";
                    string expectedOutputStr = "";

                    if (item.TryGetProperty("input", out var inputProp))
                    {
                        inputStr = ExtractJsonString(inputProp);
                    }

                    if (item.TryGetProperty("expected_output", out var expProp) ||
                        item.TryGetProperty("expectedOutput", out expProp))
                    {
                        expectedOutputStr = ExtractJsonString(expProp);
                    }

                    q.TestCases.Add(new TestCase
                    {
                        Input = inputStr,
                        ExpectedOutput = expectedOutputStr,
                        IsSample = isSample,
                        IsHidden = isHidden
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Không thể parse test cases JSON cho câu hỏi {Title}", q.Title);
            }
        }

        private static void ParseAndAddTemplates(CodingQuestion q, string jsonText, ILogger logger)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonText);
                var langMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { "python", 71 }, { "python3", 71 }, { "py", 71 },
                    { "javascript", 63 }, { "js", 63 }, { "nodejs", 63 },
                    { "java", 62 },
                    { "cpp", 54 }, { "c++", 54 }, { "c", 54 },
                    { "csharp", 51 }, { "c#", 51 }, { "cs", 51 }
                };

                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (langMap.TryGetValue(prop.Name, out var langId) || int.TryParse(prop.Name, out langId))
                        {
                            q.CodingQuestionTemplates.Add(new CodingQuestionTemplate
                            {
                                LanguageId = langId,
                                TemplateCode = ExtractJsonString(prop.Value)
                            });
                        }
                    }
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        int langId = 0;
                        string templateCode = "";

                        if (item.TryGetProperty("languageId", out var lIdProp) && lIdProp.TryGetInt32(out var parsedLId))
                            langId = parsedLId;
                        else if (item.TryGetProperty("language", out var lProp) && langMap.TryGetValue(lProp.GetString() ?? "", out var mappedId))
                            langId = mappedId;

                        if (item.TryGetProperty("templateCode", out var tcProp) ||
                            item.TryGetProperty("code", out tcProp) ||
                            item.TryGetProperty("starter_code", out tcProp))
                        {
                            templateCode = ExtractJsonString(tcProp);
                        }

                        if (langId > 0 && !string.IsNullOrWhiteSpace(templateCode))
                        {
                            q.CodingQuestionTemplates.Add(new CodingQuestionTemplate
                            {
                                LanguageId = langId,
                                TemplateCode = templateCode
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Không thể parse starter code JSON cho câu hỏi {Title}", q.Title);
            }
        }

        private static string ExtractJsonString(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? "",
                JsonValueKind.Null or JsonValueKind.Undefined => "",
                _ => element.GetRawText()
            };
        }
    }
}
