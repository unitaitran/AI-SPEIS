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
            if (normalized == "BE" || normalized == "BACKEND" || 
                normalized == "FE" || normalized == "FRONTEND" || 
                normalized == "FULLSTACK" || normalized == "FULL STACK")
            {
                rounds.Add("Code");
            }
            else if (normalized == "BA" || normalized == "TESTER")
            {
                hasOptionalCoding = true; // Cho phép lựa chọn thêm vòng coding (Code)
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
