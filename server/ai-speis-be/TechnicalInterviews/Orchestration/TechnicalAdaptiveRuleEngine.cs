using ai_speis_be.Models.Enums;
using ai_speis_be.TechnicalInterviews.Configuration;

namespace ai_speis_be.TechnicalInterviews.Orchestration
{
    public sealed record TechnicalAdaptiveRuleInput(
        TechnicalAttemptType CurrentAttemptType,
        decimal InitialMainScore,
        int RequiredClarificationCount,
        int CompletedClarificationCount,
        int RequiredFollowUpCount,
        int CompletedFollowUpCount,
        int CompletedMainQuestionCount,
        int TargetMainQuestionCount,
        bool IsReliabilityFollowUpRequired,
        bool IsCurrentReliabilityFollowUp);

    public sealed record TechnicalAdaptiveRuleOutcome(
        TechnicalInterviewDecision Decision,
        bool FinalizeMainQuestion,
        TechnicalAttemptType? NextAttemptType,
        TechnicalQuestionGenerationReason? NextGenerationReason,
        int RequiredClarificationCount,
        int RequiredFollowUpCount,
        TechnicalAdaptiveStage Stage);

    public interface ITechnicalAdaptiveRuleEngine
    {
        TechnicalAdaptiveRuleOutcome Resolve(TechnicalAdaptiveRuleInput input);
    }

    public sealed class TechnicalAdaptiveRuleEngine : ITechnicalAdaptiveRuleEngine
    {
        private readonly TechnicalInterviewOptions _options;

        public TechnicalAdaptiveRuleEngine(TechnicalInterviewOptions options)
        {
            _options = options;
        }

        public TechnicalAdaptiveRuleOutcome Resolve(TechnicalAdaptiveRuleInput input)
        {
            if (input.CurrentAttemptType == TechnicalAttemptType.Main)
            {
                if (input.InitialMainScore < 3m)
                {
                    return SubQuestion(
                        TechnicalAttemptType.Clarification,
                        TechnicalQuestionGenerationReason.AdaptiveScoreRule,
                        requiredClarifications: 1,
                        requiredFollowUps: 0,
                        TechnicalAdaptiveStage.AwaitingClarification);
                }

                if (input.InitialMainScore < 5m)
                {
                    return SubQuestion(
                        TechnicalAttemptType.FollowUp,
                        TechnicalQuestionGenerationReason.AdaptiveScoreRule,
                        requiredClarifications: 0,
                        requiredFollowUps: 2,
                        TechnicalAdaptiveStage.AwaitingFollowUp);
                }

                if (input.InitialMainScore < 8m)
                {
                    return SubQuestion(
                        TechnicalAttemptType.FollowUp,
                        TechnicalQuestionGenerationReason.AdaptiveScoreRule,
                        requiredClarifications: 0,
                        requiredFollowUps: 1,
                        TechnicalAdaptiveStage.AwaitingFollowUp);
                }
            }
            else if (input.CurrentAttemptType == TechnicalAttemptType.Clarification
                && !_options.ClarificationEndsMainQuestion)
            {
                return SubQuestion(
                    TechnicalAttemptType.FollowUp,
                    TechnicalQuestionGenerationReason.AdaptiveScoreRule,
                    input.RequiredClarificationCount,
                    Math.Max(1, input.RequiredFollowUpCount),
                    TechnicalAdaptiveStage.AwaitingFollowUp);
            }
            else if (input.CurrentAttemptType == TechnicalAttemptType.FollowUp
                && !input.IsCurrentReliabilityFollowUp
                && input.CompletedFollowUpCount + 1 < input.RequiredFollowUpCount)
            {
                return SubQuestion(
                    TechnicalAttemptType.FollowUp,
                    TechnicalQuestionGenerationReason.AdaptiveScoreRule,
                    input.RequiredClarificationCount,
                    input.RequiredFollowUpCount,
                    TechnicalAdaptiveStage.AwaitingFollowUp);
            }

            if (_options.ReliabilityFollowUpEnabled
                && input.IsReliabilityFollowUpRequired
                && _options.ReliabilityFollowUpLimit > 0)
            {
                return SubQuestion(
                    TechnicalAttemptType.FollowUp,
                    TechnicalQuestionGenerationReason.ReliabilityMinimum,
                    input.RequiredClarificationCount,
                    input.RequiredFollowUpCount,
                    TechnicalAdaptiveStage.AwaitingReliabilityFollowUp);
            }

            var decision = input.CompletedMainQuestionCount + 1 >= input.TargetMainQuestionCount
                ? TechnicalInterviewDecision.EndInterview
                : TechnicalInterviewDecision.NextQuestion;
            return new TechnicalAdaptiveRuleOutcome(
                decision,
                true,
                null,
                null,
                input.RequiredClarificationCount,
                input.RequiredFollowUpCount,
                TechnicalAdaptiveStage.Finalized);
        }

        private static TechnicalAdaptiveRuleOutcome SubQuestion(
            TechnicalAttemptType type,
            TechnicalQuestionGenerationReason reason,
            int requiredClarifications,
            int requiredFollowUps,
            TechnicalAdaptiveStage stage)
        {
            return new TechnicalAdaptiveRuleOutcome(
                type == TechnicalAttemptType.Clarification
                    ? TechnicalInterviewDecision.Clarification
                    : TechnicalInterviewDecision.FollowUp,
                false,
                type,
                reason,
                requiredClarifications,
                requiredFollowUps,
                stage);
        }
    }
}
