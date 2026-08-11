using ai_speis_be.Models.DTOs;

namespace ai_speis_be.CampaignResults
{
    public static class CampaignResultCalculator
    {
        private static readonly IReadOnlyDictionary<string, decimal> RoundWeights =
            new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["Technical"] = 0.40m,
                ["Code"] = 0.40m,
                ["Behavior"] = 0.20m
            };

        public static decimal GetCodingScore(int passedTestCases, int totalTestCases)
        {
            if (totalTestCases <= 0 || passedTestCases <= 0) return 0m;
            var passRate = Math.Clamp((decimal)passedTestCases / totalTestCases * 100m, 0m, 100m);
            return Round(passRate / 10m, 2);
        }

        public static decimal ApplyRoundWeights(ICollection<CampaignRoundResultDto> rounds)
        {
            if (rounds.Count == 0) return 0m;

            foreach (var round in rounds)
            {
                round.BaseWeight = RoundWeights.TryGetValue(round.RoundType, out var weight) ? weight : 0m;
            }

            var selectedWeight = rounds.Sum(round => round.BaseWeight);
            if (selectedWeight <= 0m) return 0m;

            foreach (var round in rounds)
            {
                round.AppliedWeight = Round(round.BaseWeight / selectedWeight, 4);
            }

            return Round(rounds.Sum(round => round.Score * round.BaseWeight) / selectedWeight, 2);
        }

        public static decimal? CalculateMetric(params (decimal? Score, decimal Weight, string Source)[] components)
        {
            var available = components
                .Where(component => component.Score.HasValue)
                .ToList();
            var availableWeight = available.Sum(component => component.Weight);
            if (availableWeight <= 0m) return null;

            return Round(
                available.Sum(component => component.Score!.Value * component.Weight) / availableWeight,
                2);
        }

        public static string GetPerformanceBand(decimal score) => score switch
        {
            >= 9m => "EXCELLENT",
            >= 8m => "VERY_GOOD",
            >= 6.5m => "GOOD",
            >= 5m => "MINIMUM_REQUIREMENT_MET",
            >= 3m => "WEAK",
            _ => "VERY_WEAK"
        };

        public static decimal Round(decimal value, int precision = 2) =>
            Math.Round(value, precision, MidpointRounding.AwayFromZero);
    }
}
