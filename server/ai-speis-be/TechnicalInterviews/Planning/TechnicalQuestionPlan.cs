using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.TechnicalInterviews.Planning
{
    public sealed record TechnicalQuestionPlanSlot(
        int MainQuestionIndex,
        TechnicalQuestionSourceType SourceType,
        string TargetSkill,
        string? TargetSubskill,
        QuestionDifficultyEnum PlannedDifficulty,
        TechnicalEvaluationObjective EvaluationObjective);

    public sealed record TechnicalQuestionPlan(
        int MatchScore,
        TechnicalMatchBand MatchBand,
        int PlannedCvQuestionCount,
        int PlannedJdQuestionCount,
        string Version,
        ImmutableArray<TechnicalQuestionPlanSlot> Slots)
    {
        public const int RequiredSlotCount = 3;

        public TechnicalQuestionPlanSlot GetRequiredSlot(int mainQuestionIndex)
        {
            return Slots.Single(slot => slot.MainQuestionIndex == mainQuestionIndex);
        }
    }

    public static class TechnicalQuestionPlanSerializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static string Serialize(TechnicalQuestionPlan plan) =>
            JsonSerializer.Serialize(plan, Options);

        public static TechnicalQuestionPlan DeserializeRequired(string json)
        {
            var plan = JsonSerializer.Deserialize<TechnicalQuestionPlan>(json, Options)
                ?? throw new InvalidOperationException("Technical Question Plan cannot be deserialized.");
            if (plan.Slots.Length != TechnicalQuestionPlan.RequiredSlotCount
                || plan.Slots.Select(slot => slot.MainQuestionIndex).Distinct().Count()
                    != TechnicalQuestionPlan.RequiredSlotCount
                || plan.Slots.Any(slot => slot.MainQuestionIndex is < 1 or > TechnicalQuestionPlan.RequiredSlotCount))
            {
                throw new InvalidOperationException("Technical Question Plan must contain exactly three unique slots.");
            }

            return plan;
        }
    }
}
