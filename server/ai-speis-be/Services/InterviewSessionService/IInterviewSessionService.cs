using System.Collections.Generic;
using System.Threading.Tasks;
using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Services.InterviewSessionService
{
    public interface IInterviewSessionService
    {
        Task<(bool Success, string? ErrorMessage, InterviewCampaignDto? Campaign)> CreateSessionsAsync(int userId, CreateInterviewSessionRequest request);
        Task<(bool Success, string? ErrorMessage, InterviewSessionDto? Session)> StartSessionAsync(int userId, int sessionId);
        Task<InterviewSessionDto?> GetSessionByIdAsync(int userId, int sessionId);
        Task<InterviewCampaignDto?> GetCampaignByIdAsync(int userId, int campaignId);
        Task<IEnumerable<InterviewCampaignDto>> GetUserCampaignsAsync(int userId);
        Task<AvailableRoundsDto?> GetAvailableRoundsAsync(int userId, int jdId);
    }
}
