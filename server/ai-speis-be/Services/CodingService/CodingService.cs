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

namespace ai_speis_be.Services.CodingService
{
    public class CodingService : ICodingService
    {
        private readonly ICodingRepository _repository;
        private readonly IJudge0Service _judge0Service;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CodingService> _logger;

        public CodingService(
            ICodingRepository repository,
            IJudge0Service judge0Service,
            ApplicationDbContext context,
            ILogger<CodingService> logger)
        {
            _repository = repository;
            _judge0Service = judge0Service;
            _context = context;
            _logger = logger;
        }

        public async Task<(bool Success, string? ErrorMessage, SubmissionResponseDto? Data)> SubmitCodeAsync(
            int userId,
            SubmitCodeRequestDto request,
            CancellationToken cancellationToken = default)
        {
            // 1. Validate session thuộc về user và đang Active
            var session = await _context.InterviewSessions
                .Include(s => s.InterviewCampaign)
                .FirstOrDefaultAsync(
                    s => s.InterviewSessionId == request.InterviewSessionId,
                    cancellationToken);

            if (session == null)
                return (false, "Không tìm thấy phiên phỏng vấn.", null);

            if (session.InterviewCampaign.UserId != userId)
                return (false, "Bạn không có quyền truy cập phiên phỏng vấn này.", null);

            if (session.Status != InterviewSessionStatus.Active)
                return (false, "Phiên phỏng vấn chưa bắt đầu hoặc đã kết thúc.", null);

            // 2. Lấy câu hỏi + test cases
            var question = await _repository.GetCodingQuestionWithTestCasesAsync(
                request.CodingQuestionId, cancellationToken);

            if (question == null)
                return (false, "Không tìm thấy câu hỏi coding.", null);

            if (question.InterviewSessionId != request.InterviewSessionId)
                return (false, "Câu hỏi không thuộc phiên phỏng vấn này.", null);

            var testCases = question.TestCases.ToList();
            if (testCases.Count == 0)
                return (false, "Câu hỏi chưa có test case nào.", null);

            // 3. Tạo batch submissions gửi đến Judge0
            var judge0Requests = testCases.Select(tc => new Judge0SubmissionRequest
            {
                source_code = request.SourceCode,
                language_id = request.LanguageId,
                stdin = tc.Input ?? "",
                cpu_time_limit = question.TimeLimit,
                memory_limit = question.MemoryLimit
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
                    // Judge0 nói Accepted, nhưng cần kiểm tra output có đúng không
                    tcStatus = string.Equals(actualOutput, expectedOutput, StringComparison.Ordinal)
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

            // 5. Lưu vào database
            await _repository.CreateSubmissionAsync(submission, cancellationToken);

            _logger.LogInformation(
                "Submission {SubmissionId} cho câu hỏi {QuestionId}: {Passed}/{Total} passed — {Status}",
                submission.CodingSubmissionId, request.CodingQuestionId,
                passedCount, testCases.Count, overallStatus);

            // 6. Map sang response DTO
            var responseDto = MapToSubmissionResponseDto(submission, testCases);

            return (true, null, responseDto);
        }

        public async Task<(bool Success, string? ErrorMessage, List<CodingQuestionResponseDto>? Data)> GetCodingQuestionsAsync(
            int userId,
            int sessionId,
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

            var questions = await _repository.GetCodingQuestionsBySessionIdAsync(
                sessionId, cancellationToken);

            var dtos = questions.Select(q => new CodingQuestionResponseDto
            {
                CodingQuestionId = q.CodingQuestionId,
                Title = q.Title,
                Description = q.Description,
                TimeLimit = q.TimeLimit,
                MemoryLimit = q.MemoryLimit,
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

            return new SubmissionResponseDto
            {
                CodingSubmissionId = submission.CodingSubmissionId,
                CodingQuestionId = submission.CodingQuestionId,
                Status = submission.Status,
                TotalTestCases = submission.TotalTestCases,
                PassedTestCases = submission.PassedTestCases,
                MaxTimeMs = submission.MaxTimeMs,
                MaxMemoryKb = submission.MaxMemoryKb,
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
                        // Chỉ trả output cho sample test cases
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
    }
}
