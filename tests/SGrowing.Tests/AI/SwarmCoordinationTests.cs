using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
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

    [Fact]
    public void ApproveTask_WhenRejected_SetsRejectedStatusAndIncludesReason()
    {
        var mission = _engine.CreateMission(new CreateMissionRequest(
            TenantId: "tenant_reject_01",
            Title: "Budget Allocation",
            HighLevelGoal: "Spend test"
        ));

        var approvalTask = mission.Tasks.First(t => t.RequiresHumanApproval);

        var rejectedPlan = _engine.ApproveTask(new ApproveTaskRequest(
            TenantId: "tenant_reject_01",
            MissionId: mission.MissionId,
            TaskId: approvalTask.TaskId,
            Approved: false,
            ApproverId: "supervisor_alice",
            Reason: "Exceeds daily risk tolerance"
        ));

        var updatedTask = rejectedPlan.Tasks.First(t => t.TaskId == approvalTask.TaskId);
        updatedTask.Status.Should().Be(SwarmTaskStatus.Rejected);
        updatedTask.OutputArtifact.Should().Contain("REJECTED by supervisor_alice");
        updatedTask.OutputArtifact.Should().Contain("Exceeds daily risk tolerance");
        rejectedPlan.FinishedAtUtc.Should().NotBeNull();
        rejectedPlan.Scratchpad["mission_phase"].Should().Be("MissionComplete");
    }

    [Fact]
    public void ListMissions_FiltersByTenantIdAndOrdersByStartTime()
    {
        var tenantA = "tenant_list_A";
        var tenantB = "tenant_list_B";

        var misA1 = _engine.CreateMission(new CreateMissionRequest(tenantA, "Mission A1", "Goal 1"));
        var misA2 = _engine.CreateMission(new CreateMissionRequest(tenantA, "Mission A2", "Goal 2"));
        var misB1 = _engine.CreateMission(new CreateMissionRequest(tenantB, "Mission B1", "Goal B"));

        var listA = _engine.ListMissions(tenantA);
        var listB = _engine.ListMissions(tenantB);

        listA.Should().HaveCount(2);
        listA.Should().AllSatisfy(m => m.TenantId.Should().Be(tenantA));
        listB.Should().HaveCount(1);
        listB.First().Title.Should().Be("Mission B1");
    }

    [Fact]
    public void GetMission_NonExistentMission_ReturnsNull()
    {
        var result = _engine.GetMission("tenant_missing", "mis_nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public void ApproveTask_InvalidMissionOrTask_ThrowsKeyNotFoundException()
    {
        var mission = _engine.CreateMission(new CreateMissionRequest("tenant_err", "Error Test", "Goal"));

        // 1. Invalid mission
        var actMissingMission = () => _engine.ApproveTask(new ApproveTaskRequest(
            "tenant_err", "mis_invalid", "tsk_1", true, "admin"));
        actMissingMission.Should().Throw<KeyNotFoundException>()
            .WithMessage("*Mission 'mis_invalid' not found*");

        // 2. Invalid task
        var actMissingTask = () => _engine.ApproveTask(new ApproveTaskRequest(
            "tenant_err", mission.MissionId, "tsk_invalid", true, "admin"));
        actMissingTask.Should().Throw<KeyNotFoundException>()
            .WithMessage("*Task 'tsk_invalid' not found*");
    }

    [Fact]
    public void UpdateScratchpadAndTelemetry_InvalidMission_ThrowsKeyNotFoundException()
    {
        var actScratchpad = () => _engine.UpdateScratchpad(new UpdateScratchpadRequest(
            "tenant_err", "mis_fake", "key", "val"));
        actScratchpad.Should().Throw<KeyNotFoundException>();

        var actTelemetry = () => _engine.GetTelemetry("tenant_err", "mis_fake");
        actTelemetry.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void SwarmCoordinationEngine_WithDbContext_MissionsTasksAndScratchpadSurviveRestart()
    {
        // Arrange
        var dbName = $"SwarmCoordinationDurability_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        const string tenantId = "tenant_swarm_durable_01";
        string missionId;

        // Instance 1: Create mission, update scratchpad, approve a task
        using (var db1 = new SGrowingDbContext(options))
        {
            var engine1 = new SwarmCoordinationEngine(db1);

            var created = engine1.CreateMission(new CreateMissionRequest(
                TenantId: tenantId,
                Title: "Autonomous Expansion Mission",
                HighLevelGoal: "Scale multi-channel B2B leads"
            ));

            missionId = created.MissionId;

            // Update scratchpad
            engine1.UpdateScratchpad(new UpdateScratchpadRequest(
                TenantId: tenantId,
                MissionId: missionId,
                Key: "custom_funnel_stage",
                Value: "HighTouchNurture"
            ));

            // Approve task
            var approvalTask = created.Tasks.First(t => t.RequiresHumanApproval);
            engine1.ApproveTask(new ApproveTaskRequest(
                TenantId: tenantId,
                MissionId: missionId,
                TaskId: approvalTask.TaskId,
                Approved: true,
                ApproverId: "cgo_approver_99"
            ));
        }

        // Instance 2: Fresh engine instance connected to same DB (simulates restart)
        using (var db2 = new SGrowingDbContext(options))
        {
            var engine2 = new SwarmCoordinationEngine(db2);

            // Mission retrieval
            var retrieved = engine2.GetMission(tenantId, missionId);
            retrieved.Should().NotBeNull();
            retrieved!.MissionId.Should().Be(missionId);
            retrieved.Title.Should().Be("Autonomous Expansion Mission");
            retrieved.Scratchpad.Should().ContainKey("custom_funnel_stage");
            retrieved.Scratchpad["custom_funnel_stage"].Should().Be("HighTouchNurture");
            retrieved.Scratchpad["last_approved_task"].Should().Be("tsk_5_dispatch");

            var approvedTask = retrieved.Tasks.First(t => t.TaskId == "tsk_5_dispatch");
            approvedTask.Status.Should().Be(SwarmTaskStatus.Completed);
            approvedTask.OutputArtifact.Should().Contain("[APPROVED by cgo_approver_99]");

            // List missions
            var list = engine2.ListMissions(tenantId);
            list.Should().HaveCount(1);
            list[0].MissionId.Should().Be(missionId);

            // Telemetry
            var telemetry = engine2.GetTelemetry(tenantId, missionId);
            telemetry.TotalTasks.Should().Be(5);
            telemetry.CompletedTasks.Should().Be(5);
            telemetry.PendingApprovalCount.Should().Be(0);
        }
    }
}
