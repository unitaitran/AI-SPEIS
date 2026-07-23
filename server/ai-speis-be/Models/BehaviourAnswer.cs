using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.Models
{
    [Table("BehaviourAnswer")]
    public class BehaviourAnswer 
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BehaviourAnswerId { get; set; }
        [Required]
        [ForeignKey("BehaviourSessionQuestion")]
        public int BehaviourSessionQuestionId { get; set; }
        [Required]
        public string Transcript { get; set; } = string.Empty;
        [MaxLength(200)]
        public string? AudioId { get; set; }
        [MaxLength(128)]
        public string? SubmissionIdempotencyKey { get; set; }
        public int AnswerVersion { get; set; } = 1;
        public decimal? SttConfidence { get; set; }
        public decimal? AiSituationScore { get; set; }       // Điểm mô tả hoàn cảnh (Situation)
        public decimal? AiActionScore { get; set; }          // Điểm hành động đã làm (Action)
        public decimal? AiResultScore { get; set; }          // Điểm kết quả đạt được (Result)
        public decimal? AiCompetencyScore { get; set; }      // Điểm năng lực lõi (Competency)
        public decimal? AiCommunicationScore { get; set; }   // Điểm giao tiếp rõ ràng (Communication)

        // Chi tiết AI nhận xét từng tiêu chí (JSON). 
        // Ví dụ: {"situation": {"score": 4, "evidence": "Mô tả rõ ràng khó khăn dự án", "missing": "Chưa nhắc đến timeline"}}
        public string? AiCriteriaDetailJson { get; set; }
        public string? AiEvidenceJson { get; set; }
        public string? AiMissingAspectsJson { get; set; }
        public int? AiOverallRubricScore { get; set; }
        public BehaviourAnswerQuality? AiAnswerQuality { get; set; }
        public string? AiStrengths { get; set; }
        public string? AiMissingPoints { get; set; }
        public BehaviourResolvedAction? AiRecommendedAction { get; set; }
        public BehaviourResolvedAction? ResolvedAction { get; set; }
        public decimal? ComputedScore { get; set; }
        public decimal? FinalQuestionScore { get; set; }

        // PROCESSING | COMPLETED | FALLBACK
        public string? EvaluationStatus { get; set; }
        // Provider/validation error code when EvaluationStatus == FALLBACK.
        public string? AiErrorCode { get; set; }
        [MaxLength(120)]
        public string? EvaluationModel { get; set; }
        [MaxLength(80)]
        public string? EvaluationPromptVersion { get; set; }
        public int? EvaluationInputTokens { get; set; }
        public int? EvaluationOutputTokens { get; set; }
        public long? EvaluationLatencyMs { get; set; }
        public int EvaluationRetryCount { get; set; }

        [Required] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual BehaviourSessionQuestion BehaviourSessionQuestion { get; set; } = null!;
    }
}
