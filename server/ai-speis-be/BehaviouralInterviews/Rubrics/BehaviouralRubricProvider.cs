using System.Text.Json;

namespace ai_speis_be.BehaviouralInterviews.Rubrics
{
    public interface IBehaviouralRubricProvider
    {
        BehaviouralRubricDefinition GetRequired(string version);
    }

    public sealed class BehaviouralRubricProvider : IBehaviouralRubricProvider
    {
        private readonly IReadOnlyDictionary<string, BehaviouralRubricDefinition> _rubrics;

        public BehaviouralRubricProvider(IWebHostEnvironment environment)
        {
            var searchDirectories = new[]
            {
                Path.Combine(environment.ContentRootPath, "BehaviouralInterviews", "Rubrics"),
                Path.Combine(AppContext.BaseDirectory, "BehaviouralInterviews", "Rubrics")
            };

            var rubricFiles = searchDirectories
                .Where(Directory.Exists)
                .SelectMany(directory => Directory.GetFiles(directory, "behaviour-rubric-*.json"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var rubrics = new Dictionary<string, BehaviouralRubricDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in rubricFiles)
            {
                var rubric = JsonSerializer.Deserialize<BehaviouralRubricDefinition>(
                    File.ReadAllText(file),
                    serializerOptions)
                    ?? throw new InvalidOperationException($"Cannot parse behavioural rubric file {file}.");

                Validate(rubric, file);
                rubrics[rubric.Version] = rubric;
            }

            _rubrics = rubrics;
        }

        public BehaviouralRubricDefinition GetRequired(string version)
        {
            return _rubrics.TryGetValue(version, out var rubric)
                ? rubric
                : throw new InvalidOperationException($"Behavioural rubric version '{version}' is not configured.");
        }

        private static void Validate(BehaviouralRubricDefinition rubric, string file)
        {
            if (string.IsNullOrWhiteSpace(rubric.Version) || rubric.Dimensions.Count == 0)
            {
                throw new InvalidOperationException($"Behavioural rubric file {file} is incomplete.");
            }

            if (rubric.Dimensions.Select(item => item.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != rubric.Dimensions.Count)
            {
                throw new InvalidOperationException($"Behavioural rubric file {file} has duplicate dimension codes.");
            }

            if (Math.Abs(rubric.Dimensions.Sum(item => item.Weight) - 1m) > 0.0001m)
            {
                throw new InvalidOperationException($"Behavioural rubric file {file} weights must total 1.0.");
            }

            var expectedLevels = Enumerable.Range(
                (int)rubric.MinimumScore,
                (int)(rubric.MaximumScore - rubric.MinimumScore) + 1);
            if (expectedLevels.Any(score => rubric.Levels.All(level => level.Score != score)))
            {
                throw new InvalidOperationException($"Behavioural rubric file {file} is missing score levels.");
            }
        }
    }
}
