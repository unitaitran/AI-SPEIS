using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Repositories.UserRepo
{
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResultDto<AdminUserListItemDto>> GetUsersAsync(
            AdminUserQueryDto query,
            CancellationToken cancellationToken = default)
        {
            var users = _context.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                users = users.Where(user =>
                    user.Email.Contains(search) ||
                    user.FullName.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(query.Role))
            {
                var role = query.Role.Trim();
                users = users.Where(user => user.Role.RoleName == role);
            }

            if (query.Status.HasValue)
            {
                users = users.Where(user => user.Status == query.Status.Value);
            }

            var totalItems = await users.CountAsync(cancellationToken);
            var orderedUsers = ApplySorting(users, query.SortBy, query.SortDirection);

            var items = await orderedUsers
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(user => new AdminUserListItemDto
                {
                    UserId = user.UserId,
                    RoleId = user.RoleId,
                    Role = user.Role.RoleName,
                    FullName = user.FullName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Status = user.Status,
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt
                })
                .ToListAsync(cancellationToken);

            return new PagedResultDto<AdminUserListItemDto>
            {
                Items = items,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                TotalItems = totalItems
            };
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _context.Users
                .Include(user => user.Role)
                .FirstOrDefaultAsync(user => user.Email == email);
        }

        public async Task<User?> GetUserByEmailConfirmationTokenAsync(string token)
        {
            return await _context.Users
                .Include(user => user.Role)
                .FirstOrDefaultAsync(user => user.EmailConfirmationToken == token);
        }

        public async Task<User> CreateUserAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task UpdateUserAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        private static IOrderedQueryable<User> ApplySorting(
            IQueryable<User> users,
            AdminUserSortBy sortBy,
            SortDirection sortDirection)
        {
            var ascending = sortDirection == SortDirection.Asc;

            return (sortBy, ascending) switch
            {
                (AdminUserSortBy.FullName, true) => users
                    .OrderBy(user => user.FullName)
                    .ThenBy(user => user.UserId),
                (AdminUserSortBy.FullName, false) => users
                    .OrderByDescending(user => user.FullName)
                    .ThenByDescending(user => user.UserId),
                (AdminUserSortBy.Email, true) => users
                    .OrderBy(user => user.Email)
                    .ThenBy(user => user.UserId),
                (AdminUserSortBy.Email, false) => users
                    .OrderByDescending(user => user.Email)
                    .ThenByDescending(user => user.UserId),
                (AdminUserSortBy.Role, true) => users
                    .OrderBy(user => user.Role.RoleName)
                    .ThenBy(user => user.UserId),
                (AdminUserSortBy.Role, false) => users
                    .OrderByDescending(user => user.Role.RoleName)
                    .ThenByDescending(user => user.UserId),
                (AdminUserSortBy.Status, true) => users
                    .OrderBy(user => user.Status)
                    .ThenBy(user => user.UserId),
                (AdminUserSortBy.Status, false) => users
                    .OrderByDescending(user => user.Status)
                    .ThenByDescending(user => user.UserId),
                (_, true) => users
                    .OrderBy(user => user.CreatedAt)
                    .ThenBy(user => user.UserId),
                _ => users
                    .OrderByDescending(user => user.CreatedAt)
                    .ThenByDescending(user => user.UserId)
            };
        }
    }
}
