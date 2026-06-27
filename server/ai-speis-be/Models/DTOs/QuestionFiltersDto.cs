namespace ai_speis_be.Models.DTOs
{
    public class QuestionFiltersDto
    {
        public List<string> Majors { get; set; } = new();
        public List<string> RoleTargets { get; set; } = new();
    }
}
