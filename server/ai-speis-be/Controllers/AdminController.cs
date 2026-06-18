using ai_speis_be.Models.DTOs;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.QuestionService;
using ai_speis_be.Services.UserService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ai_speis_be.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "admin,Admin")]
    public sealed class AdminController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IQuestionService _questionService;

        public AdminController(
            IUserService userService,
            IQuestionService questionService)
        {
            _userService = userService;
            _questionService = questionService;
        }

        [HttpGet("users")]
        [ProducesResponseType(
            typeof(PagedResultDto<AdminUserListItemDto>),
            StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResultDto<AdminUserListItemDto>>> GetUsers(
            [FromQuery] AdminUserQueryDto query,
            CancellationToken cancellationToken)
        {
            var result = await _userService.GetUsersAsync(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("questions")]
        [ProducesResponseType(
            typeof(PagedResultDto<AdminQuestionListItemDto>),
            StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResultDto<AdminQuestionListItemDto>>> GetQuestions(
            [FromQuery] AdminQuestionQueryDto query,
            CancellationToken cancellationToken)
        {
            var result = await _questionService.GetAdminQuestionsAsync(
                query,
                cancellationToken);

            return Ok(result);
        }

        [HttpPatch("users/{userId:int}/lock")]
        [ProducesResponseType(
            typeof(LockUserResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status403Forbidden)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LockUserResponseDto>> LockUser(
            int userId,
            [FromBody] LockUserRequestDto request,
            CancellationToken cancellationToken)
        {
            var actingUserIdClaim = User.FindFirstValue("UserId");
            if (!int.TryParse(actingUserIdClaim, out var actingUserId))
            {
                return Unauthorized(new ProblemDetails
                {
                    Title = "Invalid authentication token",
                    Detail = "The authenticated user identifier is missing or invalid.",
                    Status = StatusCodes.Status401Unauthorized
                });
            }

            var result = await _userService.LockUserAsync(
                userId,
                actingUserId,
                request.Reason,
                cancellationToken);

            return result.Outcome switch
            {
                LockUserOutcome.Locked => Ok(CreateLockResponse(
                    userId,
                    "User account has been locked successfully.")),
                LockUserOutcome.AlreadyLocked => Ok(CreateLockResponse(
                    userId,
                    "User account is already locked.")),
                LockUserOutcome.UserNotFound => NotFound(new ProblemDetails
                {
                    Title = "User not found",
                    Detail = $"User with ID {userId} does not exist.",
                    Status = StatusCodes.Status404NotFound
                }),
                LockUserOutcome.CannotLockSelf => StatusCode(
                    StatusCodes.Status403Forbidden,
                    new ProblemDetails
                    {
                        Title = "Self-lock is not allowed",
                        Detail = "Administrators cannot lock their own account.",
                        Status = StatusCodes.Status403Forbidden
                    }),
                LockUserOutcome.ProtectedRole => StatusCode(
                    StatusCodes.Status403Forbidden,
                    new ProblemDetails
                    {
                        Title = "Protected account",
                        Detail = "This account has a higher administrative role and cannot be locked.",
                        Status = StatusCodes.Status403Forbidden
                    }),
                _ => throw new InvalidOperationException(
                    $"Unsupported lock user outcome: {result.Outcome}")
            };
        }

        [HttpPatch("users/{userId:int}/unlock")]
        [ProducesResponseType(
            typeof(UnlockUserResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UnlockUserResponseDto>> UnlockUser(
            int userId,
            CancellationToken cancellationToken)
        {
            var result = await _userService.UnlockUserAsync(
                userId,
                cancellationToken);

            return result.Outcome switch
            {
                UnlockUserOutcome.Unlocked => Ok(CreateUnlockResponse(
                    userId,
                    "User account has been unlocked successfully.")),
                UnlockUserOutcome.UserNotFound => NotFound(new ProblemDetails
                {
                    Title = "User not found",
                    Detail = $"User with ID {userId} does not exist.",
                    Status = StatusCodes.Status404NotFound
                }),
                UnlockUserOutcome.NotLocked => BadRequest(new ProblemDetails
                {
                    Title = "User account is not locked",
                    Detail = $"User with ID {userId} is not in LOCKED status.",
                    Status = StatusCodes.Status400BadRequest
                }),
                _ => throw new InvalidOperationException(
                    $"Unsupported unlock user outcome: {result.Outcome}")
            };
        }

        private static LockUserResponseDto CreateLockResponse(
            int userId,
            string message)
        {
            return new LockUserResponseDto
            {
                UserId = userId,
                Status = UserAccountStatus.Locked,
                Message = message
            };
        }

        private static UnlockUserResponseDto CreateUnlockResponse(
            int userId,
            string message)
        {
            return new UnlockUserResponseDto
            {
                UserId = userId,
                Status = UserAccountStatus.Active,
                Message = message
            };
        }
    }
}
