using FluentAssertions;
using SGrowing.Api.Modules.Agents.GrowthDirector;
using Xunit;

namespace SGrowing.Tests.Agents;

public class GrowthDirectorTests
{
    private readonly GrowthDirectorEngine _engine;
    private const string TestTenantId = "tenant-growth-test";

    public GrowthDirectorTests()
    {
        _engine = new GrowthDirectorEngine();
    }

    [Fact]
    public void DecomposeObjective_WithHighBudget_FlagsHumanApprovalRequirement()
    {
        // Arrange
        var objective = new GrowthMissionObjective(
            ObjectiveId: "obj-high-spend",
            TenantId: TestTenantId,
            Title: "Scale Q4 Paid Acquisition",
            TargetMetric: "MQLs",
            TargetValue: 1000m,
            MaxBudgetUsd: 1500m, // High budget exceeds default auto-approve limit of $250
            DeadlineUtc: DateTime.UtcNow.AddDays(30)
        );
        var policy = new FinancialGuardrailPolicy(AutoApproveSpendLimitUsd: 250m);

        // Act
        var plan = _engine.DecomposeObjective(objective, policy);

        // Assert
        plan.Should().NotBeNull();
        plan.ExceedsAutoApprovalThreshold.Should().BeTrue();
        plan.Status.Should().Be("PendingApproval");
        plan.TotalEstimatedCostUsd.Should().Be(1500m);

        // Ad Optimizer gets 50% = $750 > $250 threshold, so human approval is flagged
        var adTask = plan.SubTasks.First(t => t.AgentType == "Ad_Optimizer");
        adTask.AllocatedBudget.Should().Be(750m);
        adTask.RequiresHumanApproval.Should().BeTrue();
        adTask.Status.Should().Be("PendingApproval");

        // Subtasks requiring approval are present
        plan.SubTasks.Should().Contain(t => t.RequiresHumanApproval);
    }

    [Fact]
    public void DecomposeObjective_WithLowBudget_AutoApprovesEligibleSubTasks()
    {
        // Arrange
        var objective = new GrowthMissionObjective(
            ObjectiveId: "obj-micro-spend",
            TenantId: TestTenantId,
            Title: "Bootstrap Newsletter Subscribers",
            TargetMetric: "Subscribers",
            TargetValue: 200m,
            MaxBudgetUsd: 200m, // All sub-tasks are <= 100m, well below $250
            DeadlineUtc: DateTime.UtcNow.AddDays(14)
        );
        var policy = new FinancialGuardrailPolicy(AutoApproveSpendLimitUsd: 250m);

        // Act
        var plan = _engine.DecomposeObjective(objective, policy);

        // Assert
        plan.Should().NotBeNull();
        plan.ExceedsAutoApprovalThreshold.Should().BeFalse();
        plan.Status.Should().Be("AutoApproved");
        plan.TotalEstimatedCostUsd.Should().Be(200m);

        plan.SubTasks.Should().AllSatisfy(task =>
        {
            task.RequiresHumanApproval.Should().BeFalse();
            task.Status.Should().Be("AutoApproved");
            task.AllocatedBudget.Should().BeLessThanOrEqualTo(250m);
        });
    }

    [Fact]
    public void DecomposeObjective_ProducesCoordinatedCrossDomainSubTasks()
    {
        // Arrange
        var objective = new GrowthMissionObjective(
            ObjectiveId: "obj-cross-domain",
            TenantId: TestTenantId,
            Title: "Enterprise Lead Generation Surge",
            TargetMetric: "SQLs",
            TargetValue: 500m,
            MaxBudgetUsd: 1000m,
            DeadlineUtc: DateTime.UtcNow.AddDays(45)
        );
        var policy = new FinancialGuardrailPolicy();

        // Act
        var plan = _engine.DecomposeObjective(objective, policy);

        // Assert
        plan.Should().NotBeNull();
        plan.SubTasks.Should().HaveCount(4);

        var agentTypes = plan.SubTasks.Select(s => s.AgentType).ToList();
        agentTypes.Should().Contain(new[]
        {
            "SEO_Specialist",
            "Content_Copywriter",
            "Ad_Optimizer",
            "Automation_Architect"
        });

        plan.SubTasks.Should().Contain(s => s.AgentType == "SEO_Specialist" && s.Title == "Keyword gap & competitor search intent audit");
        plan.SubTasks.Should().Contain(s => s.AgentType == "Content_Copywriter" && s.Title == "Landing page & lead magnet draft");
        plan.SubTasks.Should().Contain(s => s.AgentType == "Ad_Optimizer" && s.Title == "Meta & Google CAPI campaign budget allocation");
        plan.SubTasks.Should().Contain(s => s.AgentType == "Automation_Architect" && s.Title == "Nurture workflow DAG configuration");

        // Sum of all subtask budgets must equal total plan cost
        plan.TotalEstimatedCostUsd.Should().Be(plan.SubTasks.Sum(s => s.AllocatedBudget));
        plan.TotalEstimatedCostUsd.Should().Be(1000m);
    }

    [Fact]
    public void ApproveAndExecuteMission_ExecutesOnlyApprovedTasks()
    {
        // Arrange
        var tenantId = "tenant-exec-99";
        var objective = new GrowthMissionObjective(
            ObjectiveId: "obj-exec-test",
            TenantId: tenantId,
            Title: "Product Hunt Launch Blast",
            TargetMetric: "UpvotesAndSignups",
            TargetValue: 1200m,
            MaxBudgetUsd: 1000m,
            DeadlineUtc: DateTime.UtcNow.AddDays(7)
        );
        var plan = _engine.DecomposeObjective(objective, new FinancialGuardrailPolicy());

        // Approve only the first two tasks
        var task1 = plan.SubTasks[0];
        var task2 = plan.SubTasks[1];
        var approvedTaskIds = new List<string> { task1.TaskId, task2.TaskId };

        // Act
        var result = _engine.ApproveAndExecuteMission(tenantId, plan.MissionId, approvedTaskIds);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.MissionId.Should().Be(plan.MissionId);
        result.TasksExecuted.Should().Be(2);
        result.TasksPendingApproval.Should().Be(2);
        result.TotalApprovedSpend.Should().Be(task1.AllocatedBudget + task2.AllocatedBudget);

        // Verify stored state
        var updatedPlan = _engine.GetMission(tenantId, plan.MissionId);
        updatedPlan.Should().NotBeNull();
        updatedPlan!.SubTasks.First(s => s.TaskId == task1.TaskId).Status.Should().Be("Executed");
        updatedPlan.SubTasks.First(s => s.TaskId == task2.TaskId).Status.Should().Be("Executed");

        // The remaining tasks must NOT be executed
        updatedPlan.SubTasks.Where(s => !approvedTaskIds.Contains(s.TaskId))
            .Should().AllSatisfy(s => s.Status.Should().NotBe("Executed"));
    }

    [Fact]
    public void GetMission_ReturnsStoredPlan()
    {
        // Arrange
        var tenantId = "tenant-stored-plan";
        var objective = new GrowthMissionObjective(
            ObjectiveId: "obj-store-test",
            TenantId: tenantId,
            Title: "B2B SaaS Retargeting",
            TargetMetric: "DemoRequests",
            TargetValue: 50m,
            MaxBudgetUsd: 500m,
            DeadlineUtc: DateTime.UtcNow.AddDays(20)
        );
        var plan = _engine.DecomposeObjective(objective);

        // Act
        var retrieved = _engine.GetMission(tenantId, plan.MissionId);
        var nonExistent = _engine.GetMission(tenantId, "non-existent-mission-id");
        var wrongTenant = _engine.GetMission("wrong-tenant", plan.MissionId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.MissionId.Should().Be(plan.MissionId);
        retrieved.Objective.Title.Should().Be("B2B SaaS Retargeting");
        retrieved.SubTasks.Should().HaveCount(4);

        nonExistent.Should().BeNull();
        wrongTenant.Should().BeNull();
    }
}
