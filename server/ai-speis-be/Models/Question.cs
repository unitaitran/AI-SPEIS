using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ai_speis_be.Models
{
    [Table("Question")]
    [Index(nameof(QuestionId), Name = "IX_Question_QuestionId", IsUnique = true)]
    [Index(nameof(UserId), Name = "IX_Question_UserId")]
    public class Question
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int QuestionId { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }
        [Required]
        public string QuestionContent { get; set; } = string.Empty;
        [Required]
        public string SuggestedAnswer { get; set; } = string.Empty;
        [Required]
        public QuestionDifficultyEnum Difficulty { get; set; }
        [Required]
        public string RoleTarget { get; set; } = string.Empty;
        [Required]
        public string Major { get; set; } = string.Empty;
        [Required]
        public bool IsDeleted { get; set; } = false;
        [Required]
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public int? DeletedBy { get; set; }
        public QuestionPurgeStatus PurgeStatus { get; set; } = QuestionPurgeStatus.None;
        public DateTime? PurgeRequestedAt { get; set; }
        public int? PurgeRequestedBy { get; set; }
        public int PurgeAttemptCount { get; set; }
        public string? LastPurgeError { get; set; }

        // --- Behavioral Question Fields (from interview_question.xlsx) ---

        /// <summary>Technical | Behavioral</summary>
        public string QuestionType { get; set; } = "Technical";

        /// <summary>vi | en</summary>
        public string? Language { get; set; }

        /// <summary>Competency category: Communication, Teamwork, Problem Solving...</summary>
        public string? Skill { get; set; }

        /// <summary>Intern/Fresher | Fresher/Junior | Junior | Junior/Middle | Middle | Middle/Senior | Senior</summary>
        public string? ExperienceLevel { get; set; }

        /// <summary>Comma-separated tags: "Intern,Fresher"</summary>
        public string? LevelTags { get; set; }

        /// <summary>Startup | Service Company | Product Company | Enterprise</summary>
        public string? CompanyCategory { get; set; }

        public string? CompanySubcategory { get; set; }

        /// <summary>Comma-separated key points expected in answer</summary>
        public string? ExpectedKeyPoints { get; set; }

        /// <summary>Scoring guidance or rubric used to evaluate an answer.</summary>
        public string? ScoringRubric { get; set; }

        /// <summary>Pre-written clarification question to use when answer is too vague</summary>
        public string? ClarificationQuestion { get; set; }

        /// <summary>Pre-written follow-up question #1</summary>
        public string? FollowUp1 { get; set; }

        /// <summary>Pre-written follow-up question #2 (deeper probe)</summary>
        public string? FollowUp2 { get; set; }

        /// <summary>Answer time limit in seconds (default 120)</summary>
        public int? TimeLimitSeconds { get; set; } = 120;

        /// <summary>Comma-separated lowercase keyword tags</summary>
        public string? KeywordTags { get; set; }

        /// <summary>Concatenated text used for vector embedding</summary>
        public string? EmbeddingText { get; set; }

        /// <summary>Metadata JSON for Qdrant vector DB</summary>
        public string? QdrantPayloadJson { get; set; }

        // --- Navigation Properties ---
        public virtual User User { get; set; } = null!;




    }
}
