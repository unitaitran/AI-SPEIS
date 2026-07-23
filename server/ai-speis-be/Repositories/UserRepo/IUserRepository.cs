using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Repositories.UserRepo
{
    public interface IUserRepository
    {
        Task<PagedResultDto<AdminUserListItemDto>> GetUsersAsync(
            AdminUserQueryDto query,
            CancellationToken cancellationToken = default);
        Task<AdminUserDetailDto?> GetAdminUserDetailAsync(
            int userId,
            CancellationToken cancellationToken = default);
        Task<User?> GetUserByIdAsync(
            int userId,
            CancellationToken cancellationToken = default);
        Task<User?> GetUserByEmailAsync(string email);
        Task<User?> GetUserByEmailConfirmationTokenAsync(string token);
        Task<User> CreateUserAsync(User user);
        Task UpdateUserAsync(
            User user,
            CancellationToken cancellationToken = default);
        Task<User?> GetUserByPasswordResetTokenAsync(string token);
        Task<User?> GetUserByPhoneNumberAsync(string phoneNumber);
        Task<AdminUserStatsDto> GetUserStatsAsync(CancellationToken cancellationToken = default);
    }
}
