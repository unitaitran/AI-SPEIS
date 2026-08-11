using System.Text.Json;

namespace ai_speis_be.TechnicalInterviews.Rubrics
{
    public interface ITechnicalRubricProvider
    {
        TechnicalRubricDefinition GetRequired(string version);
    }

    public sealed class TechnicalRubricProvider : ITechnicalRubricProvider
    {
        private static readonly string[] TechnicalV2DimensionCodes =
        {
            "ACCURACY",
            "TECHNICAL_DEPTH",
            "REASONING",
            "APPLICATION",
            "COMMUNICATION"
        };
        private static readonly decimal[] TechnicalV2DimensionWeights =
        {
            0.30m,
            0.25m,
            0.20m,
            0.15m,
            0.10m
        };
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

            if (string.Equals(rubric.Version, "technical-v2-runtime", StringComparison.OrdinalIgnoreCase)
                && (!rubric.Dimensions.Select(item => item.Code).SequenceEqual(TechnicalV2DimensionCodes, StringComparer.OrdinalIgnoreCase)
                    || !rubric.Dimensions.Select(item => item.Weight).SequenceEqual(TechnicalV2DimensionWeights)
                    || rubric.MinimumScore != 0m
                    || rubric.MaximumScore != 10m
                    || rubric.RoundingPrecision != 2))
            {
                throw new InvalidOperationException($"Technical V2 rubric file {file} must contain exactly the canonical five criteria with weights 0.30/0.25/0.20/0.15/0.10, score range 0-10 and precision 2.");
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

            if (rubric.RoundingPrecision is < 0 or > 4
                || rubric.PerformanceBands.Count == 0
                || rubric.PerformanceBands.Any(item =>
                    string.IsNullOrWhiteSpace(item.Code)
                    || item.Minimum < rubric.MinimumScore
                    || item.Maximum > rubric.MaximumScore
                    || item.Minimum > item.Maximum)
                || rubric.PerformanceBands.Select(item => item.Code.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() != rubric.PerformanceBands.Count)
            {
                throw new InvalidOperationException($"Technical rubric file {file} has invalid performance bands.");
            }

            var scoreStep = 1m;
            for (var index = 0; index < rubric.RoundingPrecision; index++)
            {
                scoreStep /= 10m;
            }
            for (var score = rubric.MinimumScore; score <= rubric.MaximumScore; score += scoreStep)
            {
                var matchingBandCount = rubric.PerformanceBands.Count(item =>
                    score >= item.Minimum
                    && (item.MaximumExclusive ? score < item.Maximum : score <= item.Maximum));
                if (matchingBandCount != 1)
                {
                    throw new InvalidOperationException(
                        $"Technical rubric file {file} must map rounded score {score} to exactly one performance band.");
                }
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
