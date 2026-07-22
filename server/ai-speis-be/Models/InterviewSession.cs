using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.Models
{
    [Table("InterviewSession")]
    public class InterviewSession
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int InterviewSessionId { get; set; }

        [Required]
        [ForeignKey("InterviewCampaign")]
        public int InterviewCampaignId { get; set; }

        [Required]
        public InterviewRoundType InterviewRoundType { get; set; }

        [Required]
        public QuestionDifficultyEnum Difficulty { get; set; }

        [Required]
        public int QuestionCount { get; set; } = 5;

        [Required]
        public InterviewSessionStatus Status { get; set; } = InterviewSessionStatus.Pending;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [Required]
        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        // Technical Interview runtime metadata. Nullable fields preserve existing sessions.
        public TechnicalInterviewState? TechnicalState { get; set; }

        [MaxLength(50)]
        public string? TechnicalAiProvider { get; set; }

        [MaxLength(120)]
        public string? TechnicalAiModel { get; set; }

        [MaxLength(50)]
        public string? TechnicalRubricVersion { get; set; }

        [MaxLength(50)]
        public string? TechnicalScoringPolicyVersion { get; set; }

        public int? TechnicalMatchScoreSnapshot { get; set; }

        public TechnicalMatchBand? TechnicalMatchBand { get; set; }

        public int? TechnicalPlannedCvQuestionCount { get; set; }

        public int? TechnicalPlannedJdQuestionCount { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? TechnicalQuestionPlanJson { get; set; }

        [MaxLength(80)]
        public string? TechnicalQuestionPlanVersion { get; set; }

        [MaxLength(80)]
        public string? TechnicalAdaptiveRuleVersion { get; set; }

        [MaxLength(80)]
        public string? TechnicalBonusCalculationVersion { get; set; }

        [MaxLength(200)]
        public string? TechnicalJobRole { get; set; }

        [MaxLength(100)]
        public string? TechnicalExperienceLevel { get; set; }

        [MaxLength(10)]
        public string? TechnicalLanguage { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? TechnicalSelectedSkillsJson { get; set; }

        public int TechnicalCompletedMainQuestionCount { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? TechnicalFinalScore { get; set; }

        [MaxLength(50)]
        public string? TechnicalPerformanceBand { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? TechnicalSummaryJson { get; set; }

        [MaxLength(500)]
        public string? TechnicalReliabilityFailureReason { get; set; }

        [MaxLength(500)]
        public string? TechnicalLegacyUpgradeFailureReason { get; set; }

        public DateTime? TechnicalStartedAt { get; set; }

        public DateTime? TechnicalCompletedAt { get; set; }

        public int TechnicalConcurrencyVersion { get; set; }

        // Navigation properties
        public virtual InterviewCampaign InterviewCampaign { get; set; } = null!;

        public virtual ICollection<TechnicalQuestionAttempt> TechnicalQuestionAttempts { get; set; } = new List<TechnicalQuestionAttempt>();

        public virtual ICollection<AIInteractionLog> AIInteractionLogs { get; set; } = new List<AIInteractionLog>();
    }
}
