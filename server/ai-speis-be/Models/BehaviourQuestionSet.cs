using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.Models
{
    [Table("BehaviourQuestionSet")]
    public class BehaviourQuestionSet
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BehaviourQuestionSetId { get; set; }
        [Required]
        [ForeignKey("InterviewSession")]
        public int InterviewSessionId { get; set; }
        //Nguồn lựa chọn bộ câu hỏi, mặc định là do AI, nếu AI lỗi thì hệ thống tự bốc từ trong bank 
        [Required]
        public BehaviourSelectionSource SelectionSource { get; set; } = BehaviourSelectionSource.ExternalAi;
        public string? AiExecutionRunId { get; set; }
        [Required] 
        public int QuestionCount { get; set; } = 5;
        public string? CoveredSkillsJson { get; set; }
        public string? ConstraintsJson { get; set; }
        [Required] 
        public BehaviourQuestionSetStatus Status { get; set; } = BehaviourQuestionSetStatus.Active;
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public virtual InterviewSession InterviewSession { get; set; } = null!;
        public virtual ICollection<BehaviourSessionQuestion> BehaviourSessionQuestion { get; set; } = [];
    }
}