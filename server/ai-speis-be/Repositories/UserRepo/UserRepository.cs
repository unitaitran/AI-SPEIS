using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Models.Enums;
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

            if (!string.IsNullOrWhiteSpace(query.Package))
            {
                var packageCode = query.Package.Trim().ToUpper();
                users = users.Where(user =>
                    _context.UserSubscriptions.Any(subscription =>
                        subscription.UserId == user.UserId &&
                        subscription.Plan.Code == packageCode) ||
                    (!_context.UserSubscriptions.Any(subscription => subscription.UserId == user.UserId) &&
                        ((packageCode == "PREMIUM" && user.IsPremium) ||
                         (packageCode == "FREE" && !user.IsPremium))));
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
                    IsLocked = user.IsLocked,
                    AccountStatus = user.IsLocked
                        ? UserAccountStatus.Locked
                        : user.Status
                            ? UserAccountStatus.Active
                            : UserAccountStatus.PendingActivation,
                    Package = _context.UserSubscriptions
                        .Where(subscription => subscription.UserId == user.UserId)
                        .Select(subscription => subscription.Plan.Name)
                        .FirstOrDefault() ?? (user.IsPremium ? "Premium" : "Free"),
                    Quota = user.RemainingInterviewQuota,
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

        public Task<AdminUserDetailDto?> GetAdminUserDetailAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            return (
                from user in _context.Users.AsNoTracking()
                join profile in _context.UserProfiles.AsNoTracking()
                    on user.UserId equals profile.UserId into userProfiles
                from profile in userProfiles.DefaultIfEmpty()
                where user.UserId == userId
                select new AdminUserDetailDto
                {
                    UserId = user.UserId,
                    RoleId = user.RoleId,
                    Role = user.Role.RoleName,
                    FullName = user.FullName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Status = user.Status,
                    IsLocked = user.IsLocked,
                    AccountStatus = user.IsLocked
                        ? UserAccountStatus.Locked
                        : user.Status
                            ? UserAccountStatus.Active
                            : UserAccountStatus.PendingActivation,
                    LockReason = user.LockReason,
                    LockedAt = user.LockedAt,
                    LockedByUserId = user.LockedByUserId,
                    EmailConfirmedAt = user.EmailConfirmedAt,
                    HasPassword = user.PasswordHash != null &&
                        user.PasswordHash != string.Empty,
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt,
                    ImageUrl = user.ImageUrl,
                    Package = _context.UserSubscriptions
                        .Where(subscription => subscription.UserId == user.UserId)
                        .Select(subscription => subscription.Plan.Name)
                        .FirstOrDefault() ?? (user.IsPremium ? "Premium" : "Free"),
                    Quota = user.RemainingInterviewQuota,
                    Profile = profile == null
                        ? null
                        : new AdminUserProfileDto
                        {
                            ProfileId = profile.ProfileId,
                            School = profile.School,
                            Major = profile.Major,
                            Gpa = profile.Gpa,
                            TargetPosition = profile.TargetPosition,
                            Gender = profile.Gender,
                            CreatedAt = profile.CreatedAt,
                            UpdatedAt = profile.UpdatedAt
                        },
                    CVFiles = user.CVFiles.Select(cv => new AdminUserCVFileDto
                    {
                        CVFileId = cv.CVFileId,
                        FileName = cv.FileName,
                        FilePath = cv.FilePath,
                        FileSize = cv.FileSize,
                        FileType = cv.FileType,
                        Status = cv.Status.ToString(),
                        UploadedAt = cv.UploadedAt
                    }).ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public Task<User?> GetUserByIdAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            return _context.Users
                .Include(user => user.Role)
                .FirstOrDefaultAsync(
                    user => user.UserId == userId,
                    cancellationToken);
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

        public async Task UpdateUserAsync(
            User user,
            CancellationToken cancellationToken = default)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync(cancellationToken);
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

        public async Task<User?> GetUserByPasswordResetTokenAsync(string token)
        {
            return await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.PasswordResetToken == token);
        }

        public async Task<User?> GetUserByPhoneNumberAsync(string phoneNumber)
        {
            return await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
        }
    }
}
