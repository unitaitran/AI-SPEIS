using Xunit;
using ai_speis_be.Models.Enums;
using ai_speis_be.BehaviouralInterviews.Orchestration;
using ai_speis_be.BehaviouralInterviews.Rubrics;
using ai_speis_be.BehaviouralInterviews.Scoring;

namespace ai_speis_be.Tests.Services
{
    /// <summary>
    /// Ngưỡng Level 1 của Evaluation Framework (thang 0–10):
    /// &lt;3 → Clarification; 3–&lt;5 → 2 Follow-up; 5–&lt;8 → 1 Follow-up; ≥8 → Next.
    /// </summary>
    public class BehaviouralFollowUpDecisionEngineTests
    {
        private readonly BehaviouralFollowUpDecisionEngine _sut = new();

        private static BehaviouralQuestionLimits Limits => new()
        {
            MaxClarificationsPerMainQuestion = 1,
            MaxFollowUpsPerMainQuestion = 2,
            MaxTotalSubQuestionsPerMainQuestion = 3
        };

        private BehaviouralDecisionOutcome Resolve(
            decimal? score,
            int clarificationsUsed = 0,
            int followUpsUsed = 0,
            bool hasClarification = true,
            bool hasFollowUp1 = true,
            bool hasFollowUp2 = true)
        {
            return _sut.Resolve(
                score, clarificationsUsed, followUpsUsed,
                hasClarification, hasFollowUp1, hasFollowUp2, Limits);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2.99)]
        public void ScoreBelow3_AsksClarification(decimal score)
        {
            var outcome = Resolve(score);

            Assert.Equal(BehaviourResolvedAction.Clarification, outcome.Decision);
            Assert.False(outcome.FinalizeMainQuestion);
        }

        [Fact]
        public void ScoreBelow3_AfterClarification_FinalizesQuestion()
        {
            // 75% × clarification vẫn <3 → không đổi câu được (pool snapshot) → chốt
            var outcome = Resolve(2.5m, clarificationsUsed: 1);

            Assert.Equal(BehaviourResolvedAction.NextMainQuestion, outcome.Decision);
            Assert.True(outcome.FinalizeMainQuestion);
        }

        [Theory]
        [InlineData(3, 0, "FollowUp1")]
        [InlineData(4.99, 0, "FollowUp1")]
        [InlineData(3, 1, "FollowUp2")]
        public void ScoreBetween3And5_AsksTwoFollowUps(decimal score, int followUpsUsed, string expected)
        {
            var outcome = Resolve(score, followUpsUsed: followUpsUsed);

            Assert.Equal(expected, outcome.Decision.ToString());
        }

        [Theory]
        [InlineData(5)]
        [InlineData(7.99)]
        public void ScoreBetween5And8_AsksExactlyOneFollowUp(decimal score)
        {
            var first = Resolve(score);
            Assert.Equal(BehaviourResolvedAction.FollowUp1, first.Decision);

            var second = Resolve(score, followUpsUsed: 1);
            Assert.Equal(BehaviourResolvedAction.NextMainQuestion, second.Decision);
        }

        [Theory]
        [InlineData(8)]
        [InlineData(10)]
        public void ScoreAtLeast8_MovesToNextMainQuestion(decimal score)
        {
            var outcome = Resolve(score);

            Assert.Equal(BehaviourResolvedAction.NextMainQuestion, outcome.Decision);
            Assert.True(outcome.FinalizeMainQuestion);
        }

        [Fact]
        public void NullScore_MovesToNextMainQuestion()
        {
            var outcome = Resolve(null);

            Assert.Equal(BehaviourResolvedAction.NextMainQuestion, outcome.Decision);
        }

        [Fact]
        public void MissingSnapshotQuestions_SkipsSubQuestion()
        {
            Assert.Equal(
                BehaviourResolvedAction.NextMainQuestion,
                Resolve(2m, hasClarification: false).Decision);

            Assert.Equal(
                BehaviourResolvedAction.NextMainQuestion,
                Resolve(4m, hasFollowUp1: false).Decision);
        }

        [Fact]
        public void SubQuestionBudgetExhausted_FinalizesQuestion()
        {
            var outcome = Resolve(4m, clarificationsUsed: 1, followUpsUsed: 2);

            Assert.Equal(BehaviourResolvedAction.NextMainQuestion, outcome.Decision);
        }
    }

    /// <summary>
    /// Công thức gộp điểm Level 1: clarification → 75% × điểm clarification;
    /// mỗi follow-up bonus = FU/10 (≤ +1), tổng ≤ +2; kết quả ≤ 10.
    /// </summary>
    public class BehaviouralCombineScoreTests
    {
        private readonly BehaviouralRubricScoringService _sut = new();

        [Fact]
        public void MainOnly_ReturnsMainScore()
        {
            Assert.Equal(8.5m, _sut.CombineMainQuestionScore(8.5m, null, Array.Empty<decimal>()));
        }

        [Fact]
        public void WithClarification_Returns75PercentOfClarification()
        {
            // Main 2.0 (<3) → hỏi clarification, điểm cuối = 75% × 6.0 = 4.5
            Assert.Equal(4.5m, _sut.CombineMainQuestionScore(2.0m, 6.0m, Array.Empty<decimal>()));
        }

        [Fact]
        public void FollowUp_AddsAtMostOnePointEach()
        {
            // Main 6.0 + FU 8.0/10 = 6.8
            Assert.Equal(6.8m, _sut.CombineMainQuestionScore(6.0m, null, new[] { 8.0m }));

            // FU điểm tuyệt đối 10 → bonus đúng +1
            Assert.Equal(7.0m, _sut.CombineMainQuestionScore(6.0m, null, new[] { 10.0m }));
        }

        [Fact]
        public void TwoFollowUps_TotalBonusCappedAtTwo()
        {
            // Main 4.0 + FU1 10/10 + FU2 10/10 = 4 + 1 + 1 = 6 (chạm trần +2)
            Assert.Equal(6.0m, _sut.CombineMainQuestionScore(4.0m, null, new[] { 10.0m, 10.0m }));
        }

        [Fact]
        public void FinalScore_NeverExceedsTen()
        {
            Assert.Equal(10.0m, _sut.CombineMainQuestionScore(9.5m, null, new[] { 10.0m }));
        }

        [Fact]
        public void ClarificationWithFollowUps_UsesDiscountedBase()
        {
            // 75% × 6.0 = 4.5, cộng FU 5.0/10 = 0.5 → 5.0
            Assert.Equal(5.0m, _sut.CombineMainQuestionScore(2.0m, 6.0m, new[] { 5.0m }));
        }

        [Fact]
        public void NoScores_ReturnsZero()
        {
            Assert.Equal(0m, _sut.CombineMainQuestionScore(null, null, Array.Empty<decimal>()));
        }
    }
}
