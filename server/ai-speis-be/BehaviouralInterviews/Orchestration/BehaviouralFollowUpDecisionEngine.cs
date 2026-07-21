using ai_speis_be.Models.Enums;
using ai_speis_be.BehaviouralInterviews.Rubrics;

namespace ai_speis_be.BehaviouralInterviews.Orchestration
{
    public sealed record BehaviouralDecisionOutcome(
        BehaviourResolvedAction Decision,
        bool FinalizeMainQuestion,
        BehaviourQuestionType? NextAttemptType);

    public interface IBehaviouralFollowUpDecisionEngine
    {
        BehaviouralDecisionOutcome Resolve(
            decimal? baseScore,
            int clarificationsUsed,
            int followUpsUsed,
            bool hasClarificationQuestion,
            bool hasFollowUp1,
            bool hasFollowUp2,
            BehaviouralQuestionLimits limits);
    }

    /// <summary>
    /// Quyết định câu phụ theo ngưỡng điểm câu chính (Evaluation Framework — Level 1, thang 0–10):
    ///   <3 → 01 Clarification (điểm cuối = 75% × điểm clarification);
    ///   3–<5 → 02 Follow-up; 5–<8 → 01 Follow-up; ≥8 → chuyển câu chính tiếp theo.
    /// Sau clarification, baseScore = 75% × điểm clarification rồi áp lại các ngưỡng;
    /// nếu vẫn <3 thì chốt câu (pool snapshot cố định, không đổi câu khác được).
    /// Câu phụ luôn lấy từ snapshot soạn sẵn, vì vậy chỉ cho phép action khi câu tương ứng tồn tại.
    /// </summary>
    public sealed class BehaviouralFollowUpDecisionEngine : IBehaviouralFollowUpDecisionEngine
    {
        private const decimal ClarificationThreshold = 3m;
        private const decimal TwoFollowUpsThreshold = 5m;
        private const decimal OneFollowUpThreshold = 8m;

        public BehaviouralDecisionOutcome Resolve(
            decimal? baseScore,
            int clarificationsUsed,
            int followUpsUsed,
            bool hasClarificationQuestion,
            bool hasFollowUp1,
            bool hasFollowUp2,
            BehaviouralQuestionLimits limits)
        {
            var totalSubQuestions = clarificationsUsed + followUpsUsed;
            if (baseScore is null || totalSubQuestions >= limits.MaxTotalSubQuestionsPerMainQuestion)
            {
                return Next();
            }

            var score = baseScore.Value;

            if (score < ClarificationThreshold)
            {
                // Chưa clarify → hỏi clarification; đã clarify mà vẫn <3 → chốt câu.
                if (clarificationsUsed < limits.MaxClarificationsPerMainQuestion && hasClarificationQuestion)
                {
                    return new BehaviouralDecisionOutcome(
                        BehaviourResolvedAction.Clarification,
                        false,
                        BehaviourQuestionType.Clarification);
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
                    return new BehaviouralDecisionOutcome(
                        BehaviourResolvedAction.FollowUp1,
                        false,
                        BehaviourQuestionType.FollowUp1);
                }

                if (followUpsUsed == 1 && hasFollowUp2)
                {
                    return new BehaviouralDecisionOutcome(
                        BehaviourResolvedAction.FollowUp2,
                        false,
                        BehaviourQuestionType.FollowUp2);
                }
            }

            return Next();
        }

        private static BehaviouralDecisionOutcome Next()
        {
            return new BehaviouralDecisionOutcome(BehaviourResolvedAction.NextMainQuestion, true, null);
        }
    }
}
