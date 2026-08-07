using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.Models
{
    [Table("BehaviourSessionQuestion")]
    public class BehaviourSessionQuestion 
    {

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BehaviourSessionQuestionId { get; set; }
        [Required]
        [ForeignKey("BehaviourQuestionSet")]
        public int BehaviourQuestionSetId { get; set; }
        [Required]
        [ForeignKey("Question")]
        public int QuestionId { get; set; }
        [Required]
        public int QuestionOrder { get; set; }
        [Required]
        public BehaviourQuestionType QuestionType { get; set; } = BehaviourQuestionType.Main;
        public int? ParentQuestionId { get; set; }
        // Bản copy nội dung câu hỏi lúc thi (JSON), phòng khi bảng Question sau này bị sửa đổi. 
        // Ví dụ: {"text": "Kể về lần mâu thuẫn...", "timeLimit": 120}
        [Required]
        public string QuestionSnapshotJson { get; set; } = string.Empty;
        [Required]
        public BehaviourQuestionStatus Status { get; set; } = BehaviourQuestionStatus.Pending;
        public DateTime? AskedAt { get; set; }
        public DateTime? AnsweredAt { get; set; }
        public virtual BehaviourQuestionSet BehaviourQuestionSet { get; set; } = null!;
        public virtual Question Question { get; set; } = null!;
        public virtual BehaviourSessionQuestion? ParentQuestion { get; set; }
        public virtual BehaviourAnswer? BehaviourAnswerAnswer { get; set; }
    }
}