using System.Collections.Immutable;
using ai_speis_be.Models.Enums;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Planning;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalLockedQuestionPlanTests
{
    [Fact]
    public void SerializeRefresh_PreservesAllThreeLockedQuestionIdsAndSnapshots()
    {
        var plan = CreatePlan(3);

        var refreshed = TechnicalQuestionPlanSerializer.DeserializeRequired(
            TechnicalQuestionPlanSerializer.Serialize(plan));

        Assert.Equal(
            new[] { 101, 102, 103 },
            refreshed.Slots.Select(item => item.SelectedQuestionId!.Value));
        Assert.Equal(
            plan.Slots.Select(item => item.LockedQuestion!.ExpectedKeyPoints),
            refreshed.Slots.Select(item => item.LockedQuestion!.ExpectedKeyPoints));
        Assert.Equal(
            plan.Slots.Select(item => item.LockedQuestion!.ClarificationQuestion),
            refreshed.Slots.Select(item => item.LockedQuestion!.ClarificationQuestion));
        Assert.Equal(
            plan.Slots.Select(item => item.LockedQuestion!.FollowUp2),
            refreshed.Slots.Select(item => item.LockedQuestion!.FollowUp2));
    }

    [Fact]
    public void Deserialize_RejectsDuplicateLockedQuestionIds()
    {
        var plan = CreatePlan(3);
        var duplicate = plan with
        {
            Slots = plan.Slots.SetItem(2, plan.Slots[2] with
            {
                LockedQuestion = plan.Slots[2].LockedQuestion! with { SelectedQuestionId = 101 }
            })
        };

        Assert.Throws<InvalidOperationException>(() =>
            TechnicalQuestionPlanSerializer.DeserializeRequired(
                TechnicalQuestionPlanSerializer.Serialize(duplicate)));
    }

    [Fact]
    public void PracticePlan_AllowsConfiguredMainCountAndKeepsEverySlotLocked()
    {
        var plan = CreatePlan(4);
        var refreshed = TechnicalQuestionPlanSerializer.DeserializeRequired(
            TechnicalQuestionPlanSerializer.Serialize(plan));

        Assert.Equal(4, refreshed.TargetMainQuestionCount);
        Assert.All(refreshed.Slots, slot => Assert.True(slot.IsLocked));
    }

    [Fact]
    public void Randomize_PreservesLockedSetAndReindexesAppearanceOrder()
    {
        var plan = CreatePlan(3);

        var randomized = new TechnicalQuestionOrderRandomizer().Randomize(
            plan,
            Array.Empty<IReadOnlyList<int>>());

        Assert.Equal(
            plan.Slots.Select(slot => slot.SelectedQuestionId).OrderBy(id => id),
            randomized.Slots.Select(slot => slot.SelectedQuestionId).OrderBy(id => id));
        Assert.Equal(new[] { 1, 2, 3 }, randomized.Slots.Select(slot => slot.MainQuestionIndex));
        Assert.Equal(TechnicalQuestionOrderRandomizer.StrategyVersion, randomized.QuestionOrderVersion);
        Assert.All(randomized.Slots, slot => Assert.True(slot.IsLocked));
    }

    [Fact]
    public void Randomize_AvoidsMostRecentOrderForTheSameLockedSet()
    {
        var plan = CreatePlan(3);
        var mostRecent = plan.Slots.Select(slot => slot.SelectedQuestionId!.Value).ToArray();

        var randomized = new TechnicalQuestionOrderRandomizer().Randomize(
            plan,
            new IReadOnlyList<int>[] { mostRecent });

        Assert.False(mostRecent.SequenceEqual(
            randomized.Slots.Select(slot => slot.SelectedQuestionId!.Value)));
    }

    [Fact]
    public void Randomize_WhenEveryPermutationWasUsed_StillAvoidsImmediateRepeat()
    {
        var plan = CreatePlan(3);
        IReadOnlyList<IReadOnlyList<int>> history = new IReadOnlyList<int>[]
        {
            new[] { 101, 102, 103 },
            new[] { 101, 103, 102 },
            new[] { 102, 101, 103 },
            new[] { 102, 103, 101 },
            new[] { 103, 101, 102 },
            new[] { 103, 102, 101 }
        };

        var randomized = new TechnicalQuestionOrderRandomizer().Randomize(plan, history);

        Assert.False(history[0].SequenceEqual(
            randomized.Slots.Select(slot => slot.SelectedQuestionId!.Value)));
    }

    [Fact]
    public void ProviderContract_CannotSelectOrReplaceMainQuestions()
    {
        var methods = typeof(ITechnicalInterviewAIProvider).GetMethods()
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("SelectQuestionAsync", methods);
        Assert.DoesNotContain("GenerateQuestionBundleAsync", methods);
    }

    private static TechnicalQuestionPlan CreatePlan(int count)
    {
        var lockedAt = new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc);
        var slots = Enumerable.Range(1, count).Select(index =>
        {
            var source = index % 2 == 0 ? TechnicalQuestionSourceType.CV : TechnicalQuestionSourceType.JD;
            var objective = source == TechnicalQuestionSourceType.CV
                ? TechnicalEvaluationObjective.CvSkillVerification
                : TechnicalEvaluationObjective.JdCoreKnowledge;
            return new TechnicalQuestionPlanSlot(
                index,
                source,
                $"Skill {index}",
                null,
                QuestionDifficultyEnum.Medium,
                objective,
                new TechnicalLockedMainQuestionSnapshot(
                    100 + index,
                    $"Question {index}",
                    $"Expected answer {index}",
                    $"Key point {index}",
                    "Rubric metadata",
                    "{}",
                    "{}",
                    $"Skill {index}",
                    null,
                    QuestionDifficultyEnum.Medium,
                    source,
                    objective,
                    "vi",
                    "plan-v2",
                    "bank-v1",
                    lockedAt,
                    $"Clarification {index}",
                    $"Follow-up one {index}",
                    $"Follow-up two {index}"));
        }).ToImmutableArray();
        return new TechnicalQuestionPlan(70, TechnicalMatchBand.High, 1, count - 1, "plan-v2", slots, count);
    }
}
