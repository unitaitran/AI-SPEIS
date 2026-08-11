using ai_speis_be.Models.Enums;
using ai_speis_be.TechnicalInterviews.Rubrics;

namespace ai_speis_be.TechnicalInterviews.Orchestration
{
    public sealed record TechnicalDecisionOutcome(
        TechnicalInterviewDecision Decision,
        bool FinalizeMainQuestion,
        TechnicalSessionQuestionType? NextQuestionType);

    public interface ITechnicalFollowUpDecisionEngine
    {
        TechnicalDecisionOutcome Resolve(
            decimal? baseScore,
            int clarificationsUsed,
            int followUpsUsed,
            bool hasClarificationQuestion,
            bool hasFollowUp1,
            bool hasFollowUp2,
            TechnicalQuestionLimits limits);
    }

    public sealed class TechnicalFollowUpDecisionEngine : ITechnicalFollowUpDecisionEngine
    {
        private const decimal ClarificationThreshold = 3m;
        private const decimal TwoFollowUpsThreshold = 6m;
        private const decimal OneFollowUpThreshold = 8m;

        public TechnicalDecisionOutcome Resolve(
            decimal? baseScore,
            int clarificationsUsed,
            int followUpsUsed,
            bool hasClarificationQuestion,
            bool hasFollowUp1,
            bool hasFollowUp2,
            TechnicalQuestionLimits limits)
        {
            var totalSubQuestions = clarificationsUsed + followUpsUsed;
            if (baseScore is null || totalSubQuestions >= limits.MaxTotalSubQuestionsPerMainQuestion)
            {
                return Next();
            }

            var score = baseScore.Value;

            if (score < ClarificationThreshold)
            {
                if (clarificationsUsed < limits.MaxClarificationsPerMainQuestion && hasClarificationQuestion)
                {
                    return new TechnicalDecisionOutcome(
                        TechnicalInterviewDecision.Clarification,
                        false,
                        TechnicalSessionQuestionType.Clarification);
                }

                return Next();
            }

            if (score >= OneFollowUpThreshold)
            {
                return Next();
            }

            var desiredFollowUps = score < TwoFollowUpsThreshold ? 2 : 1;
            desiredFollowUps = Math.Min(desiredFollowUps, limits.MaxFollowUpsPerMainQuestion);

            if (followUpsUsed < desiredFollowUps)
            {
                if (followUpsUsed == 0 && hasFollowUp1)
                {
                    return new TechnicalDecisionOutcome(
                        TechnicalInterviewDecision.FollowUp,
                        false,
                        TechnicalSessionQuestionType.FollowUp);
                }

                if (followUpsUsed == 1 && hasFollowUp2)
                {
                    return new TechnicalDecisionOutcome(
                        TechnicalInterviewDecision.FollowUp,
                        false,
                        TechnicalSessionQuestionType.FollowUp);
                }
            }

            return Next();
        }

        private static TechnicalDecisionOutcome Next()
        {
            return new TechnicalDecisionOutcome(TechnicalInterviewDecision.NextQuestion, true, null);
        }
    }
}
