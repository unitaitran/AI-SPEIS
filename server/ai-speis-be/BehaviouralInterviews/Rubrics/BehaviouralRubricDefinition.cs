namespace ai_speis_be.BehaviouralInterviews.Rubrics
{
    public sealed class BehaviouralRubricDefinition
    {
        public string Version { get; set; } = string.Empty;
        public string ScoringPolicyVersion { get; set; } = string.Empty;
        public decimal MinimumScore { get; set; }
        public decimal MaximumScore { get; set; } = 10m;
        public int RoundingPrecision { get; set; } = 2;
        public decimal EvidenceRequiredWhenScoreAbove { get; set; }
        public List<BehaviouralRubricDimension> Dimensions { get; set; } = new();
        public List<BehaviouralRubricLevel> Levels { get; set; } = new();
        public List<BehaviouralPerformanceBand> PerformanceBands { get; set; } = new();
        public BehaviouralQuestionLimits Limits { get; set; } = new();

        public string GetLevelCode(decimal score)
        {
            var rounded = (int)Math.Round(score, 0, MidpointRounding.AwayFromZero);
            rounded = Math.Clamp(rounded, (int)MinimumScore, (int)MaximumScore);
            return Levels.First(level => level.Score == rounded).Code;
        }

        public BehaviouralPerformanceBand GetPerformanceBand(decimal score)
        {
            var band = PerformanceBands.FirstOrDefault(item =>
                score >= item.Minimum && score <= item.Maximum);

            return band ?? throw new InvalidOperationException(
                $"Rubric {Version} does not contain a performance band for score {score}.");
        }
    }

    public sealed class BehaviouralRubricDimension
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Weight { get; set; }
    }

    public sealed class BehaviouralRubricLevel
    {
        public string Code { get; set; } = string.Empty;
        public int Score { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public sealed class BehaviouralPerformanceBand
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Minimum { get; set; }
        public decimal Maximum { get; set; }
    }

    public sealed class BehaviouralQuestionLimits
    {
        public int MaxClarificationsPerMainQuestion { get; set; } = 1;
        public int MaxFollowUpsPerMainQuestion { get; set; } = 2;
        public int MaxTotalSubQuestionsPerMainQuestion { get; set; } = 2;
    }
}
