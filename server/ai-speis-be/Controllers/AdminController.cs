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

        [HttpGet("users/{userId:int}")]
        [ProducesResponseType(
            typeof(AdminUserDetailDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status404NotFound)]
        public async Task<ActionResult<AdminUserDetailDto>> GetUserDetail(
            int userId,
            CancellationToken cancellationToken)
        {
           if (userId <= 0)
                return BadRequest(new { title = "ID không hợp lệ", detail = "User ID phải là số nguyên dương." });

            var user = await _userService.GetAdminUserDetailAsync(
                userId,
                cancellationToken);

            if (user is null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Không tìm thấy user",
                    Detail = $"Không tìm thấy user với ID {userId}.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(user);
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

        [HttpPost("questions")]
        [ProducesResponseType(
            typeof(QuestionResponseDto),
            StatusCodes.Status201Created)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<QuestionResponseDto>> CreateQuestion(
            [FromBody] AdminQuestionCreateRequestDto request,
            CancellationToken cancellationToken)
        {
            if (!TryGetActingUserId(out var actingUserId))
            {
                return Unauthorized(CreateInvalidAuthenticationProblem());
            }

            var result = await _questionService.CreateAdminQuestionAsync(
                request,
                actingUserId,
                cancellationToken);

            if (result.Outcome != QuestionOperationOutcome.Created ||
                result.Question is null)
            {
                throw new InvalidOperationException(
                    $"Kết quả tạo câu hỏi không được hỗ trợ: {result.Outcome}");
            }

            return Created(
                $"/api/admin/questions/{result.Question.QuestionId}",
                result.Question);
        }

        [HttpPost("questions/import")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(
            typeof(QuestionImportSummaryDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<QuestionImportSummaryDto>> ImportQuestions(
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            if (!TryGetActingUserId(out var actingUserId))
            {
                return Unauthorized(CreateInvalidAuthenticationProblem());
            }

            var result = await _questionService.ImportAdminQuestionsAsync(
                file,
                actingUserId,
                cancellationToken);

            return result.Outcome switch
            {
                QuestionImportOutcome.Imported => Ok(result.Summary),
                QuestionImportOutcome.InvalidFile => BadRequest(new ProblemDetails
                {
                    Title = "File Excel import không hợp lệ",
                    Detail = result.ErrorMessage,
                    Status = StatusCodes.Status400BadRequest
                }),
                _ => throw new InvalidOperationException(
                    $"Kết quả import câu hỏi không được hỗ trợ: {result.Outcome}")
            };
        }

        [HttpPut("questions/{questionId:int}")]
        [ProducesResponseType(
            typeof(QuestionResponseDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status404NotFound)]
        public async Task<ActionResult<QuestionResponseDto>> UpdateQuestion(
            int questionId,
            [FromBody] AdminQuestionUpdateRequestDto request,
            CancellationToken cancellationToken)
        {
            if (questionId <= 0)
             return BadRequest(new { title = "ID không hợp lệ", detail = "Question ID phải là số nguyên dương." });
            if (!TryGetActingUserId(out var actingUserId))
            {
                return Unauthorized(CreateInvalidAuthenticationProblem());
            }

            var result = await _questionService.UpdateAdminQuestionAsync(
                questionId,
                request,
                actingUserId,
                cancellationToken);

            return result.Outcome switch
            {
                QuestionOperationOutcome.Updated => Ok(result.Question),
                QuestionOperationOutcome.QuestionNotFound => NotFound(
                    CreateQuestionNotFoundProblem(questionId)),
                QuestionOperationOutcome.QuestionDeleted => BadRequest(
                    CreateQuestionDeletedProblem(questionId)),
                _ => throw new InvalidOperationException(
                    $"Kết quả cập nhật câu hỏi không được hỗ trợ: {result.Outcome}")
            };
        }

        [HttpDelete("questions/{questionId:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteQuestion(
            int questionId,
            CancellationToken cancellationToken)
        {
            if (questionId <= 0)
                return BadRequest(new { title = "ID không hợp lệ", detail = "Question ID phải là số nguyên dương." });
            if (!TryGetActingUserId(out var actingUserId))
            {
                return Unauthorized(CreateInvalidAuthenticationProblem());
            }

            var result = await _questionService.SoftDeleteAdminQuestionAsync(
                questionId,
                actingUserId,
                cancellationToken);

            return result.Outcome switch
            {
                QuestionOperationOutcome.Deleted => NoContent(),
                QuestionOperationOutcome.AlreadyDeleted => NoContent(),
                QuestionOperationOutcome.QuestionNotFound => NotFound(
                    CreateQuestionNotFoundProblem(questionId)),
                _ => throw new InvalidOperationException(
                    $"Kết quả xóa câu hỏi không được hỗ trợ: {result.Outcome}")
            };
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
            if (userId <= 0)
                return BadRequest(new { title = "ID không hợp lệ", detail = "User ID phải là số nguyên dương." });
            if (!TryGetActingUserId(out var actingUserId))
            {
                return Unauthorized(CreateInvalidAuthenticationProblem());
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
                    "Khóa tài khoản người dùng thành công.")),
                LockUserOutcome.AlreadyLocked => Ok(CreateLockResponse(
                    userId,
                    "Tài khoản người dùng đã bị khóa.")),
                LockUserOutcome.UserNotFound => NotFound(new ProblemDetails
                {
                    Title = "Không tìm thấy người dùng",
                    Detail = $"Không tìm thấy người dùng với ID {userId}.",
                    Status = StatusCodes.Status404NotFound
                }),
                LockUserOutcome.CannotLockSelf => StatusCode(
                    StatusCodes.Status403Forbidden,
                    new ProblemDetails
                    {
                        Title = "Không thể tự khóa tài khoản",
                        Detail = "Quản trị viên không thể khóa tài khoản của chính mình.",
                        Status = StatusCodes.Status403Forbidden
                    }),
                LockUserOutcome.ProtectedRole => StatusCode(
                    StatusCodes.Status403Forbidden,
                    new ProblemDetails
                    {
                        Title = "Tài khoản được bảo vệ",
                        Detail = "Tài khoản này có quyền quản trị cao hơn nên không thể bị khóa.",
                        Status = StatusCodes.Status403Forbidden
                    }),
                _ => throw new InvalidOperationException(
                    $"Kết quả khóa người dùng không được hỗ trợ: {result.Outcome}")
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
            if (userId <= 0)
                return BadRequest(new { title = "ID không hợp lệ", detail = "User ID phải là số nguyên dương." });
            var result = await _userService.UnlockUserAsync(
                userId,
                cancellationToken);

            return result.Outcome switch
            {
                UnlockUserOutcome.Unlocked => Ok(CreateUnlockResponse(
                    userId,
                    "Mở khóa tài khoản người dùng thành công.")),
                UnlockUserOutcome.UserNotFound => NotFound(new ProblemDetails
                {
                    Title = "Không tìm thấy người dùng",
                    Detail = $"Không tìm thấy người dùng với ID {userId}.",
                    Status = StatusCodes.Status404NotFound
                }),
                UnlockUserOutcome.NotLocked => BadRequest(new ProblemDetails
                {
                    Title = "Tài khoản người dùng chưa bị khóa",
                    Detail = $"Người dùng với ID {userId} không ở trạng thái bị khóa.",
                    Status = StatusCodes.Status400BadRequest
                }),
                _ => throw new InvalidOperationException(
                    $"Kết quả mở khóa người dùng không được hỗ trợ: {result.Outcome}")
            };
        }

        [HttpPatch("users/{userId:int}/role")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateUserRole(
            int userId,
            [FromBody] UpdateUserRoleRequestDto request,
            CancellationToken cancellationToken)
        {
            if (userId <= 0)
                return BadRequest(new { title = "ID không hợp lệ", detail = "User ID phải là số nguyên dương." });

            if (string.IsNullOrWhiteSpace(request.RoleName))
                return BadRequest(new { title = "Role không hợp lệ", detail = "RoleName không được để trống." });

            var success = await _userService.UpdateUserRoleAsync(userId, request.RoleName, cancellationToken);
            if (!success)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "User not found or role invalid",
                    Detail = $"User with ID {userId} does not exist or the role is invalid.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(new { Message = "Cập nhật vai trò người dùng thành công." });
        }

        private bool TryGetActingUserId(out int actingUserId)
        {
            var actingUserIdClaim = User.FindFirstValue("UserId");
            return int.TryParse(actingUserIdClaim, out actingUserId);
        }

        private static ProblemDetails CreateInvalidAuthenticationProblem()
        {
            return new ProblemDetails
            {
                Title = "Token xác thực không hợp lệ",
                Detail = "Thiếu hoặc sai định danh người dùng trong token xác thực.",
                Status = StatusCodes.Status401Unauthorized
            };
        }

        private static ProblemDetails CreateQuestionNotFoundProblem(int questionId)
        {
            return new ProblemDetails
            {
                Title = "Không tìm thấy câu hỏi",
                Detail = $"Không tìm thấy câu hỏi với ID {questionId}.",
                Status = StatusCodes.Status404NotFound
            };
        }

        private static ProblemDetails CreateQuestionDeletedProblem(int questionId)
        {
            return new ProblemDetails
            {
                Title = "Câu hỏi đã bị xóa",
                Detail = $"Câu hỏi với ID {questionId} đã bị xóa mềm và không thể cập nhật.",
                Status = StatusCodes.Status400BadRequest
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
