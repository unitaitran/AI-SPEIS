using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ai_speis_be.Models
{
    [Table("TestCase")]
    public class TestCase
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TestCaseId { get; set; }

        [Required]
        [ForeignKey("CodingQuestion")]
        public int CodingQuestionId { get; set; }

        public string? Input { get; set; } // Can be empty or null for some questions

        [Required]
        public string ExpectedOutput { get; set; } = null!;

        [Required]
        public bool IsSample { get; set; } = false;

        [Required]
        public bool IsHidden { get; set; } = true;

        // Navigation properties
        public virtual CodingQuestion CodingQuestion { get; set; } = null!;

        public virtual ICollection<SubmissionTestCaseResult> SubmissionTestCaseResults { get; set; } = new List<SubmissionTestCaseResult>();
    }
}
