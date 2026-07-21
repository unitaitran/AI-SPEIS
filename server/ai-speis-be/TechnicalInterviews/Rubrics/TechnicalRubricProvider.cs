using System.Text.Json;

namespace ai_speis_be.TechnicalInterviews.Rubrics
{
    public interface ITechnicalRubricProvider
    {
        TechnicalRubricDefinition GetRequired(string version);
    }

    public sealed class TechnicalRubricProvider : ITechnicalRubricProvider
    {
        private readonly IReadOnlyDictionary<string, TechnicalRubricDefinition> _rubrics;

        public TechnicalRubricProvider(IWebHostEnvironment environment)
        {
            var searchDirectories = new[]
            {
                Path.Combine(environment.ContentRootPath, "TechnicalInterviews", "Rubrics"),
                Path.Combine(AppContext.BaseDirectory, "TechnicalInterviews", "Rubrics")
            };

            var rubricFiles = searchDirectories
                .Where(Directory.Exists)
                .SelectMany(directory => Directory.GetFiles(directory, "technical-rubric-*.json"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var rubrics = new Dictionary<string, TechnicalRubricDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in rubricFiles)
            {
                var rubric = JsonSerializer.Deserialize<TechnicalRubricDefinition>(
                    File.ReadAllText(file),
                    serializerOptions)
                    ?? throw new InvalidOperationException($"Cannot parse technical rubric file {file}.");

                Validate(rubric, file);
                rubrics[rubric.Version] = rubric;
            }

            _rubrics = rubrics;
        }

        public TechnicalRubricDefinition GetRequired(string version)
        {
            return _rubrics.TryGetValue(version, out var rubric)
                ? rubric
                : throw new InvalidOperationException($"Technical rubric version '{version}' is not configured.");
        }

        private static void Validate(TechnicalRubricDefinition rubric, string file)
        {
            if (string.IsNullOrWhiteSpace(rubric.Version) || rubric.Dimensions.Count == 0)
            {
                throw new InvalidOperationException($"Technical rubric file {file} is incomplete.");
            }

            if (rubric.Dimensions.Select(item => item.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != rubric.Dimensions.Count)
            {
                throw new InvalidOperationException($"Technical rubric file {file} has duplicate dimension codes.");
            }

            if (Math.Abs(rubric.Dimensions.Sum(item => item.Weight) - 1m) > 0.0001m)
            {
                throw new InvalidOperationException($"Technical rubric file {file} weights must total 1.0.");
            }

            var expectedLevels = Enumerable.Range(
                (int)rubric.MinimumScore,
                (int)(rubric.MaximumScore - rubric.MinimumScore) + 1);
            if (expectedLevels.Any(score => rubric.Levels.All(level => level.Score != score)))
            {
                throw new InvalidOperationException($"Technical rubric file {file} is missing score levels.");
            }
        }
    }
}
