using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ai_speis_be.Models;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Repositories.InterviewCampaignRepo
{
    public class InterviewCampaignRepository : IInterviewCampaignRepository
    {
        private readonly ApplicationDbContext _context;

        public InterviewCampaignRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<InterviewCampaign> AddCampaignAsync(InterviewCampaign campaign)
        {
            _context.InterviewCampaigns.Add(campaign);
            await _context.SaveChangesAsync();
            return campaign;
        }

        public async Task<InterviewCampaign?> GetCampaignByIdAsync(int campaignId)
        {
            return await _context.InterviewCampaigns
                .Include(c => c.CVExtractedProfile)
                .Include(c => c.JDExtractedProfile)
                .Include(c => c.InterviewSessions.Where(s => !s.IsDeleted))
                .FirstOrDefaultAsync(c => c.InterviewCampaignId == campaignId && !c.IsDeleted);
        }

        public async Task<IEnumerable<InterviewCampaign>> GetCampaignsByUserIdAsync(int userId)
        {
            return await _context.InterviewCampaigns
                .Include(c => c.CVExtractedProfile)
                .Include(c => c.JDExtractedProfile)
                .Include(c => c.InterviewSessions.Where(s => !s.IsDeleted))
                .Where(c => c.UserId == userId && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> UpdateCampaignAsync(InterviewCampaign campaign)
        {
            _context.InterviewCampaigns.Update(campaign);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteCampaignAsync(int campaignId)
        {
            var campaign = await _context.InterviewCampaigns
                .Include(c => c.InterviewSessions)
                .FirstOrDefaultAsync(c => c.InterviewCampaignId == campaignId && !c.IsDeleted);
            if (campaign == null) return false;

            campaign.IsDeleted = true;
            campaign.DeletedAt = System.DateTime.UtcNow;

            foreach (var session in campaign.InterviewSessions)
            {
                session.IsDeleted = true;
                session.DeletedAt = System.DateTime.UtcNow;
            }

            _context.InterviewCampaigns.Update(campaign);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<InterviewSession?> GetSessionByIdAsync(int sessionId)
        {
            return await _context.InterviewSessions
                .Include(s => s.InterviewCampaign)
                    .ThenInclude(c => c.CVExtractedProfile)
                .Include(s => s.InterviewCampaign)
                    .ThenInclude(c => c.JDExtractedProfile)
                .FirstOrDefaultAsync(s => s.InterviewSessionId == sessionId && !s.IsDeleted && !s.InterviewCampaign.IsDeleted);
        }

        public async Task<bool> UpdateSessionAsync(InterviewSession session)
        {
            _context.InterviewSessions.Update(session);
            return await _context.SaveChangesAsync() > 0;
        }
    }
}
