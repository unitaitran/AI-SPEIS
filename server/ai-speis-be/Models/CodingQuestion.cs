using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.Models
{
    [Table("CodingQuestion")]
    public class CodingQuestion
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CodingQuestionId { get; set; }

        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = null!;

        [Required]
        public string Description { get; set; } = null!; // Maps to problem_statement

        public double TimeLimit { get; set; } = 2.0; // seconds

        public int MemoryLimit { get; set; } = 256000; // KB

        // --- Global Question Bank Fields ---
        public string? Language { get; set; }
        public string? JobRole { get; set; }
        public string? Skill { get; set; }
        public string? Subskill { get; set; }
        public string? Difficulty { get; set; }
        public string? ExperienceLevel { get; set; }
        public string? LevelTags { get; set; }
        public string? CompanyCategory { get; set; }
        public string? CompanySubcategory { get; set; }
        public string? QuestionType { get; set; } // "Coding"
        
        public string? InputDescription { get; set; }
        public string? OutputDescription { get; set; }
        public string? Constraints { get; set; }
        public string? Examples { get; set; }
        
        public string? FunctionName { get; set; }
        public string? FunctionParameters { get; set; }
        public string? ReturnType { get; set; }
        public string? FunctionSignature { get; set; }
        
        public string? StarterCode { get; set; }
        public string? ReferenceSolution { get; set; }
        public string? PublicTestCases { get; set; }
        public string? HiddenTestCases { get; set; }
        public string? SupportedProgrammingLanguages { get; set; }
        
        public string? ExpectedTimeComplexity { get; set; }
        public string? ExpectedSpaceComplexity { get; set; }
        public string? SolutionExplanation { get; set; }
        public string? EvaluationCriteria { get; set; }
        
        public string? Keywords { get; set; }
        public string? KeywordTags { get; set; }
        public bool IsActive { get; set; } = true;
        
        public string? EmbeddingText { get; set; }
        public string? QdrantPayloadJson { get; set; }

        [Required]
        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public int? DeletedBy { get; set; }

        // Navigation properties
        public virtual ICollection<CodingQuestionTemplate> CodingQuestionTemplates { get; set; } = new List<CodingQuestionTemplate>();

        public virtual ICollection<TestCase> TestCases { get; set; } = new List<TestCase>();

        public virtual ICollection<CodingSubmission> CodingSubmissions { get; set; } = new List<CodingSubmission>();
    }
}
