using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Repositories.QuestionRepo
{
    public class QuestionRepository : IQuestionRepoitory
    {
        private readonly ApplicationDbContext _context;
        public QuestionRepository (ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Question?> GetQuestionByIdAdminAsync(int questionId)
        {
            return await  _context.Questions.FirstOrDefaultAsync(q => q.QuestionId == questionId);
        }

        public async Task<Question?> GetQuestionByIdAsync(int questionId)
        {
            return await _context.Questions.FirstOrDefaultAsync(q => q.QuestionId == questionId && q.IsDeleted == false);
        }

        public async Task<IEnumerable<Question>> GetQuestionsAdminAsync(string? roleTarget, string? major, string? difficulty)
        {
            var questions = _context.Questions.AsQueryable();
            //filter
            if(!string.IsNullOrEmpty(roleTarget))
            {
                questions = questions.Where(q => q.RoleTarget == roleTarget);
            }
            if(!string.IsNullOrEmpty(major))
            {
                questions = questions.Where(q => q.RoleTarget == major);
            }
            if(!string.IsNullOrEmpty(difficulty) && Enum.TryParse<QuestionDifficultyEnum>(difficulty, true, out var difficultyEnum))
            {
                questions = questions.Where(q => q.Difficulty == difficultyEnum);
            }
            return await questions.ToListAsync();
        }
        public async Task<IEnumerable<Question>> GetQuestionsAsync(string? roleTarget, string? major, string? difficulty)
        {
            var questions = _context.Questions.Where(q => q.IsDeleted == false).AsQueryable();
            //filter
            if(!string.IsNullOrEmpty(roleTarget))
            {
                questions = questions.Where(q => q.RoleTarget == roleTarget);
            }
            if(!string.IsNullOrEmpty(major))
            {
                questions = questions.Where(q => q.RoleTarget == major);
            }
            if(!string.IsNullOrEmpty(difficulty) && Enum.TryParse<QuestionDifficultyEnum>(difficulty, true, out var difficultyEnum))
            {
                questions = questions.Where(q => q.Difficulty == difficultyEnum);
            }
            return await questions.ToListAsync();
        }
    }
}
