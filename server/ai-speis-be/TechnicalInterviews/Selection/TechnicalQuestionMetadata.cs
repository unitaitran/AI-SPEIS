using System.Text.Json;

namespace ai_speis_be.TechnicalInterviews.Selection
{
    public static class TechnicalQuestionMetadata
    {
        public static IReadOnlyList<string> ResolveRoleAliases(string? roleTarget, string? jobTitle = null)
        {
            var normalized = Normalize(roleTarget);
            if (normalized.Length == 0)
            {
                normalized = Normalize(jobTitle);
            }

            if (normalized.Contains("businessanalyst") || normalized == "ba")
                return new[] { "Business Analyst (BA)" };
            if (normalized.Contains("tester") || normalized.Contains("qa"))
                return new[] { "QA / Tester" };
            if (normalized.Contains("backend") || normalized == "be")
                return new[] { "Backend Developer" };
            if (normalized.Contains("frontend") || normalized == "fe")
                return new[] { "Frontend Developer" };
            if (normalized.Contains("fullstack"))
                return new[] { "Fullstack Developer" };
            if (normalized.Contains("mobile"))
                return new[] { "Mobile Developer" };
            if (normalized.Contains("devops"))
                return new[] { "DevOps Engineer" };
            if (normalized.Contains("dataanalyst") || (normalized.Contains("data") && normalized.Contains("analyst")))
                return new[] { "Data Analyst" };

            return Array.Empty<string>();
        }

        public static IReadOnlyList<string> ResolveExperienceAliases(string? experienceLevel)
        {
            var normalized = Normalize(experienceLevel);
            if (normalized.Contains("intern"))
                return new[] { "Intern/Fresher" };
            if (normalized.Contains("fresher"))
                return new[] { "Intern/Fresher", "Fresher/Junior" };
            if (normalized.Contains("junior"))
                return new[] { "Fresher/Junior", "Junior", "Junior/Middle" };
            if (normalized.Contains("mid") || normalized.Contains("middle"))
                return new[] { "Junior/Middle", "Middle" };
            if (normalized.Contains("senior") || normalized.Contains("lead"))
                return new[] { "Middle" };

            return Array.Empty<string>();
        }

        public static IReadOnlyList<string> ParseStringArray(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<string>();

            try
            {
                var values = JsonSerializer.Deserialize<List<string>>(json);
                return values is null
                    ? Array.Empty<string>()
                    : values
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
            }
            catch (JsonException)
            {
                return Array.Empty<string>();
            }
        }

        public static string? GetSubskill(string? qdrantPayloadJson)
        {
            if (string.IsNullOrWhiteSpace(qdrantPayloadJson))
                return null;

            try
            {
                using var document = JsonDocument.Parse(qdrantPayloadJson);
                return document.RootElement.TryGetProperty("subskill", out var subskill)
                    ? subskill.GetString()
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static bool FuzzyMatches(string left, string right)
        {
            var normalizedLeft = Normalize(left);
            var normalizedRight = Normalize(right);
            if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
                return false;
            if (string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal))
                return true;

            // One-character technology names such as C/C# must not fuzzy-match
            // unrelated skills merely because their normalized text contains "c".
            return Math.Min(normalizedLeft.Length, normalizedRight.Length) >= 3
                && (normalizedLeft.Contains(normalizedRight, StringComparison.Ordinal)
                    || normalizedRight.Contains(normalizedLeft, StringComparison.Ordinal));
        }

        private static string Normalize(string? value)
        {
            var expanded = (value ?? string.Empty)
                .Replace("C#", "csharp", StringComparison.OrdinalIgnoreCase)
                .Replace("C++", "cplusplus", StringComparison.OrdinalIgnoreCase)
                .Replace(".NET", "dotnet", StringComparison.OrdinalIgnoreCase);
            return new string(expanded
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
        }
    }
}
