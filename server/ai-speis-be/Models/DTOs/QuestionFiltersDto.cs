namespace ai_speis_be.Models.DTOs
{
    public class QuestionFiltersDto
    {
        public List<string> Majors { get; set; } = new();
        public List<string> RoleTargets { get; set; } = new();
        public List<string> Difficulties { get; set; } = new();
        public List<string> InterviewTypes { get; set; } = new();
        public List<string> TechStacks { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public List<string> Statuses { get; set; } = new();
    }
}
