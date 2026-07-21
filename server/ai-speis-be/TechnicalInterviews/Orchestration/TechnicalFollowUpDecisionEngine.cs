using ai_speis_be.Models.Enums;
using ai_speis_be.TechnicalInterviews.Rubrics;

namespace ai_speis_be.TechnicalInterviews.Orchestration
{
    public sealed record TechnicalDecisionOutcome(
        TechnicalInterviewDecision Decision,
        bool FinalizeMainQuestion,
        TechnicalAttemptType? NextAttemptType);

    public interface ITechnicalFollowUpDecisionEngine
    {
        TechnicalDecisionOutcome Resolve(
            TechnicalInterviewDecision aiDecision,
            int clarificationsUsed,
            int followUpsUsed,
            int completedMainQuestions,
            int targetMainQuestions,
            bool hasValidNextQuestion,
            TechnicalQuestionLimits limits);
    }

    public sealed class TechnicalFollowUpDecisionEngine : ITechnicalFollowUpDecisionEngine
    {
        public TechnicalDecisionOutcome Resolve(
            TechnicalInterviewDecision aiDecision,
            int clarificationsUsed,
            int followUpsUsed,
            int completedMainQuestions,
            int targetMainQuestions,
            bool hasValidNextQuestion,
            TechnicalQuestionLimits limits)
        {
            var totalSubQuestions = clarificationsUsed + followUpsUsed;
            if (hasValidNextQuestion && totalSubQuestions < limits.MaxTotalSubQuestionsPerMainQuestion)
            {
                if (aiDecision == TechnicalInterviewDecision.Clarification
                    && clarificationsUsed < limits.MaxClarificationsPerMainQuestion)
                {
                    return new TechnicalDecisionOutcome(
                        TechnicalInterviewDecision.Clarification,
                        false,
                        TechnicalAttemptType.Clarification);
                }

                if (aiDecision == TechnicalInterviewDecision.FollowUp
                    && followUpsUsed < limits.MaxFollowUpsPerMainQuestion)
                {
                    return new TechnicalDecisionOutcome(
                        TechnicalInterviewDecision.FollowUp,
                        false,
                        TechnicalAttemptType.FollowUp);
                }
            }

            var finalDecision = completedMainQuestions + 1 >= targetMainQuestions
                ? TechnicalInterviewDecision.EndInterview
                : TechnicalInterviewDecision.NextQuestion;

            return new TechnicalDecisionOutcome(finalDecision, true, null);
        }
    }
}
