using FluentAssertions;
using SGrowing.Api.Modules.AI.SwarmCoordination;
using Xunit;

namespace SGrowing.Tests.AI;

public class SwarmCoordinationTests
{
    private readonly SwarmCoordinationEngine _engine = new();

    [Fact]
    public void CreateMission_DecomposesObjectiveAcrossSpecializedPersonas()
    {
        // Arrange
        var request = new CreateMissionRequest(
            TenantId: "tenant_ai_swarm_01",
            Title: "Q4 Enterprise Expansion",
            HighLevelGoal: "Acquire 25 mid-market and enterprise accounts in DACH region"
        );

        // Act
        var mission = _engine.CreateMission(request);

        // Assert
        mission.Should().NotBeNull();
        mission.TenantId.Should().Be("tenant_ai_swarm_01");
        mission.Tasks.Should().HaveCount(5);
        mission.Tasks.Should().Contain(t => t.AssignedTo == AgentPersonaRole.LeadResearcher);
        mission.Tasks.Should().Contain(t => t.AssignedTo == AgentPersonaRole.OutreachCopywriter);
        mission.Tasks.Should().Contain(t => t.AssignedTo == AgentPersonaRole.ComplianceGuardian);
        mission.Tasks.Should().Contain(t => t.AssignedTo == AgentPersonaRole.AdOptimizer && t.RequiresHumanApproval);

        mission.Scratchpad.Should().ContainKey("mission_phase");
        mission.Scratchpad["mission_phase"].Should().Be("ExecutionPendingApproval");
    }

    [Fact]
    public void ApproveTask_UnlocksAwaitingApprovalTaskAndUpdatesMissionPhase()
    {
        // Arrange
        var mission = _engine.CreateMission(new CreateMissionRequest(
            TenantId: "tenant_ai_swarm_02",
            Title: "Holiday Ad & SDR Blitz",
            HighLevelGoal: "Maximize Black Friday conversions"
        ));

        var approvalTask = mission.Tasks.First(t => t.RequiresHumanApproval);
        approvalTask.Status.Should().Be(SwarmTaskStatus.AwaitingApproval);

        // Act
        var approvedMission = _engine.ApproveTask(new ApproveTaskRequest(
            TenantId: "tenant_ai_swarm_02",
            MissionId: mission.MissionId,
            TaskId: approvalTask.TaskId,
            Approved: true,
            ApproverId: "admin_chief_growth_officer"
        ));

        // Assert
        var updatedTask = approvedMission.Tasks.First(t => t.TaskId == approvalTask.TaskId);
        updatedTask.Status.Should().Be(SwarmTaskStatus.Completed);
        updatedTask.OutputArtifact.Should().Contain("[APPROVED by admin_chief_growth_officer]");
        approvedMission.Scratchpad["mission_phase"].Should().Be("MissionComplete");
        approvedMission.FinishedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void UpdateScratchpadAndGetTelemetry_TracksSharedMemoryState()
    {
        // Arrange
        var mission = _engine.CreateMission(new CreateMissionRequest(
            TenantId: "tenant_ai_swarm_03",
            Title: "SEO Inbound Scaler",
            HighLevelGoal: "Scale organic blog traffic"
        ));

        // Act 1: Update Scratchpad
        _engine.UpdateScratchpad(new UpdateScratchpadRequest(
            TenantId: "tenant_ai_swarm_03",
            MissionId: mission.MissionId,
            Key: "target_cluster",
            Value: "Growth OS vs HubSpot Alternatives"
        ));

        // Act 2: Telemetry
        var telemetry = _engine.GetTelemetry("tenant_ai_swarm_03", mission.MissionId);

        // Assert
        telemetry.TotalTasks.Should().Be(5);
        telemetry.CompletedTasks.Should().Be(4);
        telemetry.PendingApprovalCount.Should().Be(1);
        telemetry.Scratchpad["target_cluster"].Should().Be("Growth OS vs HubSpot Alternatives");
    }
}
