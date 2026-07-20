using System.Security.Claims;
using System.Text.Json;
using ai_speis_be.Controllers;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Services.InterviewSessionService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ai_speis_be.Tests.Controllers;

public sealed class InterviewSessionControllerConflictTests
{
    private readonly Mock<IInterviewSessionService> _service = new();

    [Fact]
    public async Task CreateSession_ActiveSession_ReturnsStructuredConflictData()
    {
        var campaign = CreateActiveCampaign();
        _service
            .Setup(service => service.CreateSessionsAsync(7, It.IsAny<CreateInterviewSessionRequest>()))
            .ReturnsAsync((false, "Wording is not a contract.", (InterviewCampaignDto?)null));
        _service
            .Setup(service => service.GetActiveCampaignAsync(7))
            .ReturnsAsync(campaign);
        var controller = CreateController();

        var result = await controller.CreateSession(new CreateInterviewSessionRequest());

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var json = JsonSerializer.Serialize(conflict.Value);
        Assert.Contains("ACTIVE_INTERVIEW_SESSION_EXISTS", json);
        Assert.Contains("\"CampaignId\":8", json);
        Assert.Contains("\"SessionId\":17", json);
        Assert.Contains("\"CanResume\":true", json);
        Assert.DoesNotContain("Wording is not a contract", json);
    }

    [Fact]
    public async Task GetActiveCampaign_NoCampaign_ReturnsNoContent()
    {
        _service
            .Setup(service => service.GetActiveCampaignAsync(7))
            .ReturnsAsync((InterviewCampaignDto?)null);
        var controller = CreateController();

        var result = await controller.GetActiveCampaign();

        Assert.IsType<NoContentResult>(result);
    }

    private InterviewSessionController CreateController()
    {
        var controller = new InterviewSessionController(_service.Object);
        var identity = new ClaimsIdentity([new Claim("UserId", "7")], "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
        return controller;
    }

    private static InterviewCampaignDto CreateActiveCampaign() => new()
    {
        InterviewCampaignId = 8,
        Status = "Active",
        StartedAt = new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc),
        Sessions =
        [
            new InterviewSessionDto
            {
                InterviewSessionId = 17,
                InterviewCampaignId = 8,
                InterviewRoundType = "Technical",
                Status = "Active",
                CompletedQuestionCount = 2,
                CreatedAt = new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc)
            }
        ]
    };
}
