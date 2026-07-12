using System.Collections.Generic;
using System.Threading.Tasks;
using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Services.InterviewSessionService
{
    public interface IInterviewSessionService
    {
        Task<(bool Success, string? ErrorMessage, InterviewCampaignDto? Campaign)> CreateSessionsAsync(int userId, CreateInterviewSessionRequest request);
        Task<(bool Success, string? ErrorMessage, InterviewCampaignDto? Campaign)> StartSessionAsync(int userId, int sessionId);
        Task<(bool Success, string? ErrorMessage, InterviewCampaignDto? Campaign)> CompleteSessionAsync(int userId, int sessionId);
        Task<(bool Success, string? ErrorMessage, InterviewCampaignDto? Campaign)> CancelCampaignAsync(int userId, int campaignId);
        Task<(bool Success, string? ErrorMessage, InterviewCampaignDto? Campaign)> ExpireCampaignAsync(int userId, int campaignId);
        Task<InterviewQuotaDto?> GetQuotaAsync(int userId);
        Task<InterviewSessionDto?> GetSessionByIdAsync(int userId, int sessionId);
        Task<InterviewCampaignDto?> GetCampaignByIdAsync(int userId, int campaignId);
        Task<IEnumerable<InterviewCampaignDto>> GetUserCampaignsAsync(int userId);
        Task<AvailableRoundsDto?> GetAvailableRoundsAsync(int userId, int jdId);
    }
}
