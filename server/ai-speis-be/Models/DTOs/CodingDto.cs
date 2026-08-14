using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ai_speis_be.Models.DTOs
{
    // =============================================
    // Request DTOs
    // =============================================

    /// <summary>
    /// Request body khi user submit code để chạy qua Judge0.
    /// </summary>
    public class SubmitCodeRequestDto
    {
        [Required]
        public int CodingQuestionId { get; set; }

        [Required]
        public int InterviewSessionId { get; set; }

        [Required]
        public string SourceCode { get; set; } = null!;

        [Required]
        public int LanguageId { get; set; }

        public bool IsTestRun { get; set; } = false;
    }

    // =============================================
    // Response DTOs
    // =============================================

    /// <summary>
    /// Response trả về sau khi submit code, chứa kết quả tổng hợp và từng test case.
    /// </summary>
    public class SubmissionResponseDto
    {
        public int CodingSubmissionId { get; set; }
        public int CodingQuestionId { get; set; }
        public string Status { get; set; } = null!;
        public int TotalTestCases { get; set; }
        public int PassedTestCases { get; set; }
        public double MaxTimeMs { get; set; }
        public int MaxMemoryKb { get; set; }
        public string? CompileOutput { get; set; }
        public string? Stderr { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<TestCaseResultDto> TestCaseResults { get; set; } = new();
    }

    /// <summary>
    /// Kết quả chi tiết từng test case.
    /// Với hidden test cases, ActualOutput và ExpectedOutput sẽ bị ẩn.
    /// </summary>
    public class TestCaseResultDto
    {
        public int TestCaseId { get; set; }
        public bool IsSample { get; set; }
        public string Status { get; set; } = null!;
        public string? Input { get; set; }
        public string? ActualOutput { get; set; }
        public string? ExpectedOutput { get; set; }
        public string? Stderr { get; set; }
        public string? CompileOutput { get; set; }
        public double TimeMs { get; set; }
        public int MemoryKb { get; set; }
    }

    /// <summary>
    /// Thông tin câu hỏi coding trả về cho client.
    /// Chỉ bao gồm sample test cases (không trả hidden).
    /// </summary>
    public class CodingQuestionResponseDto
    {
        public int CodingQuestionId { get; set; }
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public double TimeLimit { get; set; }
        public int MemoryLimit { get; set; }

        public string? JobRole { get; set; }
        public string? Skill { get; set; }
        public string? Subskill { get; set; }
        public string? Difficulty { get; set; }
        public string? InputDescription { get; set; }
        public string? OutputDescription { get; set; }
        public string? Constraints { get; set; }
        public string? Examples { get; set; }
        public string? FunctionName { get; set; }
        public string? FunctionParameters { get; set; }
        public string? ReturnType { get; set; }
        public string? FunctionSignature { get; set; }
        public string? SupportedProgrammingLanguages { get; set; }
        public string? ExpectedTimeComplexity { get; set; }
        public string? ExpectedSpaceComplexity { get; set; }

        public List<CodingQuestionTemplateDto> Templates { get; set; } = new();
        public List<SampleTestCaseDto> SampleTestCases { get; set; } = new();
    }

    public class CodingQuestionTemplateDto
    {
        public int TemplateId { get; set; }
        public int LanguageId { get; set; }
        public string TemplateCode { get; set; } = null!;
    }

    public class SampleTestCaseDto
    {
        public int TestCaseId { get; set; }
        public string? Input { get; set; }
        public string ExpectedOutput { get; set; } = null!;
    }

    /// <summary>
    /// Tóm tắt lịch sử submission (không bao gồm chi tiết test case results).
    /// </summary>
    public class SubmissionSummaryDto
    {
        public int CodingSubmissionId { get; set; }
        public int CodingQuestionId { get; set; }
        public int LanguageId { get; set; }
        public string Status { get; set; } = null!;
        public int TotalTestCases { get; set; }
        public int PassedTestCases { get; set; }
        public double MaxTimeMs { get; set; }
        public int MaxMemoryKb { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // =============================================
    // Internal DTOs — Giao tiếp với Judge0 API
    // =============================================

    /// <summary>
    /// Request body gửi đến Judge0 API cho 1 submission.
    /// </summary>
    public class Judge0SubmissionRequest
    {
        public string source_code { get; set; } = null!;
        public int language_id { get; set; }
        public string? stdin { get; set; }
        public double? cpu_time_limit { get; set; }
        public int? memory_limit { get; set; }
        public string? command_line_arguments { get; set; }
        public string? compiler_options { get; set; }
    }

    /// <summary>
    /// Request body cho batch submission đến Judge0 API.
    /// </summary>
    public class Judge0BatchRequest
    {
        public List<Judge0SubmissionRequest> submissions { get; set; } = new();
    }

    /// <summary>
    /// Response từ Judge0 POST batch submission (chứa token).
    /// </summary>
    public class Judge0TokenResponse
    {
        public string token { get; set; } = null!;
    }

    /// <summary>
    /// Response từ Judge0 GET batch submission (chứa danh sách kết quả submissions).
    /// </summary>
    public class Judge0BatchResponse
    {
        public List<Judge0SubmissionResponse> submissions { get; set; } = new();
    }

    /// <summary>
    /// Response từ Judge0 API cho 1 submission.
    /// </summary>
    public class Judge0SubmissionResponse
    {
        public string? stdout { get; set; }
        public string? stderr { get; set; }
        public string? compile_output { get; set; }
        public string? message { get; set; }
        public string? time { get; set; }
        public int? memory { get; set; }
        public string? token { get; set; }
        public Judge0Status? status { get; set; }
    }

    public class Judge0Status
    {
        public int id { get; set; }
        public string description { get; set; } = null!;
    }

    /// <summary>
    /// Response từ Judge0 API cho danh sách ngôn ngữ.
    /// </summary>
    public class Judge0LanguageDto
    {
        public int id { get; set; }
        public string name { get; set; } = null!;
    }
}
