using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ai_speis_be.Models;
using ai_speis_be.Services.CodingService.Rubrics;
using ai_speis_be.TechnicalInterviews.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ai_speis_be.Services.CodingService.Selection
{
    public class CodingQuestionSelectionService : ICodingQuestionSelectionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CodingQuestionSelectionService> _logger;

        public CodingQuestionSelectionService(
            ApplicationDbContext context,
            ILogger<CodingQuestionSelectionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<CodingQuestion>> SelectCodingQuestionsAsync(
            InterviewSession session,
            CancellationToken cancellationToken = default)
        {
            var campaign = session.InterviewCampaign;
            var matchScore = campaign?.CvJdMatchScore;

            // 1. Load Rubric & Determine total question count (dynamically from session setup, default 3)
            var rubric = CodingRubricDefinition.LoadDefault();
            var band = rubric.GetBand(matchScore);
            int totalQuestionCount = session.QuestionCount > 0 ? session.QuestionCount : 3;
            var (cvQuota, jdQuota) = band.GetQuestionCounts(totalQuestionCount);

            _logger.LogInformation(
                "Coding Question Selection cho Session {SessionId}: MatchScore = {Score}, Band = {BandCode} ({BandName}), Total = {TotalCount}, Target CV = {CvCount}, Target JD = {JdCount}",
                session.InterviewSessionId, matchScore, band.Code, band.Name, totalQuestionCount, cvQuota, jdQuota);

            // 2. Extract Context (JobRole, CV Skills, JD Skills)
            var jobRole = ExtractJobRole(campaign);
            var cvSkills = ExtractCvSkills(campaign);
            var jdSkills = ExtractJdSkills(campaign);

            // 3. Fetch all active candidate questions from DB
            var allQuestions = await _context.CodingQuestions
                .Include(q => q.CodingQuestionTemplates)
                .Include(q => q.TestCases)
                .Where(q => !q.IsDeleted && q.IsActive)
                .ToListAsync(cancellationToken);

            if (allQuestions.Count == 0)
            {
                _logger.LogWarning("Ngân hàng câu hỏi Coding rỗng!");
                return new List<CodingQuestion>();
            }

            // Fetch question IDs used in previous sessions for this user to avoid duplicate selections across consecutive rounds
            var userId = campaign?.UserId ?? 0;
            var previousQuestionIds = new HashSet<int>();
            if (userId > 0)
            {
                var prevQIds = await _context.CodingSubmissions
                    .Where(cs => cs.InterviewSession.InterviewCampaign.UserId == userId && cs.InterviewSessionId != session.InterviewSessionId)
                    .Select(cs => cs.CodingQuestionId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                foreach (var id in prevQIds)
                {
                    previousQuestionIds.Add(id);
                }
            }

            // 4. Filter by JobRole (matching JobRole column or text inside EmbeddingText / Title / Description)
            var roleFiltered = FilterByJobRole(allQuestions, jobRole);

            // 5. Filter by Difficulty Band
            var difficultyFiltered = roleFiltered
                .Where(q => band.AllowedDifficulties.Any(d => string.Equals(d, q.Difficulty, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // Fallback: if difficulty filter reduces candidates below target, keep roleFiltered
            var candidatePool = difficultyFiltered.Count >= totalQuestionCount
                ? difficultyFiltered
                : roleFiltered;

            if (candidatePool.Count == 0)
            {
                candidatePool = allQuestions;
            }

            // 6. Select Questions according to Rubric split (CV focus vs JD focus)
            var selected = new List<CodingQuestion>();
            var usedIds = new HashSet<int>();

            // Phase A: Prioritize questions NOT used in previous sessions of the user
            var unusedCandidatePool = candidatePool
                .Where(q => !previousQuestionIds.Contains(q.CodingQuestionId))
                .ToList();

            // Select CV-focused questions (unused pool first)
            var cvSelectedCount = 0;
            foreach (var q in unusedCandidatePool)
            {
                if (cvSelectedCount >= cvQuota) break;
                if (usedIds.Contains(q.CodingQuestionId)) continue;

                if (MatchesSkills(q, cvSkills))
                {
                    selected.Add(q);
                    usedIds.Add(q.CodingQuestionId);
                    cvSelectedCount++;
                }
            }

            // Fallback to full candidatePool for CV quota if unused pool runs low
            if (cvSelectedCount < cvQuota)
            {
                foreach (var q in candidatePool)
                {
                    if (cvSelectedCount >= cvQuota) break;
                    if (usedIds.Contains(q.CodingQuestionId)) continue;

                    if (MatchesSkills(q, cvSkills))
                    {
                        selected.Add(q);
                        usedIds.Add(q.CodingQuestionId);
                        cvSelectedCount++;
                    }
                }
            }

            // Select JD-focused questions (unused pool first)
            var jdSelectedCount = 0;
            foreach (var q in unusedCandidatePool)
            {
                if (jdSelectedCount >= jdQuota) break;
                if (usedIds.Contains(q.CodingQuestionId)) continue;

                if (MatchesSkills(q, jdSkills))
                {
                    selected.Add(q);
                    usedIds.Add(q.CodingQuestionId);
                    jdSelectedCount++;
                }
            }

            // Fallback to full candidatePool for JD quota if unused pool runs low
            if (jdSelectedCount < jdQuota)
            {
                foreach (var q in candidatePool)
                {
                    if (jdSelectedCount >= jdQuota) break;
                    if (usedIds.Contains(q.CodingQuestionId)) continue;

                    if (MatchesSkills(q, jdSkills))
                    {
                        selected.Add(q);
                        usedIds.Add(q.CodingQuestionId);
                        jdSelectedCount++;
                    }
                }
            }

            // Fallback fill: unused candidatePool first, then full candidatePool
            var targetTotal = Math.Min(totalQuestionCount, candidatePool.Count);
            foreach (var q in unusedCandidatePool)
            {
                if (selected.Count >= targetTotal) break;
                if (!usedIds.Contains(q.CodingQuestionId))
                {
                    selected.Add(q);
                    usedIds.Add(q.CodingQuestionId);
                }
            }

            foreach (var q in candidatePool)
            {
                if (selected.Count >= targetTotal) break;
                if (!usedIds.Contains(q.CodingQuestionId))
                {
                    selected.Add(q);
                    usedIds.Add(q.CodingQuestionId);
                }
            }

            // Final fallback from allQuestions if still needed
            if (selected.Count < totalQuestionCount)
            {
                var unusedAll = allQuestions.Where(q => !previousQuestionIds.Contains(q.CodingQuestionId)).ToList();
                foreach (var q in unusedAll)
                {
                    if (selected.Count >= totalQuestionCount) break;
                    if (!usedIds.Contains(q.CodingQuestionId))
                    {
                        selected.Add(q);
                        usedIds.Add(q.CodingQuestionId);
                    }
                }

                foreach (var q in allQuestions)
                {
                    if (selected.Count >= totalQuestionCount) break;
                    if (!usedIds.Contains(q.CodingQuestionId))
                    {
                        selected.Add(q);
                        usedIds.Add(q.CodingQuestionId);
                    }
                }
            }

            // 7. Sort by Difficulty (Easy -> Medium -> Hard)
            var difficultyOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "Easy", 1 },
                { "Medium", 2 },
                { "Hard", 3 }
            };

            return selected
                .OrderBy(q => difficultyOrder.GetValueOrDefault(q.Difficulty ?? "Medium", 2))
                .ToList();
        }

        private static string ExtractJobRole(InterviewCampaign? campaign)
        {
            if (campaign == null) return string.Empty;

            if (!string.IsNullOrWhiteSpace(campaign.JDExtractedProfile?.RoleTarget))
                return campaign.JDExtractedProfile.RoleTarget;

            if (!string.IsNullOrWhiteSpace(campaign.JDExtractedProfile?.JobTitle))
                return campaign.JDExtractedProfile.JobTitle;

            if (!string.IsNullOrWhiteSpace(campaign.CVExtractedProfile?.RoleTarget))
                return campaign.CVExtractedProfile.RoleTarget;

            return string.Empty;
        }

        private static List<string> ExtractCvSkills(InterviewCampaign? campaign)
        {
            if (campaign?.CVExtractedProfile?.Skills == null)
                return new List<string>();

            return campaign.CVExtractedProfile.Skills
                .Select(s => s.SkillName)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        private static List<string> ExtractJdSkills(InterviewCampaign? campaign)
        {
            var skills = new List<string>();
            if (campaign?.JDExtractedProfile == null) return skills;

            var req = ParseStringArray(campaign.JDExtractedProfile.RequiredSkills);
            var nice = ParseStringArray(campaign.JDExtractedProfile.NiceToHaveSkills);

            skills.AddRange(req);
            skills.AddRange(nice);
            return skills.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static List<string> ParseStringArray(string? jsonOrCsv)
        {
            if (string.IsNullOrWhiteSpace(jsonOrCsv)) return new List<string>();

            try
            {
                var trimmed = jsonOrCsv.Trim();
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(trimmed);
                    if (list != null) return list;
                }
            }
            catch
            {
            }

            return jsonOrCsv.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        private static List<CodingQuestion> FilterByJobRole(List<CodingQuestion> questions, string jobRole)
        {
            if (string.IsNullOrWhiteSpace(jobRole)) return questions;

            var lowerRole = jobRole.ToLowerInvariant();
            var keywords = lowerRole.Split(new[] { ' ', '/', '-' }, StringSplitOptions.RemoveEmptyEntries);

            var matches = questions.Where(q =>
            {
                if (!string.IsNullOrWhiteSpace(q.JobRole) && q.JobRole.ToLowerInvariant().Contains(lowerRole))
                    return true;

                if (!string.IsNullOrWhiteSpace(q.EmbeddingText) && q.EmbeddingText.ToLowerInvariant().Contains(lowerRole))
                    return true;

                // Match individual keywords e.g. "Backend" in "Job role: Backend Developer"
                if (keywords.Any(kw => kw.Length > 2 && (
                    (!string.IsNullOrWhiteSpace(q.JobRole) && q.JobRole.ToLowerInvariant().Contains(kw)) ||
                    (!string.IsNullOrWhiteSpace(q.EmbeddingText) && q.EmbeddingText.ToLowerInvariant().Contains(kw))
                )))
                {
                    return true;
                }

                return false;
            }).ToList();

            // If matching produces results, return them; otherwise fallback to full set
            return matches.Count > 0 ? matches : questions;
        }

        private static bool MatchesSkills(CodingQuestion q, List<string> skills)
        {
            if (skills.Count == 0) return false;

            var textToSearch = string.Join(" ",
                q.Skill ?? "",
                q.Subskill ?? "",
                q.Title ?? "",
                q.EmbeddingText ?? "",
                q.Keywords ?? "").ToLowerInvariant();

            return skills.Any(skill =>
            {
                if (string.IsNullOrWhiteSpace(skill)) return false;
                var sLower = skill.ToLowerInvariant().Trim();
                return textToSearch.Contains(sLower);
            });
        }
    }
}
