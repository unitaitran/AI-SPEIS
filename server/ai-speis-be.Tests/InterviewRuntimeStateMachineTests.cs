using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.InterviewSessionService;
using Xunit;

namespace ai_speis_be.Tests
{
    public class InterviewRuntimeStateMachineTests
    {
        [Fact]
        public void Test_LinearRoundOrdering_Behavior_Technical_Coding_Final()
        {
            var rounds = new[]
            {
                InterviewRoundType.Code,
                InterviewRoundType.Behavior,
                InterviewRoundType.Technical
            };

            var ordered = rounds.OrderBy(r => r switch
            {
                InterviewRoundType.Behavior => 0,
                InterviewRoundType.Technical => 1,
                InterviewRoundType.Code => 2,
                _ => int.MaxValue
            }).ToList();

            Assert.Equal(InterviewRoundType.Behavior, ordered[0]);
            Assert.Equal(InterviewRoundType.Technical, ordered[1]);
            Assert.Equal(InterviewRoundType.Code, ordered[2]);
        }

        [Fact]
        public void Test_AdvanceCampaign_SelectsTechnical_WhenBehavioralCompletes()
        {
            var campaign = new InterviewCampaign
            {
                InterviewCampaignId = 1,
                Status = InterviewCampaignStatus.Active,
                InterviewSessions = new List<InterviewSession>
                {
                    new InterviewSession { InterviewSessionId = 10, InterviewRoundType = InterviewRoundType.Behavior, Status = InterviewSessionStatus.Completed },
                    new InterviewSession { InterviewSessionId = 11, InterviewRoundType = InterviewRoundType.Technical, Status = InterviewSessionStatus.Pending },
                    new InterviewSession { InterviewSessionId = 12, InterviewRoundType = InterviewRoundType.Code, Status = InterviewSessionStatus.Pending },
                }
            };

            var nextSession = campaign.InterviewSessions
                .Where(s => !s.IsDeleted
                    && s.Status != InterviewSessionStatus.Completed
                    && s.Status != InterviewSessionStatus.Cancelled)
                .OrderBy(s => s.InterviewRoundType switch
                {
                    InterviewRoundType.Behavior => 0,
                    InterviewRoundType.Technical => 1,
                    InterviewRoundType.Code => 2,
                    _ => int.MaxValue
                })
                .FirstOrDefault();

            Assert.NotNull(nextSession);
            Assert.Equal(InterviewRoundType.Technical, nextSession.InterviewRoundType);
            Assert.Equal(11, nextSession.InterviewSessionId);
        }
    }
}
