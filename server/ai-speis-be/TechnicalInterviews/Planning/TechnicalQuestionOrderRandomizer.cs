using System.Collections.Immutable;
using System.Security.Cryptography;

namespace ai_speis_be.TechnicalInterviews.Planning
{
    public interface ITechnicalQuestionOrderRandomizer
    {
        TechnicalQuestionPlan Randomize(
            TechnicalQuestionPlan lockedPlan,
            IReadOnlyList<IReadOnlyList<int>> previousOrders);
    }

    public sealed class TechnicalQuestionOrderRandomizer : ITechnicalQuestionOrderRandomizer
    {
        public const string StrategyVersion = "technical-main-question-order-v1";
        private const int RandomAttemptLimit = 64;
        private readonly Random? _random;

        public TechnicalQuestionOrderRandomizer()
        {
        }

        internal TechnicalQuestionOrderRandomizer(Random random)
        {
            _random = random;
        }

        public TechnicalQuestionPlan Randomize(
            TechnicalQuestionPlan lockedPlan,
            IReadOnlyList<IReadOnlyList<int>> previousOrders)
        {
            ArgumentNullException.ThrowIfNull(lockedPlan);
            ArgumentNullException.ThrowIfNull(previousOrders);

            var original = lockedPlan.Slots
                .OrderBy(slot => slot.MainQuestionIndex)
                .ToArray();
            if (original.Any(slot => slot.LockedQuestion is null))
            {
                throw new InvalidOperationException(
                    "Main-question order can only be randomized after every plan slot is locked.");
            }

            if (original.Length < 2)
            {
                return lockedPlan with { QuestionOrderVersion = StrategyVersion };
            }

            var previousKeys = previousOrders
                .Where(order => order.Count == original.Length)
                .Select(OrderKey)
                .ToHashSet(StringComparer.Ordinal);
            var mostRecentKey = previousOrders.FirstOrDefault(order => order.Count == original.Length) is { } mostRecent
                ? OrderKey(mostRecent)
                : null;

            TechnicalQuestionPlanSlot[]? selected = null;
            for (var attempt = 0; attempt < RandomAttemptLimit; attempt++)
            {
                var candidate = Shuffle(original);
                if (!previousKeys.Contains(OrderKey(candidate)))
                {
                    selected = candidate;
                    break;
                }
            }

            if (selected is null)
            {
                var shuffled = Shuffle(original);
                selected = FindUnseenRotation(shuffled, previousKeys)
                    ?? EnsureDifferentFromMostRecent(shuffled, mostRecentKey);
            }

            var reindexed = selected
                .Select((slot, index) => slot with { MainQuestionIndex = index + 1 })
                .ToImmutableArray();
            return lockedPlan with
            {
                Slots = reindexed,
                QuestionOrderVersion = StrategyVersion
            };
        }

        private TechnicalQuestionPlanSlot[] Shuffle(
            IReadOnlyList<TechnicalQuestionPlanSlot> source)
        {
            var shuffled = source.ToArray();
            for (var index = shuffled.Length - 1; index > 0; index--)
            {
                var swapIndex = Next(index + 1);
                (shuffled[index], shuffled[swapIndex]) = (shuffled[swapIndex], shuffled[index]);
            }
            return shuffled;
        }

        private static TechnicalQuestionPlanSlot[]? FindUnseenRotation(
            IReadOnlyList<TechnicalQuestionPlanSlot> source,
            IReadOnlySet<string> previousKeys)
        {
            for (var offset = 1; offset < source.Count; offset++)
            {
                var candidate = source
                    .Skip(offset)
                    .Concat(source.Take(offset))
                    .ToArray();
                if (!previousKeys.Contains(OrderKey(candidate)))
                    return candidate;
            }
            return null;
        }

        private static TechnicalQuestionPlanSlot[] EnsureDifferentFromMostRecent(
            TechnicalQuestionPlanSlot[] candidate,
            string? mostRecentKey)
        {
            if (mostRecentKey is null || !string.Equals(OrderKey(candidate), mostRecentKey, StringComparison.Ordinal))
                return candidate;

            (candidate[0], candidate[1]) = (candidate[1], candidate[0]);
            return candidate;
        }

        private int Next(int exclusiveMaximum)
        {
            return _random?.Next(exclusiveMaximum)
                ?? RandomNumberGenerator.GetInt32(exclusiveMaximum);
        }

        private static string OrderKey(IEnumerable<TechnicalQuestionPlanSlot> slots) =>
            OrderKey(slots.Select(slot => slot.SelectedQuestionId!.Value).ToArray());

        private static string OrderKey(IEnumerable<int> questionIds) =>
            string.Join(',', questionIds);
    }
}
