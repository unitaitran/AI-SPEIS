using ai_speis_be.Models.DTOs;
using ai_speis_be.Services.UserService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ai_speis_be.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "admin,Admin")]
    public sealed class AdminController : ControllerBase
    {
        private readonly IUserService _userService;

        public AdminController(IUserService userService)
        {
            _userService = userService;
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
    }
}
