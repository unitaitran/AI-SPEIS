using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ai_speis_be.Models
{
    [Table("CodingQuestion")]
    public class CodingQuestion
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CodingQuestionId { get; set; }

        [Required]
        [ForeignKey("InterviewSession")]
        public int InterviewSessionId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = null!;

        [Required]
        public string Description { get; set; } = null!;

        [Required]
        public double TimeLimit { get; set; } = 2.0; // seconds

        [Required]
        public int MemoryLimit { get; set; } = 256000; // KB

        // Navigation properties
        public virtual InterviewSession InterviewSession { get; set; } = null!;

        public virtual ICollection<CodingQuestionTemplate> CodingQuestionTemplates { get; set; } = new List<CodingQuestionTemplate>();

        public virtual ICollection<TestCase> TestCases { get; set; } = new List<TestCase>();

        public virtual ICollection<CodingSubmission> CodingSubmissions { get; set; } = new List<CodingSubmission>();
    }
}
