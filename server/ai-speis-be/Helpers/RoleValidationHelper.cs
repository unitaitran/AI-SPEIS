using ai_speis_be.TechnicalInterviews.Selection;

namespace ai_speis_be.Helpers
{
    public static class RoleValidationHelper
    {
        public const string UnsupportedRoleErrorMessage = 
            "Hệ thống hiện chỉ hỗ trợ các vị trí: Backend Developer, Frontend Developer, Fullstack Developer, Mobile Developer, Business Analyst (BA), QA/Tester, DevOps Engineer, và Data Analyst. File/vị trí của bạn không thuộc danh sách trên.";

        public static bool IsSupportedRole(string? roleTarget, string? jobTitle = null)
        {
            if (string.IsNullOrWhiteSpace(roleTarget) && string.IsNullOrWhiteSpace(jobTitle))
                return false;

            var matchedRoles = TechnicalQuestionMetadata.ResolveRoleAliases(roleTarget, jobTitle);
            return matchedRoles.Count > 0;
        }
    }
}
