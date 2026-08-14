using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ai_speis_be.Models
{
    [Table("CodingSubmission")]
    public class CodingSubmission
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CodingSubmissionId { get; set; }

        [Required]
        [ForeignKey("InterviewSession")]
        public int InterviewSessionId { get; set; }

        [Required]
        [ForeignKey("CodingQuestion")]
        public int CodingQuestionId { get; set; }

        [Required]
        public string SourceCode { get; set; } = null!;

        [Required]
        public int LanguageId { get; set; } // Judge0 Language ID

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Queue"; // Queue, Processing, Accepted, WrongAnswer, Runtime Error, etc.

        [Required]
        public int TotalTestCases { get; set; } = 0;

        [Required]
        public int PassedTestCases { get; set; } = 0;

        [Required]
        public double MaxTimeMs { get; set; } = 0.0;

        [Required]
        public int MaxMemoryKb { get; set; } = 0;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual InterviewSession InterviewSession { get; set; } = null!;

        public virtual CodingQuestion CodingQuestion { get; set; } = null!;

        public virtual ICollection<SubmissionTestCaseResult> SubmissionTestCaseResults { get; set; } = new List<SubmissionTestCaseResult>();
    }
}
