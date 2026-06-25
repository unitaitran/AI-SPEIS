using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Services.UserService
{
    public interface IUserService
    {
        Task<PagedResultDto<AdminUserListItemDto>> GetUsersAsync(
            AdminUserQueryDto query,
            CancellationToken cancellationToken = default);
        Task<AdminUserDetailDto?> GetAdminUserDetailAsync(
            int userId,
            CancellationToken cancellationToken = default);
        Task<LockUserResult> LockUserAsync(
            int userId,
            int actingUserId,
            string? reason,
            CancellationToken cancellationToken = default);
        Task<UnlockUserResult> UnlockUserAsync(
            int userId,
            CancellationToken cancellationToken = default);
        Task<User?> GetUserByEmailAsync(string email);
        Task<User> CreateUserAsync(RegisterDto registerDto);
        Task<User> CreateGoogleUserAsync(string email, string fullName);
        Task<bool> ConfirmEmailAsync(string token);
        Task<User?> ConfirmEmailFromGoogleAsync(string email);
        Task<string?> InitiatePasswordResetAsync(string email);
        Task<bool> ResetPasswordAsync(ResetPasswordDto resetPasswordDto);

        // ── Profile & Security ──────────────────────────────────────────────
        Task<UserMeResponseDto?> GetMyProfileAsync(
            int userId,
            CancellationToken cancellationToken = default);
        Task<UpdateProfileResult> UpdateMyProfileAsync(
            int userId,
            UpdateProfileRequestDto dto,
            CancellationToken cancellationToken = default);
        Task<ChangePasswordResult> ChangePasswordAsync(
            int userId,
            ChangePasswordRequestDto dto,
            CancellationToken cancellationToken = default);
    }
}
