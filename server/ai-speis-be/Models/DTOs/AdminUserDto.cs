using System.ComponentModel.DataAnnotations;

namespace ai_speis_be.Models.DTOs
{
    public enum AdminUserSortBy
    {
        CreatedAt,
        FullName,
        Email,
        Role,
        Status
    }

    public enum SortDirection
    {
        Asc,
        Desc
    }

    public sealed class AdminUserQueryDto
    {
        [Range(1, 1_000_000)]
        public int PageNumber { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 10;

        [StringLength(200)]
        public string? Search { get; set; }

        [StringLength(100)]
        public string? Role { get; set; }

        public bool? Status { get; set; }

        public AdminUserSortBy SortBy { get; set; } = AdminUserSortBy.CreatedAt;

        public SortDirection SortDirection { get; set; } = SortDirection.Desc;
    }

    public sealed class AdminUserListItemDto
    {
        public int UserId { get; set; }
        public int RoleId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public bool Status { get; set; }
        public bool IsLocked { get; set; }
        public string AccountStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public sealed class LockUserRequestDto
    {
        [StringLength(500)]
        public string? Reason { get; set; }
    }

    public sealed class LockUserResponseDto
    {
        public int UserId { get; init; }
        public string Status { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }

    public sealed class PagedResultDto<T>
    {
        public required IReadOnlyList<T> Items { get; init; }
        public int PageNumber { get; init; }
        public int PageSize { get; init; }
        public int TotalItems { get; init; }
        public int TotalPages => TotalItems == 0
            ? 0
            : (int)Math.Ceiling(TotalItems / (double)PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }
}
