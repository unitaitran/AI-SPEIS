using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ai_speis_be.Repositories.CVRepo
{
    public class CVRepository : ICVRepository 
    {   
        private readonly ApplicationDbContext _context;
        public CVRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        
        public async Task<PagedResult<CVFile>> GetAllCVAsync(CVQueryParameters query, CancellationToken cancellationToken = default)
        {
            var CVFiles = _context.CVFiles.AsQueryable();
            if(!string.IsNullOrEmpty(query.Status) && Enum.TryParse<CVFileStatus>(query.Status, true, out var statusEnum))
            {
                CVFiles = CVFiles.Where(c => c.Status == statusEnum);
            }
            var totalItems =await CVFiles.CountAsync(cancellationToken);
            var orderdCVs = ApplySorting(CVFiles, query.SortBy, query.IsAscending);
            var items = await orderdCVs
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);
            return new PagedResult<CVFile>
            {
                Items = items,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                TotalItems = totalItems
            };


            
        }

        public async Task<CVFile?> GetCVByIdAsync(int id)
        {
            return await _context.CVFiles.Include(c => c.User).FirstOrDefaultAsync(c => c.CVFileId == id && c.Status != CVFileStatus.Archived);
        }

        public async Task<PagedResult<CVFile>> GetCVByUserIdAsync(int userId,CVQueryParameters query, CancellationToken cancellationToken = default)
        {
            var cvFiles = _context.CVFiles.AsNoTracking().Where(c => c.UserId == userId);
            if(!string.IsNullOrEmpty(query.Status) && Enum.TryParse<CVFileStatus>(query.Status, true, out var statusEnum))
            {
                cvFiles = cvFiles.Where(c => c.Status == statusEnum);
            }
            var totalItems = await cvFiles.CountAsync(cancellationToken);
            var orderedItems = ApplySorting(cvFiles, query.SortBy, query.IsAscending);
            var items = await orderedItems
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);
            return new PagedResult<CVFile>
            {
                Items = items,  
                TotalItems = totalItems,    
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
            };

         
        }

        public async Task<CVFile> AddCVAsync(CVFile cvFile)
        {
            await _context.CVFiles.AddAsync(cvFile);
            await _context.SaveChangesAsync();
            return cvFile;
        }

        public async Task<bool> DeleteCVAsync(int id)
        {
            var cvFile = await _context.CVFiles
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.CVFileId == id);
            if (cvFile == null) return false;

            var profile = await _context.CVExtractedProfiles
                .Include(p => p.Skills)
                .Include(p => p.Projects)
                .FirstOrDefaultAsync(p => p.CVFileId == id);

            if (profile != null)
            {
                var campaigns = await _context.InterviewCampaigns
                    .Where(c => c.CVExtractedProfileId == profile.ExtractedProfileId)
                    .ToListAsync();

                if (campaigns.Any())
                {
                    await DeleteCampaignsAndDependenciesAsync(campaigns);
                }

                _context.CVSkills.RemoveRange(profile.Skills);
                _context.CVProjects.RemoveRange(profile.Projects);
                _context.CVExtractedProfiles.Remove(profile);
            }

            var fastCheckResults = await _context.FastCheckResults
                .Where(f => f.CVFileId == id)
                .ToListAsync();
            if (fastCheckResults.Any())
            {
                _context.FastCheckResults.RemoveRange(fastCheckResults);
            }

            _context.CVFiles.Remove(cvFile);

            if (!string.IsNullOrEmpty(cvFile.FilePath))
            {
                try
                {
                    var absolutePath = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        cvFile.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                    if (File.Exists(absolutePath))
                    {
                        File.Delete(absolutePath);
                    }
                }
                catch
                {
                    // File deletion failure is non-critical; DB removal still proceeds.
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<CVFile?> GetActiveCVByUserIdAsync(int userId)
        {
            return await _context.CVFiles
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Status != CVFileStatus.Archived);
        }

        public async Task ArchiveAllActiveCVsByUserIdAsync(int userId)
        {
            var activeCVs = await _context.CVFiles
                .Where(c => c.UserId == userId && c.Status != CVFileStatus.Archived)
                .ToListAsync();

            foreach (var cv in activeCVs)
            {
                cv.Status = CVFileStatus.Archived;
                cv.UpdatedAt = DateTime.Now;
            }

            if (activeCVs.Count > 0)
                await _context.SaveChangesAsync();
        }

        public async Task<CVFile> UpdateCVAsync(CVFile cvFile)
        {
            _context.CVFiles.Update(cvFile);
            await _context.SaveChangesAsync();
            return cvFile;
        }
        private static IOrderedQueryable<CVFile> ApplySorting(
            IQueryable<CVFile> query,
            string sortBy,
            bool isAscending)
        {
            var property = (sortBy ?? "UploadedAt").Trim().ToLowerInvariant();
            return (property, isAscending) switch
            {
                
                ("userid", true) => query.OrderBy(c => c.UserId).ThenBy(c => c.CVFileId),
                ("userid", false) => query.OrderByDescending(c => c.UserId).ThenByDescending(c => c.CVFileId),
                // Mặc định sort theo UploadedAt
                (_, true) => query.OrderBy(c => c.UploadedAt).ThenBy(c => c.CVFileId),
                _ => query.OrderByDescending(c => c.UploadedAt).ThenByDescending(c => c.CVFileId)
            };
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
    } 
   
}