using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Services.UserService
{
    public interface IUserService
    {
        Task<PagedResultDto<AdminUserListItemDto>> GetUsersAsync(
            AdminUserQueryDto query,
            CancellationToken cancellationToken = default);
        Task<LockUserResult> LockUserAsync(
            int userId,
            int actingUserId,
            string? reason,
            CancellationToken cancellationToken = default);
        Task<User?> GetUserByEmailAsync(string email);
        Task<User> CreateUserAsync(RegisterDto registerDto);
        Task<User> CreateGoogleUserAsync(string email, string fullName);
        Task<bool> ConfirmEmailAsync(string token);
        Task<User?> ConfirmEmailFromGoogleAsync(string email);
    }
}
