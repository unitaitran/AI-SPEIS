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

        public async Task<int> CreateQuestionsAsync(
            IReadOnlyCollection<Question> questions,
            CancellationToken cancellationToken = default)
        {
            if (questions.Count == 0)
            {
                return 0;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(
                cancellationToken);

            await _context.Questions.AddRangeAsync(questions, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return questions.Count;
        }

        public async Task UpdateQuestionAsync(
            Question question,
            CancellationToken cancellationToken = default)
        {
            _context.Questions.Update(question);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<PagedResultDto<Question>> GetQuestionsAsync(
            UserQuestionQueryDto query,
            CancellationToken cancellationToken = default)
        {
            var questions = ApplyUserFilters(
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

        private static IQueryable<Question> ApplyUserFilters(
            IQueryable<Question> questions,
            UserQuestionQueryDto query)
        {
            var major = Normalize(query.Major);
            var roleTarget = Normalize(query.RoleTarget);

            questions = questions.Where(q => !q.IsDeleted);

            questions = WhereIf(
                questions,
                roleTarget is not null,
                q => q.RoleTarget == roleTarget);

            questions = WhereIf(
                questions,
                major is not null,
                q => q.Major == major);

            if (Enum.TryParse<QuestionDifficultyEnum>(
                Normalize(query.Difficulty),
                ignoreCase: true,
                out var difficulty))
            {
                questions = questions.Where(q => q.Difficulty == difficulty);
            }

            return questions;
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
