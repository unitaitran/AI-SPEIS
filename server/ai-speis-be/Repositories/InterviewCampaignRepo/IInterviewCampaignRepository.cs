using System.Collections.Generic;
using System.Threading.Tasks;
using ai_speis_be.Models;

namespace ai_speis_be.Repositories.InterviewCampaignRepo
{
    public interface IInterviewCampaignRepository
    {
        Task<InterviewCampaign> AddCampaignAsync(InterviewCampaign campaign);
        Task<InterviewCampaign?> GetCampaignByIdAsync(int campaignId);
        Task<IEnumerable<InterviewCampaign>> GetCampaignsByUserIdAsync(int userId);
        Task<bool> UpdateCampaignAsync(InterviewCampaign campaign);
        Task<bool> DeleteCampaignAsync(int campaignId);
        Task<InterviewSession?> GetSessionByIdAsync(int sessionId);
        Task<bool> UpdateSessionAsync(InterviewSession session);
    }
}
