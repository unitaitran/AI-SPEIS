using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ai_speis_be.Models.Enums;

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
        [Range(1, 1_000_000, ErrorMessage = "Số trang phải từ 1 đến 1000000.")]
        public int PageNumber { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "Kích thước trang phải từ 1 đến 100.")]
        public int PageSize { get; set; } = 10;

        [StringLength(200, ErrorMessage = "Từ khóa tìm kiếm không được vượt quá 200 ký tự.")]
        public string? Search { get; set; }

        [StringLength(100, ErrorMessage = "Vai trò không được vượt quá 100 ký tự.")]
        public string? Role { get; set; }

        public bool? Status { get; set; }

        /// <summary>Filter by subscription package: "premium" or "free".</summary>
        [StringLength(50)]
        public string? Package { get; set; }

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
        public bool IsPremium { get; set; }
        public string Package { get; set; } = string.Empty;
        public int RemainingInterviewQuota { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public sealed class AdminUserDetailDto
    {
        public int UserId { get; init; }
        public int RoleId { get; init; }
        public string Role { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string? PhoneNumber { get; init; }
        public bool Status { get; init; }
        public bool IsLocked { get; init; }
        public string AccountStatus { get; init; } = string.Empty;
        public string? LockReason { get; init; }
        public DateTime? LockedAt { get; init; }
        public int? LockedByUserId { get; init; }
        public DateTime? EmailConfirmedAt { get; init; }
        public bool HasPassword { get; init; }
        public bool IsPremium { get; init; }
        public string Package { get; init; } = string.Empty;
        public int RemainingInterviewQuota { get; init; }
        public DateTime? PremiumExpireAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
        public string? ImageUrl { get; init; }
        public AdminUserProfileDto? Profile { get; init; }
        public IReadOnlyList<AdminUserCVFileDto> CVFiles { get; init; } = Array.Empty<AdminUserCVFileDto>();
    }

    public sealed class AdminUserStatsDto
    {
        public int TotalUsers { get; init; }
        public int PremiumUsers { get; init; }
        public int FreeUsers { get; init; }
        public int ActiveUsers { get; init; }
        public int LockedUsers { get; init; }
    }

    public sealed class AdminUserCVFileDto
    {
        public int CVFileId { get; init; }
        public string FileName { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public long FileSize { get; init; }
        public string FileType { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public DateTime UploadedAt { get; init; }
    }

    public sealed class AdminUserProfileDto
    {
        public int ProfileId { get; init; }
        public string School { get; init; } = string.Empty;
        public string Major { get; init; } = string.Empty;
        public decimal Gpa { get; init; }
        public string TargetPosition { get; init; } = string.Empty;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Gender Gender { get; init; }

        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }

    public sealed class LockUserRequestDto
    {
        [StringLength(500, ErrorMessage = "Lý do khóa không được vượt quá 500 ký tự.")]
        public string? Reason { get; set; }
    }

    public sealed class LockUserResponseDto
    {
        public int UserId { get; init; }
        public string Status { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }

    public sealed class UnlockUserResponseDto
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

    public sealed class UpdateUserRoleRequestDto
    {
        [Required]
        public string RoleName { get; set; } = string.Empty;
    }
}
