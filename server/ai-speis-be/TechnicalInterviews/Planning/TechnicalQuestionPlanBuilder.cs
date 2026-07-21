using System.Collections.Immutable;
using ai_speis_be.Models.Enums;
using ai_speis_be.TechnicalInterviews.Selection;

namespace ai_speis_be.TechnicalInterviews.Planning
{
    public sealed record TechnicalQuestionPlanRequest(
        int MatchScore,
        IReadOnlyCollection<string> CvSkills,
        IReadOnlyCollection<string> RequiredJdSkills,
        IReadOnlyCollection<string> NiceToHaveJdSkills,
        IReadOnlyCollection<string> AvailableQuestionBankSkills,
        string PlanVersion);

    public sealed record TechnicalQuestionPlanBuildResult(
        TechnicalQuestionPlan? Plan,
        string? ErrorCode,
        string? Message)
    {
        public bool IsSuccess => Plan is not null;
    }

    public interface ITechnicalQuestionPlanBuilder
    {
        TechnicalQuestionPlanBuildResult Build(TechnicalQuestionPlanRequest request);
    }

    public sealed class TechnicalQuestionPlanBuilder : ITechnicalQuestionPlanBuilder
    {
        public TechnicalQuestionPlanBuildResult Build(TechnicalQuestionPlanRequest request)
        {
            if (request.MatchScore is < 0 or > 100)
            {
                return Failure("MATCH_SCORE_OUT_OF_RANGE", "CV-JD Match Score must be between 0 and 100.");
            }

            var bankSkills = Clean(request.AvailableQuestionBankSkills);
            if (bankSkills.Count < TechnicalQuestionPlan.RequiredSlotCount)
            {
                return Failure(
                    "INSUFFICIENT_DISTINCT_QUESTION_BANK_SKILLS",
                    "At least three distinct active Question Bank skills are required for the Technical Question Plan.");
            }
            var cvSkills = Canonicalize(request.CvSkills, bankSkills);
            var requiredJdSkills = Canonicalize(request.RequiredJdSkills, bankSkills);
            var allJdSkills = requiredJdSkills
                .Concat(Canonicalize(request.NiceToHaveJdSkills, bankSkills))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (cvSkills.Count == 0 || allJdSkills.Count == 0)
            {
                return Failure(
                    "INSUFFICIENT_PLAN_SOURCE_SKILLS",
                    "Both CV and JD must provide at least one skill for the Technical Question Plan.");
            }

            var distinctSkills = cvSkills.Concat(allJdSkills)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (distinctSkills.Count < TechnicalQuestionPlan.RequiredSlotCount)
            {
                return Failure(
                    "INSUFFICIENT_DISTINCT_PLAN_SKILLS",
                    "At least three distinct CV/JD skills are required to build a non-duplicating Technical Question Plan.");
            }

            var band = ResolveBand(request.MatchScore);
            var (cvCount, jdCount) = ResolveAllocation(band, cvSkills, allJdSkills, bankSkills);
            var sources = ResolveSourceOrder(cvCount, jdCount);
            var difficulties = ResolveDifficulties(band);
            var usedSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var slots = ImmutableArray.CreateBuilder<TechnicalQuestionPlanSlot>(TechnicalQuestionPlan.RequiredSlotCount);

            for (var index = 0; index < TechnicalQuestionPlan.RequiredSlotCount; index++)
            {
                var source = sources[index];
                var candidates = source == TechnicalQuestionSourceType.JD
                    ? requiredJdSkills.Concat(allJdSkills)
                    : cvSkills.OrderBy(skill => requiredJdSkills.Any(required =>
                        TechnicalQuestionMetadata.FuzzyMatches(required, skill)) ? 0 : 1);
                var skill = candidates.FirstOrDefault(candidate => !usedSkills.Contains(candidate));
                if (skill is null)
                {
                    return Failure(
                        "INSUFFICIENT_UNIQUE_SOURCE_SKILLS",
                        $"The {source} source does not contain enough distinct skills for its planned allocation.");
                }

                usedSkills.Add(skill);
                slots.Add(new TechnicalQuestionPlanSlot(
                    index + 1,
                    source,
                    skill,
                    null,
                    difficulties[index],
                    ResolveObjective(band, source, index)));
            }

            var plan = new TechnicalQuestionPlan(
                request.MatchScore,
                band,
                cvCount,
                jdCount,
                request.PlanVersion,
                slots.MoveToImmutable());
            return new TechnicalQuestionPlanBuildResult(plan, null, null);
        }

        private static TechnicalMatchBand ResolveBand(int matchScore) => matchScore switch
        {
            <= 39 => TechnicalMatchBand.Low,
            <= 69 => TechnicalMatchBand.Medium,
            _ => TechnicalMatchBand.High
        };

        private static (int Cv, int Jd) ResolveAllocation(
            TechnicalMatchBand band,
            IReadOnlyCollection<string> cvSkills,
            IReadOnlyCollection<string> jdSkills,
            IReadOnlyCollection<string> bankSkills)
        {
            if (band == TechnicalMatchBand.Low)
                return (2, 1);
            if (band == TechnicalMatchBand.High)
                return (1, 2);

            var cvCoverage = CountBankCoverage(cvSkills, bankSkills);
            var jdCoverage = CountBankCoverage(jdSkills, bankSkills);
            return cvCoverage > jdCoverage ? (2, 1) : (1, 2);
        }

        private static int CountBankCoverage(
            IEnumerable<string> sourceSkills,
            IReadOnlyCollection<string> bankSkills)
        {
            return sourceSkills.Count(source => bankSkills.Any(bank =>
                TechnicalQuestionMetadata.FuzzyMatches(source, bank)));
        }

        private static TechnicalQuestionSourceType[] ResolveSourceOrder(int cvCount, int jdCount)
        {
            if (cvCount == 2 && jdCount == 1)
            {
                return new[]
                {
                    TechnicalQuestionSourceType.CV,
                    TechnicalQuestionSourceType.JD,
                    TechnicalQuestionSourceType.CV
                };
            }

            return new[]
            {
                TechnicalQuestionSourceType.JD,
                TechnicalQuestionSourceType.CV,
                TechnicalQuestionSourceType.JD
            };
        }

        private static QuestionDifficultyEnum[] ResolveDifficulties(TechnicalMatchBand band) => band switch
        {
            TechnicalMatchBand.Low => new[]
            {
                QuestionDifficultyEnum.Easy,
                QuestionDifficultyEnum.Easy,
                QuestionDifficultyEnum.Medium
            },
            TechnicalMatchBand.High => new[]
            {
                QuestionDifficultyEnum.Medium,
                QuestionDifficultyEnum.Hard,
                QuestionDifficultyEnum.Hard
            },
            _ => Enumerable.Repeat(QuestionDifficultyEnum.Medium, TechnicalQuestionPlan.RequiredSlotCount).ToArray()
        };

        private static TechnicalEvaluationObjective ResolveObjective(
            TechnicalMatchBand band,
            TechnicalQuestionSourceType source,
            int zeroBasedIndex)
        {
            if (source == TechnicalQuestionSourceType.CV)
            {
                return zeroBasedIndex == 0
                    ? TechnicalEvaluationObjective.CvSkillVerification
                    : TechnicalEvaluationObjective.CvProjectApplication;
            }

            if (band == TechnicalMatchBand.High)
            {
                return zeroBasedIndex == 0
                    ? TechnicalEvaluationObjective.JdDepthAndTradeOff
                    : zeroBasedIndex == 2
                        ? TechnicalEvaluationObjective.JdOptimization
                        : TechnicalEvaluationObjective.JdRealWorldApplication;
            }

            return zeroBasedIndex == 2
                ? TechnicalEvaluationObjective.JdRealWorldApplication
                : TechnicalEvaluationObjective.JdCoreKnowledge;
        }

        private static List<string> Canonicalize(
            IEnumerable<string> source,
            IReadOnlyCollection<string> bankSkills)
        {
            return Clean(source)
                .Select(skill => bankSkills.FirstOrDefault(bank =>
                    TechnicalQuestionMetadata.FuzzyMatches(skill, bank)))
                .Where(skill => !string.IsNullOrWhiteSpace(skill))
                .Select(skill => skill!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> Clean(IEnumerable<string> values)
        {
            return values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static TechnicalQuestionPlanBuildResult Failure(string code, string message) =>
            new(null, code, message);
    }
}
