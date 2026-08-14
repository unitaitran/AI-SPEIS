using ai_speis_be.Models;

namespace ai_speis_be.Services.UserService
{
    public enum UpdateUserRoleOutcome
    {
        Updated,
        UserNotFound,
        InvalidRole,
        AdminDemotionForbidden
    }

    public sealed record UpdateUserRoleResult(
        UpdateUserRoleOutcome Outcome,
        User? User = null);
}
