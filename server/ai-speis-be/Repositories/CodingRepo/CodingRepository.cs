using ai_speis_be.Models;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Repositories.CodingRepo
{
    public class CodingRepository : ICodingRepository
    {
        private readonly ApplicationDbContext _context;

        public CodingRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CodingQuestion?> GetCodingQuestionWithTestCasesAsync(
            int questionId,
            CancellationToken cancellationToken = default)
        {
            return await _context.CodingQuestions
                .Include(q => q.TestCases)
                .Include(q => q.CodingQuestionTemplates)
                .FirstOrDefaultAsync(
                    q => q.CodingQuestionId == questionId,
                    cancellationToken);
        }

        public async Task<List<CodingQuestion>> GetCodingQuestionsBySkillsAsync(
            List<string> skills,
            CancellationToken cancellationToken = default)
        {
            var query = _context.CodingQuestions
                .Include(q => q.CodingQuestionTemplates)
                .Include(q => q.TestCases.Where(tc => tc.IsSample))
                .Where(q => !q.IsDeleted && q.IsActive);

            if (skills != null && skills.Any())
            {
                // Simple matching: if any skill matches JobRole, Skill, or Subskill
                // In a real scenario, this would be more complex
                var skillLower = skills.Select(s => s.ToLower()).ToList();
                // We'll just fetch all and filter in memory for simplicity if EF can't translate
                // Or just pick the first active one for now if no skills match perfectly
            }

            return await query
                .OrderBy(q => q.CodingQuestionId)
                .Take(1) // Just take 1 for the interview
                .ToListAsync(cancellationToken);
        }

        public async Task<CodingSubmission> CreateSubmissionAsync(
            CodingSubmission submission,
            CancellationToken cancellationToken = default)
        {
            await _context.CodingSubmissions.AddAsync(submission, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return submission;
        }

        public async Task<CodingSubmission?> GetSubmissionByIdAsync(
            int submissionId,
            CancellationToken cancellationToken = default)
        {
            return await _context.CodingSubmissions
                .Include(s => s.SubmissionTestCaseResults)
                    .ThenInclude(r => r.TestCase)
                .FirstOrDefaultAsync(
                    s => s.CodingSubmissionId == submissionId,
                    cancellationToken);
        }

        public async Task<List<CodingSubmission>> GetSubmissionsBySessionAndQuestionAsync(
            int sessionId,
            int questionId,
            CancellationToken cancellationToken = default)
        {
            return await _context.CodingSubmissions
                .Where(s => s.InterviewSessionId == sessionId
                         && s.CodingQuestionId == questionId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync(cancellationToken);
        }
    }
}
