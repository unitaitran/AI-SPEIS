namespace ai_speis_be.TechnicalInterviews.Rubrics
{
    public sealed class TechnicalRubricDefinition
    {
        public string Version { get; set; } = string.Empty;
        public string ScoringPolicyVersion { get; set; } = string.Empty;
        public decimal MinimumScore { get; set; }
        public decimal MaximumScore { get; set; } = 5m;
        public int RoundingPrecision { get; set; } = 2;
        public decimal EvidenceRequiredWhenScoreAbove { get; set; }
        public List<TechnicalRubricDimension> Dimensions { get; set; } = new();
        public List<TechnicalRubricLevel> Levels { get; set; } = new();
        public List<TechnicalPerformanceBand> PerformanceBands { get; set; } = new();
        public TechnicalQuestionLimits Limits { get; set; } = new();

        public string GetLevelCode(decimal score)
        {
            var rounded = (int)Math.Round(score, 0, MidpointRounding.AwayFromZero);
            rounded = Math.Clamp(rounded, (int)MinimumScore, (int)MaximumScore);
            return Levels.First(level => level.Score == rounded).Code;
        }

        public TechnicalPerformanceBand GetPerformanceBand(decimal score)
        {
            var band = PerformanceBands.FirstOrDefault(item =>
                score >= item.Minimum
                && (item.MaximumExclusive ? score < item.Maximum : score <= item.Maximum));

            return band ?? throw new InvalidOperationException(
                $"Rubric {Version} does not contain a performance band for score {score}.");
        }

        public string GetPerformanceBandCode(decimal score)
        {
            var code = GetPerformanceBand(score).Code?.Trim();
            return !string.IsNullOrWhiteSpace(code)
                ? code.ToUpperInvariant()
                : throw new InvalidOperationException(
                    $"Rubric {Version} contains an empty performance band code.");
        }
    }

    public sealed class TechnicalRubricDimension
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Weight { get; set; }
    }

    public sealed class TechnicalRubricLevel
    {
        public string Code { get; set; } = string.Empty;
        public int Score { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public sealed class TechnicalPerformanceBand
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Minimum { get; set; }
        public decimal Maximum { get; set; }
        public bool MaximumExclusive { get; set; }
    }

    public sealed class TechnicalQuestionLimits
    {
        public int MaxClarificationsPerMainQuestion { get; set; } = 1;
        public int MaxFollowUpsPerMainQuestion { get; set; } = 2;
        public int MaxTotalSubQuestionsPerMainQuestion { get; set; } = 2;
    }
}
