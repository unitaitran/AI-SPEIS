using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ai_speis_be.Models
{
    [Table("BehaviourRoundResult")]
    public class BehaviourRoundResult 
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BehaviourRoundResultId { get; set; }
        [Required]
        [ForeignKey("InterviewSession")]
        public int InterviewSessionId { get; set; }
        public decimal? OverallScore { get; set; }
        public string? SkillScoresJson { get; set; }
        public string? CriteriaAveragesJson { get; set; }
        public string? AiExecutiveSummary { get; set; }
        public string? AiStrengths { get; set; }
        public string? AiGaps { get; set; }
        public string? AiLevelAssessment { get; set; }
        public string? AiRecommendations { get; set; }
        public DateTime? CompletedAt { get; set; }
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual InterviewSession InterviewSession { get; set; } = null!;
    }
}