using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.AI.Governance;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Unit;

public class AutonomousSwarmPersistenceTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task LaunchSwarmMissionAsync_PersistsMissionAndDecomposedSteps()
    {
        using var db = CreateDbContext();
        var gatekeeper = new ApprovalGatekeeperService(db);
        var swarmService = new AutonomousSwarmPersistenceService(db, gatekeeper);
        var tenantId = Guid.NewGuid();

        var mission = await swarmService.LaunchSwarmMissionAsync(new SwarmGoalRequest(
            TenantId: tenantId,
            GoalTitle: "Scale Q4 Conversions",
            HighLevelObjective: "Expand audience reach and scale high-ROAS ad campaigns",
            TargetPersona: "GrowthDirector",
            BudgetUsd: 200m
        ));

        mission.Should().NotBeNull();
        mission.Status.Should().Be(AgentMissionStatus.Pending);
        mission.Steps.Should().HaveCount(3);

        var persisted = await swarmService.GetMissionDetailsAsync(mission.Id);
        persisted.Should().NotBeNull();
        persisted!.Steps.Should().HaveCount(3);
        persisted.Steps[0].ToolName.Should().Be("crm_segment_audience");
        persisted.Steps[1].ToolName.Should().Be("AdSpendAdjustment");
    }

    [Fact]
    public async Task ExecuteNextStepAsync_LowRiskStep_ExecutesAndAuditsLog()
    {
        using var db = CreateDbContext();
        var gatekeeper = new ApprovalGatekeeperService(db);
        var swarmService = new AutonomousSwarmPersistenceService(db, gatekeeper);
        var tenantId = Guid.NewGuid();

        var mission = await swarmService.LaunchSwarmMissionAsync(new SwarmGoalRequest(
            TenantId: tenantId,
            GoalTitle: "Audience Discovery",
            HighLevelObjective: "Segment buyers",
            TargetPersona: "GrowthDirector",
            BudgetUsd: 50m
        ));

        var step1Result = await swarmService.ExecuteNextStepAsync(mission.Id);

        step1Result.Status.Should().Be(AgentMissionStepStatus.Completed);
        step1Result.RequiresHumanApproval.Should().BeFalse();

        var toolLog = await db.AiToolExecutionLogs.FirstOrDefaultAsync(l => l.TenantId == tenantId);
        toolLog.Should().NotBeNull();
        toolLog!.ToolName.Should().Be("crm_segment_audience");
    }

    [Fact]
    public async Task ExecuteNextStepAsync_HighRiskStep_PausesForHitlCryptographicToken()
    {
        using var db = CreateDbContext();
        var gatekeeper = new ApprovalGatekeeperService(db);
        var swarmService = new AutonomousSwarmPersistenceService(db, gatekeeper);
        var tenantId = Guid.NewGuid();

        var mission = await swarmService.LaunchSwarmMissionAsync(new SwarmGoalRequest(
            TenantId: tenantId,
            GoalTitle: "Scale Meta Ads",
            HighLevelObjective: "Scale budget to $1200",
            TargetPersona: "AdOptimizer",
            BudgetUsd: 1500m
        ));

        // Step 1: Low risk -> completes
        await swarmService.ExecuteNextStepAsync(mission.Id);

        // Step 2: AdSpendAdjustment > $500 -> High risk -> pauses
        var step2Result = await swarmService.ExecuteNextStepAsync(mission.Id);

        step2Result.Status.Should().Be(AgentMissionStepStatus.AwaitingApproval);
        step2Result.RequiresHumanApproval.Should().BeTrue();
        step2Result.ApprovalToken.Should().NotBeNullOrWhiteSpace();

        var updatedMission = await swarmService.GetMissionDetailsAsync(mission.Id);
        updatedMission!.Status.Should().Be(AgentMissionStatus.PausedForApproval);

        // Verify pending approval request exists in database
        var pendingApprovals = await gatekeeper.GetPendingApprovalsAsync(tenantId);
        pendingApprovals.Should().ContainSingle(p => p.OperationType == "AdSpendAdjustment");
    }

    [Fact]
    public async Task ApprovePausedStepWithTokenAsync_WithValidToken_ResumesMissionToCompletion()
    {
        using var db = CreateDbContext();
        var gatekeeper = new ApprovalGatekeeperService(db);
        var swarmService = new AutonomousSwarmPersistenceService(db, gatekeeper);
        var tenantId = Guid.NewGuid();

        var mission = await swarmService.LaunchSwarmMissionAsync(new SwarmGoalRequest(
            TenantId: tenantId,
            GoalTitle: "Full Cycle Campaign",
            HighLevelObjective: "Run complete growth swarm",
            TargetPersona: "GrowthDirector",
            BudgetUsd: 2000m
        ));

        // Step 1
        await swarmService.ExecuteNextStepAsync(mission.Id);
        // Step 2 (Pauses)
        var step2Result = await swarmService.ExecuteNextStepAsync(mission.Id);

        var pendingApprovals = await gatekeeper.GetPendingApprovalsAsync(tenantId);
        var approvalReq = pendingApprovals.First();

        // 1. Try fake token -> fails
        var fakeApproved = await swarmService.ApprovePausedStepWithTokenAsync(
            mission.Id,
            step2Result.StepId,
            approvalReq.Id,
            "fake_token_1234567890abcdef1234567890abcdef",
            "director"
        );
        fakeApproved.Should().BeFalse();

        // 2. Supply genuine cryptographic token
        var validApproved = await swarmService.ApprovePausedStepWithTokenAsync(
            mission.Id,
            step2Result.StepId,
            approvalReq.Id,
            step2Result.ApprovalToken!,
            "director_officer"
        );
        validApproved.Should().BeTrue();

        // 3. Continue execution through Step 2 and Step 3
        var step2Resume = await swarmService.ExecuteNextStepAsync(mission.Id);
        step2Resume.Status.Should().Be(AgentMissionStepStatus.Completed);

        var step3Result = await swarmService.ExecuteNextStepAsync(mission.Id);
        step3Result.Status.Should().Be(AgentMissionStepStatus.Completed);

        // Mission complete!
        var finalMission = await swarmService.GetMissionDetailsAsync(mission.Id);
        finalMission!.Status.Should().Be(AgentMissionStatus.Completed);
        finalMission.CompletedAtUtc.Should().NotBeNull();
    }
}
