using ai_speis_be.Models;

namespace ai_speis_be.Repositories.UserRepo
{
    public interface IUserRepository
    {
        Task<IEnumerable<User>> GetUsersAsync();
        Task<User?> GetUserByEmailAsync(string email);
        Task<User> CreateUserAsync(User user);
    }
}
