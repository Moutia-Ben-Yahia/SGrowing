using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.AI;

public class AutonomousAgentExecutionTests
{
    private readonly SGrowingDbContext _db;
    private readonly Mock<IMcpServerRegistryService> _mcpMock;
    private readonly AutonomousAgentMissionService _service;

    public AutonomousAgentExecutionTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _mcpMock = new Mock<IMcpServerRegistryService>();

        _mcpMock.Setup(m => m.ExecuteMcpToolAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>()))
            .ReturnsAsync((Guid tenant, string tool, Dictionary<string, object> args) =>
                new McpToolExecutionResultDto(
                    ToolName: tool,
                    IsSuccess: true,
                    OutputPayload: new { result = $"Output for {tool}", count = 25 },
                    ErrorMessage: null,
                    TokensConsumed: 2500,
                    ExecutionTimeMs: 120.0
                ));

        _service = new AutonomousAgentMissionService(_db, _mcpMock.Object);
    }

    [Fact]
    public async Task CreateMission_DecomposesGoalIntoFiveMcpSteps()
    {
        var tenantId = Guid.NewGuid();

        var mission = await _service.CreateMissionAsync(
            tenantId,
            "Q4 B2B Sales Blitz",
            "Target retail merchants in Maghreb and close deals",
            "Human-in-the-Loop",
            50.0m);

        mission.Should().NotBeNull();
        mission.Steps.Should().HaveCount(5);

        // Verify the 5 MCP tools in execution order
        mission.Steps[0].ToolName.Should().Be("crm_search_leads");
        mission.Steps[1].ToolName.Should().Be("deliverability_run_placement_test");
        mission.Steps[2].ToolName.Should().Be("outbound_send_spintax_campaign");
        mission.Steps[3].ToolName.Should().Be("contracts_dispatch_for_signature");
        mission.Steps[4].ToolName.Should().Be("courier_generate_waybill");

        // First non-sensitive step executed on creation
        mission.Steps[0].Status.Should().Be(AgentMissionStepStatus.Completed);
        mission.Steps[0].TokensConsumed.Should().Be(2500);
        mission.Steps[0].CostUsd.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteMissionStep_InHumanInTheLoopMode_PausesOnSensitiveTool()
    {
        var tenantId = Guid.NewGuid();

        // 1. Create mission (step 0 runs)
        var mission = await _service.CreateMissionAsync(
            tenantId,
            "Maghreb Campaign",
            "Outreach blitz",
            "Human-in-the-Loop",
            50.0m);

        // 2. Execute step 1 (deliverability test - non-sensitive)
        var step1Result = await _service.ExecuteMissionStepAsync(mission.Id);
        step1Result.Should().BeTrue();

        var updated = await _db.AgentMissions.Include(m => m.Steps).FirstAsync(m => m.Id == mission.Id);
        updated.Steps[1].Status.Should().Be(AgentMissionStepStatus.Completed);

        // 3. Execute step 2 (outbound_send_spintax_campaign - sensitive action!)
        var step2Result = await _service.ExecuteMissionStepAsync(mission.Id);
        step2Result.Should().BeFalse(); // Paused for human gate

        updated = await _db.AgentMissions.Include(m => m.Steps).FirstAsync(m => m.Id == mission.Id);
        updated.Status.Should().Be(AgentMissionStatus.PausedForApproval);
        updated.Steps[2].Status.Should().Be(AgentMissionStepStatus.AwaitingApproval);
        updated.Steps[2].ApprovalRequired.Should().BeTrue();
        updated.ReasoningTrace.Should().Contain("requires human review");
    }

    [Fact]
    public async Task ApproveMissionStep_AuthorizesSensitiveToolAndResumesExecution()
    {
        var tenantId = Guid.NewGuid();

        var mission = await _service.CreateMissionAsync(
            tenantId,
            "Maghreb Campaign",
            "Outreach blitz",
            "Human-in-the-Loop",
            50.0m);

        // Run step 1
        await _service.ExecuteMissionStepAsync(mission.Id);
        // Step 2 pauses for approval
        await _service.ExecuteMissionStepAsync(mission.Id);

        var paused = await _db.AgentMissions.Include(m => m.Steps).FirstAsync(m => m.Id == mission.Id);
        var sensitiveStep = paused.Steps[2];

        // Supervisor approves
        var approved = await _service.ApproveMissionStepAsync(mission.Id, sensitiveStep.Id, "Head of Sales");
        approved.Should().BeTrue();

        // Now step execution succeeds
        var stepExecution = await _service.ExecuteMissionStepAsync(mission.Id);
        stepExecution.Should().BeTrue();

        var completedStep = await _db.AgentMissionSteps.FirstAsync(s => s.Id == sensitiveStep.Id);
        completedStep.Status.Should().Be(AgentMissionStepStatus.Completed);
        completedStep.ApprovedBy.Should().Be("Head of Sales");
    }

    [Fact]
    public async Task ExecuteMissionStep_WhenBudgetExceeded_HaltsWithGuardrailAlert()
    {
        var tenantId = Guid.NewGuid();

        // Set tight budget of $0.05
        var mission = await _service.CreateMissionAsync(
            tenantId,
            "Low Budget Test",
            "Run steps",
            "Auto-Approve",
            0.005m);

        // Set spent budget to exceed limit
        mission.SpentBudgetUsd = 0.01m;
        await _db.SaveChangesAsync();

        var result = await _service.ExecuteMissionStepAsync(mission.Id);
        result.Should().BeFalse();

        var halted = await _db.AgentMissions.FirstAsync(m => m.Id == mission.Id);
        halted.Status.Should().Be(AgentMissionStatus.Failed);
        halted.ReasoningTrace.Should().Contain("budget limit");
    }

    [Fact]
    public async Task ExecuteMissionStep_WhenMcpToolFails_HaltsMissionWithFailedStatus()
    {
        var tenantId = Guid.NewGuid();

        var mission = await _service.CreateMissionAsync(
            tenantId,
            "Failing Tool Test",
            "Should halt on error",
            "Auto-Approve",
            50.0m);

        // Configure MCP tool mock to fail on deliverability test (step 1)
        _mcpMock.Setup(m => m.ExecuteMcpToolAsync(
                It.IsAny<Guid>(),
                "deliverability_run_placement_test",
                It.IsAny<Dictionary<string, object>>()))
            .ReturnsAsync(new McpToolExecutionResultDto(
                ToolName: "deliverability_run_placement_test",
                IsSuccess: false,
                OutputPayload: null,
                ErrorMessage: "DNS MX lookup failed: host unreachable",
                TokensConsumed: 120,
                ExecutionTimeMs: 45.0
            ));

        var stepResult = await _service.ExecuteMissionStepAsync(mission.Id);
        stepResult.Should().BeFalse();

        var failedMission = await _db.AgentMissions.Include(m => m.Steps).FirstAsync(m => m.Id == mission.Id);
        failedMission.Status.Should().Be(AgentMissionStatus.Failed);
        failedMission.Steps[1].Status.Should().Be(AgentMissionStepStatus.Failed);
        failedMission.ReasoningTrace.Should().Contain("DNS MX lookup failed");

        // Subsequent execute call must NOT mark mission Completed
        var nextResult = await _service.ExecuteMissionStepAsync(mission.Id);
        nextResult.Should().BeFalse();
        failedMission = await _db.AgentMissions.FirstAsync(m => m.Id == mission.Id);
        failedMission.Status.Should().Be(AgentMissionStatus.Failed);
    }

    [Fact]
    public async Task ExecuteMissionStep_WhenInAutoApproveMode_ExecutesSensitiveToolsWithoutPausing()
    {
        var tenantId = Guid.NewGuid();

        // 1. Create mission with Auto-Approve autonomy mode
        var mission = await _service.CreateMissionAsync(
            tenantId,
            "Autonomous Outreach Campaign",
            "Auto pipeline execution",
            "Auto-Approve",
            100.0m);

        // Step 0 executed on creation (crm_search_leads)
        // Step 1: deliverability test
        var step1Result = await _service.ExecuteMissionStepAsync(mission.Id);
        step1Result.Should().BeTrue();

        // Step 2: outbound_send_spintax_campaign (sensitive, but autonomy is Auto-Approve!)
        var step2Result = await _service.ExecuteMissionStepAsync(mission.Id);
        step2Result.Should().BeTrue(); // Does NOT pause!

        var updated = await _db.AgentMissions.Include(m => m.Steps).FirstAsync(m => m.Id == mission.Id);
        updated.Status.Should().Be(AgentMissionStatus.Running);
        updated.Steps[2].Status.Should().Be(AgentMissionStepStatus.Completed);
    }

    [Fact]
    public async Task ApproveMissionStep_WhenMissionOrStepNotFound_ReturnsFalse()
    {
        var tenantId = Guid.NewGuid();
        var mission = await _service.CreateMissionAsync(
            tenantId,
            "Approval Guard Test",
            "Target goal",
            "Human-in-the-Loop",
            50.0m);

        // Non-existent mission
        var res1 = await _service.ApproveMissionStepAsync(Guid.NewGuid(), Guid.NewGuid(), "Admin");
        res1.Should().BeFalse();

        // Existing mission, non-existent step
        var res2 = await _service.ApproveMissionStepAsync(mission.Id, Guid.NewGuid(), "Admin");
        res2.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessActiveMissionsAsync_WhenActiveMissionsExist_ExecutesPendingStepsInBatch()
    {
        var tenantId = Guid.NewGuid();

        var mission1 = await _service.CreateMissionAsync(
            tenantId,
            "Batch Mission 1",
            "Goal 1",
            "Auto-Approve",
            50.0m);

        var mission2 = await _service.CreateMissionAsync(
            tenantId,
            "Batch Mission 2",
            "Goal 2",
            "Auto-Approve",
            50.0m);

        // Both missions are in Running status after creation step 0
        var processed = await _service.ProcessActiveMissionsAsync();

        processed.Should().Be(2);

        var updated1 = await _db.AgentMissions.Include(m => m.Steps).FirstAsync(m => m.Id == mission1.Id);
        var updated2 = await _db.AgentMissions.Include(m => m.Steps).FirstAsync(m => m.Id == mission2.Id);

        updated1.Steps[1].Status.Should().Be(AgentMissionStepStatus.Completed);
        updated2.Steps[1].Status.Should().Be(AgentMissionStepStatus.Completed);
    }
}
