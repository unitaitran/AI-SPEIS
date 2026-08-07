using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Repositories.JDRepo
{
    public class JDRepository : IJDRepository
    {
        private readonly ApplicationDbContext _context;
        public JDRepository (ApplicationDbContext context)
        {
            _context = context; 
        }
        public async Task<JDFile> AddJDAsync(JDFile jdFile)
        {
            await _context.JDFiles.AddAsync(jdFile);
            await _context.SaveChangesAsync();  
            return jdFile;
        }

        public async Task<JDFile?> DeleteJDAsync(int id)
        {
            var jdFile = await _context.JDFiles.FirstOrDefaultAsync(j => j.JDFileId == id);
            if (jdFile == null) return null;

            var profile = await _context.JDExtractedProfiles.FirstOrDefaultAsync(p => p.JDFileId == id);
            if (profile != null)
            {
                var campaigns = await _context.InterviewCampaigns
                    .Where(c => c.JDExtractedProfileId == profile.ExtractedProfileId)
                    .ToListAsync();

                if (campaigns.Any())
                {
                    await DeleteCampaignsAndDependenciesAsync(campaigns);
                }

                _context.JDExtractedProfiles.Remove(profile);
            }

            var fastCheckResults = await _context.FastCheckResults
                .Where(f => f.JDFileId == id)
                .ToListAsync();

            if (fastCheckResults.Any())
            {
                _context.FastCheckResults.RemoveRange(fastCheckResults);
            }

            _context.JDFiles.Remove(jdFile);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(jdFile.FilePath))
            {
                try
                {
                    var absolutePath = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        jdFile.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                    if (File.Exists(absolutePath))
                    {
                        File.Delete(absolutePath);
                    }
                }
                catch
                {
                    // Ignore physical file deletion error
                }
            }

            return jdFile;
        }

        private async Task DeleteCampaignsAndDependenciesAsync(List<InterviewCampaign> campaigns)
        {
            if (!campaigns.Any()) return;

            var campaignIds = campaigns.Select(c => c.InterviewCampaignId).ToList();
            var sessions = await _context.InterviewSessions
                .Where(s => campaignIds.Contains(s.InterviewCampaignId))
                .ToListAsync();

            if (sessions.Any())
            {
                var sessionIds = sessions.Select(s => s.InterviewSessionId).ToList();

                var submissions = await _context.CodingSubmissions
                    .Where(sub => sessionIds.Contains(sub.InterviewSessionId))
                    .ToListAsync();
                if (submissions.Any())
                {
                    var submissionIds = submissions.Select(sub => sub.CodingSubmissionId).ToList();
                    var testResults = await _context.SubmissionTestCaseResults
                        .Where(r => submissionIds.Contains(r.CodingSubmissionId))
                        .ToListAsync();
                    if (testResults.Any()) _context.SubmissionTestCaseResults.RemoveRange(testResults);
                    _context.CodingSubmissions.RemoveRange(submissions);
                }

                var aiLogs = await _context.AIInteractionLogs
                    .Where(log => sessionIds.Contains(log.InterviewSessionId))
                    .ToListAsync();
                if (aiLogs.Any()) _context.AIInteractionLogs.RemoveRange(aiLogs);

                var attempts = await _context.TechnicalQuestionAttempts
                    .Where(a => sessionIds.Contains(a.InterviewSessionId))
                    .ToListAsync();
                if (attempts.Any())
                {
                    var attemptIds = attempts.Select(a => a.AttemptId).ToList();
                    var evals = await _context.TechnicalAnswerEvaluations
                        .Where(e => attemptIds.Contains(e.AttemptId))
                        .ToListAsync();
                    if (evals.Any()) _context.TechnicalAnswerEvaluations.RemoveRange(evals);
                    _context.TechnicalQuestionAttempts.RemoveRange(attempts);
                }

                var bSets = await _context.BehaviourQuestionSets
                    .Where(b => sessionIds.Contains(b.InterviewSessionId))
                    .ToListAsync();
                if (bSets.Any())
                {
                    var bSetIds = bSets.Select(b => b.BehaviourQuestionSetId).ToList();
                    var bQuestions = await _context.BehaviourSessionQuestions
                        .Where(bq => bSetIds.Contains(bq.BehaviourQuestionSetId))
                        .ToListAsync();
                    if (bQuestions.Any())
                    {
                        var bQuestionIds = bQuestions.Select(bq => bq.BehaviourSessionQuestionId).ToList();
                        var bAnswers = await _context.BehaviourAnswers
                            .Where(ba => bQuestionIds.Contains(ba.BehaviourSessionQuestionId))
                            .ToListAsync();
                        if (bAnswers.Any()) _context.BehaviourAnswers.RemoveRange(bAnswers);
                        _context.BehaviourSessionQuestions.RemoveRange(bQuestions);
                    }
                    _context.BehaviourQuestionSets.RemoveRange(bSets);
                }

                var bResults = await _context.BehaviourRoundResults
                    .Where(br => sessionIds.Contains(br.InterviewSessionId))
                    .ToListAsync();
                if (bResults.Any()) _context.BehaviourRoundResults.RemoveRange(bResults);

                var skillScores = await _context.UserSkillScores
                    .Where(ss => (ss.InterviewSessionId.HasValue && sessionIds.Contains(ss.InterviewSessionId.Value)) || (ss.InterviewCampaignId.HasValue && campaignIds.Contains(ss.InterviewCampaignId.Value)))
                    .ToListAsync();
                if (skillScores.Any()) _context.UserSkillScores.RemoveRange(skillScores);

                _context.InterviewSessions.RemoveRange(sessions);
            }

            var campaignSkillScores = await _context.UserSkillScores
                .Where(ss => ss.InterviewCampaignId.HasValue && campaignIds.Contains(ss.InterviewCampaignId.Value))
                .ToListAsync();
            if (campaignSkillScores.Any()) _context.UserSkillScores.RemoveRange(campaignSkillScores);

            _context.InterviewCampaigns.RemoveRange(campaigns);
        }
             

        public async Task<PagedResult<JDFile>> GetAllJDAsync(JDQueryParameters query, CancellationToken cancellationToken = default)
        {
            var jdFiles = _context.JDFiles.AsNoTracking();

            if(!string.IsNullOrEmpty(query.Status) && Enum.TryParse<JDFileStatus>(query.Status, true, out var statusEnum))
            {
                jdFiles = jdFiles.Where(j => j.Status == statusEnum);
            }
            var totalItems = await jdFiles.CountAsync(cancellationToken);
            var orderedItems = ApplySorting(jdFiles, query.SortBy, query.IsAscending);
            var items = await orderedItems
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);
            return new PagedResult<JDFile>
            {
                Items = items,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                TotalItems = totalItems,
            };
        }

        public async Task<JDFile?> GetJDByIdAsync(int id)
        {
            return await _context.JDFiles.Include(j => j.User).FirstOrDefaultAsync(j => j.JDFileId == id);
        }

        public async Task<PagedResult<JDFile>> GetJDByUserIdAsync(int userId, JDQueryParameters query, CancellationToken cancellationToken = default)
        {
            var jdFiles = _context.JDFiles.AsNoTracking().Where(j => j.UserId == userId);
            if(!string.IsNullOrEmpty(query.Status) && Enum.TryParse<JDFileStatus>(query.Status, true, out var statusEnum))
            {
                jdFiles = jdFiles.Where(j => j.Status == statusEnum);
            }
            
            var totalItems = await jdFiles.CountAsync(cancellationToken);
            var orderedItems = ApplySorting(jdFiles, query.SortBy, query.IsAscending);
            var items = await orderedItems
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);
            return new PagedResult<JDFile>
            {
                Items = items,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                TotalItems = totalItems,
            };
        }

        public async Task<JDFile> UpdateJDAsync(JDFile jdFile)
        {
            _context.JDFiles.Update(jdFile);
            await _context.SaveChangesAsync();
            return jdFile;
        }
        private static IOrderedQueryable<JDFile> ApplySorting(
           IQueryable<JDFile> query,
           string sortBy,
           bool isAscending)
        {
            var property = (sortBy ?? "UploadedAt").Trim().ToLowerInvariant();
            return (property, isAscending) switch
            {

                ("userid", true) => query.OrderBy(j => j.UserId).ThenBy(j => j.JDFileId),
                ("userid", false) => query.OrderByDescending(j => j.UserId).ThenByDescending(j => j.JDFileId),
                // Mặc định sort theo UploadedAt
                (_, true) => query.OrderBy(j => j.UploadedAt).ThenBy(j => j.JDFileId),
                _ => query.OrderByDescending(j => j.UploadedAt).ThenByDescending(j => j.JDFileId),
            };
        }
    }
}
