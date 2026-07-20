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
                .OrderBy(q => q.QuestionId)
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

        public async Task<QuestionFiltersDto> GetQuestionFiltersAsync(CancellationToken cancellationToken = default)
        {
            var majors = await _context.Questions
                .Where(q => !q.IsDeleted && !string.IsNullOrEmpty(q.Major))
                .Select(q => q.Major)
                .Distinct()
                .ToListAsync(cancellationToken);

            var roles = await _context.Questions
                .Where(q => !q.IsDeleted && !string.IsNullOrEmpty(q.RoleTarget))
                .Select(q => q.RoleTarget)
                .Distinct()
                .ToListAsync(cancellationToken);

            return new QuestionFiltersDto
            {
                Majors = majors,
                RoleTargets = roles
            };
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

        public async Task<IReadOnlyList<Question>> GetTechnicalCandidatesAsync(
            TechnicalQuestionCandidateQuery query,
            CancellationToken cancellationToken = default)
        {
            var questions = _context.Questions
                .AsNoTracking()
                .Where(question => !question.IsDeleted && question.QuestionType == "Technical");

            if (!string.IsNullOrWhiteSpace(query.Language))
            {
                questions = questions.Where(question => question.Language == query.Language);
            }

            if (query.RoleTargets.Count > 0)
            {
                questions = questions.Where(question => query.RoleTargets.Contains(question.RoleTarget));
            }

            if (query.ExperienceLevels.Count > 0)
            {
                questions = questions.Where(question =>
                    question.ExperienceLevel != null
                    && query.ExperienceLevels.Contains(question.ExperienceLevel));
            }

            if (query.Skills.Count > 0)
            {
                questions = questions.Where(question =>
                    question.Skill != null && query.Skills.Contains(question.Skill));
            }

            if (query.Difficulty.HasValue)
            {
                questions = questions.Where(question => question.Difficulty == query.Difficulty.Value);
            }

            if (query.ExcludedQuestionIds.Count > 0)
            {
                questions = questions.Where(question => !query.ExcludedQuestionIds.Contains(question.QuestionId));
            }

            return await questions
                .OrderBy(question => question.QuestionId)
                .Take(Math.Clamp(query.MaximumResults, 1, 500))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<string>> GetTechnicalSkillsAsync(
            string language,
            IReadOnlyCollection<string> roleTargets,
            CancellationToken cancellationToken = default)
        {
            var questions = _context.Questions
                .AsNoTracking()
                .Where(question =>
                    !question.IsDeleted
                    && question.QuestionType == "Technical"
                    && question.Language == language
                    && question.Skill != null);

            if (roleTargets.Count > 0)
            {
                questions = questions.Where(question => roleTargets.Contains(question.RoleTarget));
            }

            return await questions
                .Select(question => question.Skill!)
                .Distinct()
                .OrderBy(skill => skill)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Question>> GetActiveTechnicalQuestionsByIdsAsync(
            IReadOnlyCollection<int> questionIds,
            CancellationToken cancellationToken = default)
        {
            if (questionIds.Count == 0)
            {
                return Array.Empty<Question>();
            }

            return await _context.Questions
                .AsNoTracking()
                .Where(question =>
                    questionIds.Contains(question.QuestionId)
                    && !question.IsDeleted
                    && question.QuestionType == "Technical")
                .OrderBy(question => question.QuestionId)
                .ToListAsync(cancellationToken);
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
                q => q.RoleTarget.Contains(roleTarget!));

            questions = WhereIf(
                questions,
                major is not null,
                q => q.Major.Contains(major!));

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
                q => q.Major.Contains(major!));

            questions = WhereIf(
                questions,
                roleTarget is not null,
                q => q.RoleTarget.Contains(roleTarget!));

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
