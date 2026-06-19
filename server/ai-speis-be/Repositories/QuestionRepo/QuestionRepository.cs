using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace ai_speis_be.Repositories.QuestionRepo
{
    public class QuestionRepository : IQuestionRepoitory
    {
        private readonly ApplicationDbContext _context;
        public QuestionRepository (ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResultDto<Question>> GetAdminQuestionsAsync(
            AdminQuestionQueryDto query,
            CancellationToken cancellationToken = default)
        {
            var questions = ApplyAdminFilters(
                _context.Questions.AsNoTracking(),
                query);

            var totalItems = await questions.CountAsync(cancellationToken);

            var items = await questions
                .OrderByDescending(q => q.CreatedAt)
                .ThenByDescending(q => q.QuestionId)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResultDto<Question>
            {
                Items = items,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                TotalItems = totalItems
            };
        }

        public async Task<Question?> GetQuestionByIdAdminAsync(
            int questionId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Questions.FirstOrDefaultAsync(
                q => q.QuestionId == questionId,
                cancellationToken);
        }

        public async Task<Question?> GetQuestionByIdAsync(
            int questionId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Questions.FirstOrDefaultAsync(
                q => q.QuestionId == questionId && q.IsDeleted == false,
                cancellationToken);
        }

        public async Task<Question> CreateQuestionAsync(
            Question question,
            CancellationToken cancellationToken = default)
        {
            await _context.Questions.AddAsync(question, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return question;
        }

        public async Task UpdateQuestionAsync(
            Question question,
            CancellationToken cancellationToken = default)
        {
            _context.Questions.Update(question);
            await _context.SaveChangesAsync(cancellationToken);
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
                questions = questions.Where(q => q.Major == major);
            }
            if(!string.IsNullOrEmpty(difficulty) && Enum.TryParse<QuestionDifficultyEnum>(difficulty, true, out var difficultyEnum))
            {
                questions = questions.Where(q => q.Difficulty == difficultyEnum);
            }
            return await questions.ToListAsync();
        }

        private static IQueryable<Question> ApplyAdminFilters(
            IQueryable<Question> questions,
            AdminQuestionQueryDto query)
        {
            var keyword = Normalize(query.Keyword);
            var major = Normalize(query.Major);
            var roleTarget = Normalize(query.RoleTarget);

            questions = WhereIf(
                questions,
                !query.IncludeDeleted,
                q => !q.IsDeleted);

            questions = WhereIf(
                questions,
                keyword is not null,
                q => q.QuestionContent.Contains(keyword!) ||
                    q.SuggestedAnswer.Contains(keyword!) ||
                    q.Major.Contains(keyword!) ||
                    q.RoleTarget.Contains(keyword!));

            questions = WhereIf(
                questions,
                major is not null,
                q => q.Major == major);

            questions = WhereIf(
                questions,
                roleTarget is not null,
                q => q.RoleTarget == roleTarget);

            questions = WhereIf(
                questions,
                query.Difficulty.HasValue,
                q => q.Difficulty == query.Difficulty!.Value);

            return questions;
        }

        private static IQueryable<Question> WhereIf(
            IQueryable<Question> questions,
            bool condition,
            Expression<Func<Question, bool>> predicate)
        {
            return condition ? questions.Where(predicate) : questions;
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
