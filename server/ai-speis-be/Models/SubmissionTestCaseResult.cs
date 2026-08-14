using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ai_speis_be.Models
{
    [Table("SubmissionTestCaseResult")]
    public class SubmissionTestCaseResult
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ResultId { get; set; }

        [Required]
        [ForeignKey("CodingSubmission")]
        public int CodingSubmissionId { get; set; }

        [Required]
        [ForeignKey("TestCase")]
        public int TestCaseId { get; set; }

        public string? ActualOutput { get; set; }

        public string? Stderr { get; set; }

        public string? CompileOutput { get; set; }

        [Required]
        public double TimeMs { get; set; } = 0.0;

        [Required]
        public int MemoryKb { get; set; } = 0;

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = null!; // Accepted, Wrong Answer, etc.

        // Navigation properties
        public virtual CodingSubmission CodingSubmission { get; set; } = null!;

        public virtual TestCase TestCase { get; set; } = null!;
    }
}
