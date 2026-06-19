using ai_speis_be.Models;

namespace ai_speis_be.Services.UserService
{
    public enum LockUserOutcome
    {
        Locked,
        AlreadyLocked,
        UserNotFound,
        CannotLockSelf,
        ProtectedRole
    }

    public sealed record LockUserResult(
        LockUserOutcome Outcome,
        User? User = null);

    public enum UnlockUserOutcome
    {
        Unlocked,
        UserNotFound,
        NotLocked
    }

    public sealed record UnlockUserResult(
        UnlockUserOutcome Outcome,
        User? User = null);
}
