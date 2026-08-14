using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using ai_speis_be.Models.Enums;

namespace ai_speis_be.TechnicalInterviews.Planning
{
    public sealed record TechnicalLockedMainQuestionSnapshot(
        int SelectedQuestionId,
        string Content,
        string ExpectedAnswer,
        string ExpectedKeyPoints,
        string QuestionSpecificRubric,
        string RubricMetadataJson,
        string ScoringMetadataJson,
        string Skill,
        string? Subskill,
        QuestionDifficultyEnum Difficulty,
        TechnicalQuestionSourceType SourceType,
        TechnicalEvaluationObjective EvaluationObjective,
        string Language,
        string QuestionPlanVersion,
        string? QuestionBankVersion,
        DateTime LockedAt,
        string? ClarificationQuestion = null,
        string? FollowUp1 = null,
        string? FollowUp2 = null);

    public sealed record TechnicalQuestionPlanSlot(
        int MainQuestionIndex,
        TechnicalQuestionSourceType SourceType,
        string TargetSkill,
        string? TargetSubskill,
        QuestionDifficultyEnum PlannedDifficulty,
        TechnicalEvaluationObjective EvaluationObjective,
        TechnicalLockedMainQuestionSnapshot? LockedQuestion = null)
    {
        public int? SelectedQuestionId => LockedQuestion?.SelectedQuestionId;
        public bool IsLocked => LockedQuestion is not null;
    }

    public sealed record TechnicalQuestionPlan(
        int MatchScore,
        TechnicalMatchBand MatchBand,
        int PlannedCvQuestionCount,
        int PlannedJdQuestionCount,
        string Version,
        ImmutableArray<TechnicalQuestionPlanSlot> Slots,
        int TargetMainQuestionCount = 3,
        string? SelectionContextKey = null,
        string? QuestionOrderVersion = null)
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
            if (plan.TargetMainQuestionCount == 0)
            {
                plan = plan with { TargetMainQuestionCount = plan.Slots.Length };
            }
            if (plan.TargetMainQuestionCount is < 1 or > 20
                || plan.Slots.Length != plan.TargetMainQuestionCount
                || plan.Slots.Select(slot => slot.MainQuestionIndex).Distinct().Count()
                    != plan.TargetMainQuestionCount
                || plan.Slots.Any(slot => slot.MainQuestionIndex < 1
                    || slot.MainQuestionIndex > plan.TargetMainQuestionCount))
            {
                throw new InvalidOperationException("Technical Question Plan contains an invalid set of unique slots.");
            }
            if (plan.Slots.All(slot => slot.IsLocked)
                && plan.Slots.Select(slot => slot.SelectedQuestionId).Distinct().Count()
                    != plan.TargetMainQuestionCount)
            {
                throw new InvalidOperationException("Locked Main Question ids must be unique within the plan.");
            }

            return plan;
        }
    }
}
