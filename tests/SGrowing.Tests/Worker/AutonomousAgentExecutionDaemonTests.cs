using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class AutonomousAgentExecutionDaemonTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    private Mock<IMcpServerRegistryService> CreateMockMcp()
    {
        var mock = new Mock<IMcpServerRegistryService>();
        mock.Setup(m => m.ExecuteMcpToolAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .ReturnsAsync(new McpToolExecutionResultDto(
                ToolName: "web_search",
                IsSuccess: true,
                OutputPayload: new { matches = 5, topDomain = "targetcorp.com" },
                ErrorMessage: null,
                TokensConsumed: 320,
                ExecutionTimeMs: 145.0
            ));
        return mock;
    }

    [Fact]
    public async Task ProcessActiveMissionsAsync_WhenNoActiveMissions_ReturnsZero()
    {
        using var db = CreateDbContext();
        var mockMcp = CreateMockMcp();

        var processed = await AutonomousAgentExecutionDaemon.ProcessActiveMissionsAsync(db, mockMcp.Object);

        processed.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteMissionStepAsync_WhenNoSteps_DecomposesGoalAndStartsRunning()
    {
        using var db = CreateDbContext();
        var mockMcp = CreateMockMcp();
        var tenantId = Guid.NewGuid();

        var mission = AgentMission.Create(tenantId, "Prospect FinTech Accounts", "Research and enrich 50 FinTech founders");
        db.AgentMissions.Add(mission);
        await db.SaveChangesAsync();

        var executed = await AutonomousAgentExecutionDaemon.ExecuteMissionStepAsync(db, mockMcp.Object, mission.Id);

        executed.Should().BeTrue();

        var updated = await db.AgentMissions.Include(m => m.Steps).FirstOrDefaultAsync(m => m.Id == mission.Id);
        updated.Should().NotBeNull();
        updated!.Steps.Should().NotBeEmpty();
        updated.Status.Should().Be(AgentMissionStatus.Running);
    }

    [Fact]
    public async Task ExecuteMissionStepAsync_WhenPendingToolExecution_ExecutesToolAndTracksTokensAndCost()
    {
        using var db = CreateDbContext();
        var mockMcp = CreateMockMcp();
        var tenantId = Guid.NewGuid();

        var mission = AgentMission.Create(tenantId, "Enrich Enterprise Leads", "Find decision makers", autonomyMode: "Auto-Approve");
        var step1 = AgentMissionStep.Create(mission.Id, 1, "prospect_enrich_contact", "{\"email\":\"ceo@acme.com\"}", "Extract corporate bio");
        mission.Steps.Add(step1);
        mission.Status = AgentMissionStatus.Running;

        db.AgentMissions.Add(mission);
        await db.SaveChangesAsync();

        var executed = await AutonomousAgentExecutionDaemon.ExecuteMissionStepAsync(db, mockMcp.Object, mission.Id);

        executed.Should().BeTrue();

        var updatedStep = await db.AgentMissionSteps.FindAsync(step1.Id);
        updatedStep.Should().NotBeNull();
        updatedStep!.Status.Should().Be(AgentMissionStepStatus.Completed);
        updatedStep.TokensConsumed.Should().Be(320);
        updatedStep.CostUsd.Should().BeGreaterThan(0);
        updatedStep.ExecutedAtUtc.Should().NotBeNull();

        var updatedMission = await db.AgentMissions.FindAsync(mission.Id);
        updatedMission!.TokensConsumed.Should().Be(320);
        updatedMission.Status.Should().Be(AgentMissionStatus.Completed); // All steps completed
    }

    [Fact]
    public async Task ExecuteMissionStepAsync_WhenBudgetExceeded_HaltsMissionWithFailedStatus()
    {
        using var db = CreateDbContext();
        var mockMcp = CreateMockMcp();
        var tenantId = Guid.NewGuid();

        var mission = AgentMission.Create(tenantId, "Budget Limited Mission", "Execute heavy data extraction", budgetUsd: 10.0m);
        mission.SpentBudgetUsd = 10.5m; // Exceeded
        var step = AgentMissionStep.Create(mission.Id, 1, "scrape_deep_web", "{}", "Scraping page");
        mission.Steps.Add(step);

        db.AgentMissions.Add(mission);
        await db.SaveChangesAsync();

        var executed = await AutonomousAgentExecutionDaemon.ExecuteMissionStepAsync(db, mockMcp.Object, mission.Id);

        executed.Should().BeFalse();

        var updated = await db.AgentMissions.FindAsync(mission.Id);
        updated!.Status.Should().Be(AgentMissionStatus.Failed);
        updated.ReasoningTrace.Should().Contain("Mission budget limit");
    }

    [Fact]
    public async Task ExecuteMissionStepAsync_WhenSensitiveActionInHitlMode_PausesForHumanApproval()
    {
        using var db = CreateDbContext();
        var mockMcp = CreateMockMcp();
        var tenantId = Guid.NewGuid();

        var mission = AgentMission.Create(tenantId, "Outreach Blast", "Send cold emails", autonomyMode: "Human-in-the-Loop");
        var sensitiveStep = AgentMissionStep.Create(mission.Id, 1, "outbound_send_spintax_campaign", "{}", "Dispatching emails to 500 prospects");
        mission.Steps.Add(sensitiveStep);

        db.AgentMissions.Add(mission);
        await db.SaveChangesAsync();

        var executed = await AutonomousAgentExecutionDaemon.ExecuteMissionStepAsync(db, mockMcp.Object, mission.Id);

        executed.Should().BeFalse(); // Paused waiting for approval

        var updatedMission = await db.AgentMissions.FindAsync(mission.Id);
        updatedMission!.Status.Should().Be(AgentMissionStatus.PausedForApproval);

        var updatedStep = await db.AgentMissionSteps.FindAsync(sensitiveStep.Id);
        updatedStep!.Status.Should().Be(AgentMissionStepStatus.AwaitingApproval);
        updatedStep.ApprovalRequired.Should().BeTrue();
    }

    [Fact]
    public async Task ApproveMissionStepAsync_WhenApproved_ClearsGateAndEnablesExecution()
    {
        using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();

        var mission = AgentMission.Create(tenantId, "Outreach Blast", "Send cold emails", autonomyMode: "Human-in-the-Loop");
        mission.Status = AgentMissionStatus.PausedForApproval;
        var step = AgentMissionStep.Create(mission.Id, 1, "outbound_send_spintax_campaign", "{}", "Dispatching emails");
        step.Status = AgentMissionStepStatus.AwaitingApproval;
        step.ApprovalRequired = true;
        mission.Steps.Add(step);

        db.AgentMissions.Add(mission);
        await db.SaveChangesAsync();

        var approved = await AutonomousAgentExecutionDaemon.ApproveMissionStepAsync(db, mission.Id, step.Id, "chief_revenue_officer@sgrowing.io");

        approved.Should().BeTrue();

        var updatedStep = await db.AgentMissionSteps.FindAsync(step.Id);
        updatedStep!.ApprovedBy.Should().Be("chief_revenue_officer@sgrowing.io");
        updatedStep.Status.Should().Be(AgentMissionStepStatus.Pending);

        var updatedMission = await db.AgentMissions.FindAsync(mission.Id);
        updatedMission!.Status.Should().Be(AgentMissionStatus.Running);
    }
}
