using System.Collections.Generic;
using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Helpers
{
    public static class RoleCategoryHelper
    {
        public static AvailableRoundsDto GetAvailableRounds(string? roleTarget)
        {
            var normalized = roleTarget?.Trim().ToUpper() ?? "OTHER";
            var rounds = new List<string> { "Behavior", "Technical" };
            bool hasOptionalCoding = false;

            // Hỗ trợ 5 vai trò chính: BE, FE, FULLSTACK, BA, TESTER
            if (normalized.Contains("FULLSTACK") || normalized.Contains("FULL STACK") ||
                normalized.Contains("BACKEND") || normalized.Contains("FRONTEND") ||
                normalized.Contains("DEVELOPER") || normalized.Contains("ENGINEER") ||
                normalized.Contains("SOFTWARE") || normalized.Contains("DEV") ||
                normalized == "BE" || normalized == "FE")
            {
                rounds.Add("Code");
            }
            else
            {
                hasOptionalCoding = true; // Linh hoạt: Tất cả các vai trò không lập trình khác (BA, Product Owner, PM, Tester, etc.) đều có thể chọn thêm vòng Coding
            }

            return new AvailableRoundsDto
            {
                RoleTarget = roleTarget ?? "Other",
                AvailableRounds = rounds,
                HasOptionalCoding = hasOptionalCoding
            };
        }
    }
}
