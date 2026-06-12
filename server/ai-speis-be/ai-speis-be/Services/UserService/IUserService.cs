using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Services.UserService
{
    public interface IUserService
    {
        Task<IEnumerable<UserResponseDto>> GetUsersAsync();
        Task<User?> GetUserByEmailAsync(string email);
        Task<User> CreateUserAsync(RegisterDto registerDto);
        Task<User> CreateGoogleUserAsync(string email, string fullName);
        Task<bool> ConfirmEmailAsync(string token);
        Task<User?> ConfirmEmailFromGoogleAsync(string email);
    }
}
