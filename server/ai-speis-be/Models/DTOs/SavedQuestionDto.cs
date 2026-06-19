namespace ai_speis_be.Models.DTOs
{
    public class SavedQuestionDto
    {
        public int SavedQuestionId { get; set; }    
        public int UserId { get; set; }
        public int QuestionId { get; set; }
        public QuestionResponseDto Question { get; set; } = null!;
        public DateTime SavedAt { get; set; }   
    }
}
