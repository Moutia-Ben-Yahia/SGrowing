using FluentAssertions;
using SGrowing.Api.Modules.AI.Missions;
using Xunit;

namespace SGrowing.Tests.Unit;

public class AiMissionPlannerTests
{
    [Fact]
    public void PlanMission_DecomposesGoalIntoStructuredDagWithHumanInTheLoopGate()
    {
        // Arrange
        var service = new AiMissionPlannerService();
        var request = new MissionObjectiveRequest(
            Title: "MENA Q4 E-Commerce Scale",
            StrategicGoal: "Accelerate high-margin direct-to-consumer sales across KSA and UAE",
            BudgetLimit: 12_500m,
            TargetGeo: "KSA",
            Constraints: ["Max daily ad spend SAR 1,500", "Bilingual Arabic/English creative assets"]
        );

        // Act
        var plan = service.PlanMission(request);

        // Assert
        plan.Should().NotBeNull();
        plan.MissionId.Should().NotBeEmpty();
        plan.Title.Should().Be("MENA Q4 E-Commerce Scale");
        plan.StrategicGoal.Should().Be("Accelerate high-margin direct-to-consumer sales across KSA and UAE");
        plan.Status.Should().Be(MissionStatus.InProgress);
        plan.EstimatedCost.Should().Be(12_500m);
        plan.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        plan.CompletedAtUtc.Should().BeNull();

        plan.TaskDag.Should().HaveCount(5);

        // Task 1: ResearchAgent
        var task1 = plan.TaskDag.Single(t => t.TaskId == "task-1");
        task1.Title.Should().Be("Market & Competitor Signal Analysis");
        task1.AgentRole.Should().Be("ResearchAgent");
        task1.DependsOnTaskIds.Should().BeEmpty();
        task1.Status.Should().Be(TaskExecutionStatus.Pending);
        task1.RequiresHumanApproval.Should().BeFalse();
        task1.OutputPayload.Should().BeNull();

        // Task 2: SeoAgent (depends on task-1)
        var task2 = plan.TaskDag.Single(t => t.TaskId == "task-2");
        task2.Title.Should().Be("SEO Keyword Strategy & Content Brief Generation");
        task2.AgentRole.Should().Be("SeoAgent");
        task2.DependsOnTaskIds.Should().ContainSingle().Which.Should().Be("task-1");
        task2.Status.Should().Be(TaskExecutionStatus.Pending);
        task2.RequiresHumanApproval.Should().BeFalse();

        // Task 3: ContentAgent (depends on task-2)
        var task3 = plan.TaskDag.Single(t => t.TaskId == "task-3");
        task3.Title.Should().Be("Multilingual Landing Page & Editorial Copywriting");
        task3.AgentRole.Should().Be("ContentAgent");
        task3.DependsOnTaskIds.Should().ContainSingle().Which.Should().Be("task-2");
        task3.Status.Should().Be(TaskExecutionStatus.Pending);
        task3.RequiresHumanApproval.Should().BeFalse();

        // Task 4: AdvertisingAgent (independent parallel task depending on task-1)
        var task4 = plan.TaskDag.Single(t => t.TaskId == "task-4");
        task4.Title.Should().Be("Omnichannel Spintax Ad Campaigns & Creative Synthesis");
        task4.AgentRole.Should().Be("AdvertisingAgent");
        task4.DependsOnTaskIds.Should().ContainSingle().Which.Should().Be("task-1");
        task4.Status.Should().Be(TaskExecutionStatus.Pending);
        task4.RequiresHumanApproval.Should().BeFalse();

        // Task 5: ApprovalGatekeeper (depends on task-3 and task-4, requires human sign-off)
        var task5 = plan.TaskDag.Single(t => t.TaskId == "task-5");
        task5.Title.Should().Be("Budget Allocation & Campaign Launch Sign-Off");
        task5.AgentRole.Should().Be("ApprovalGatekeeper");
        task5.DependsOnTaskIds.Should().BeEquivalentTo(new[] { "task-3", "task-4" });
        task5.Status.Should().Be(TaskExecutionStatus.Pending);
        task5.RequiresHumanApproval.Should().BeTrue();
    }

    [Fact]
    public void ExecuteNextStep_AdvancesIndependentTasks_AndHaltsAtApprovalGate()
    {
        // Arrange
        var service = new AiMissionPlannerService();
        var request = new MissionObjectiveRequest(
            Title: "Black Friday High Velocity Swarm",
            StrategicGoal: "Dominate search and paid channels for premium skincare",
            BudgetLimit: 8_000m,
            TargetGeo: "KSA"
        );
        var plan = service.PlanMission(request);

        // Act - execute autonomous step
        var updated = service.ExecuteNextStep(plan.MissionId);

        // Assert - DAG advances autonomous tasks and halts before gated spend allocation
        updated.Status.Should().Be(MissionStatus.AwaitingApproval);
        updated.CompletedAtUtc.Should().BeNull();

        // Task 1 completed
        var task1 = updated.TaskDag.Single(t => t.TaskId == "task-1");
        task1.Status.Should().Be(TaskExecutionStatus.Succeeded);
        task1.OutputPayload.Should().Contain("identified 4 competitor pricing anomalies in KSA");

        // Independent tasks 2 & 4 advanced and succeeded
        var task2 = updated.TaskDag.Single(t => t.TaskId == "task-2");
        task2.Status.Should().Be(TaskExecutionStatus.Succeeded);
        task2.OutputPayload.Should().Contain("SEO strategy complete");

        var task4 = updated.TaskDag.Single(t => t.TaskId == "task-4");
        task4.Status.Should().Be(TaskExecutionStatus.Succeeded);
        task4.OutputPayload.Should().Contain("Advertising complete");

        // Task 3 completed after task 2
        var task3 = updated.TaskDag.Single(t => t.TaskId == "task-3");
        task3.Status.Should().Be(TaskExecutionStatus.Succeeded);
        task3.OutputPayload.Should().Contain("Content complete");

        // Task 5 halted at human gatekeeper
        var task5 = updated.TaskDag.Single(t => t.TaskId == "task-5");
        task5.Status.Should().Be(TaskExecutionStatus.WaitingApproval);
        task5.RequiresHumanApproval.Should().BeTrue();
        task5.OutputPayload.Should().BeNull();
    }

    [Fact]
    public void ExecuteNextStep_WithExplicitApproval_CompletesEntireMission()
    {
        // Arrange
        var service = new AiMissionPlannerService();
        var request = new MissionObjectiveRequest(
            Title: "Direct Inbound Lead Gen",
            StrategicGoal: "Acquire enterprise B2B retail partnerships",
            BudgetLimit: 15_000m,
            TargetGeo: "KSA, UAE"
        );
        var plan = service.PlanMission(request);

        // First step advances all autonomous steps and halts at ApprovalGatekeeper
        var awaitingPlan = service.ExecuteNextStep(plan.MissionId);
        awaitingPlan.Status.Should().Be(MissionStatus.AwaitingApproval);

        // Act - Explicitly approve task-5
        var completedPlan = service.ExecuteNextStep(plan.MissionId, "task-5");

        // Assert - entire mission and all DAG nodes complete
        completedPlan.Status.Should().Be(MissionStatus.Completed);
        completedPlan.CompletedAtUtc.Should().NotBeNull();
        completedPlan.CompletedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        completedPlan.TaskDag.Should().OnlyContain(t => t.Status == TaskExecutionStatus.Succeeded);

        var task5 = completedPlan.TaskDag.Single(t => t.TaskId == "task-5");
        task5.Status.Should().Be(TaskExecutionStatus.Succeeded);
        task5.OutputPayload.Should().Contain("Human Gatekeeper signed off");
    }

    [Fact]
    public void ExecuteNextStep_WithoutApproval_WhenAwaitingApproval_RemainsHalted()
    {
        // Arrange
        var service = new AiMissionPlannerService();
        var request = new MissionObjectiveRequest(
            Title: "Brand Refresh",
            StrategicGoal: "Launch repositioning campaign",
            BudgetLimit: 5_000m,
            TargetGeo: "Global"
        );
        var plan = service.PlanMission(request);

        // Advance to gate
        service.ExecuteNextStep(plan.MissionId);

        // Act - repeat step without approval
        var result = service.ExecuteNextStep(plan.MissionId);

        // Assert - still awaiting approval
        result.Status.Should().Be(MissionStatus.AwaitingApproval);
        var task5 = result.TaskDag.Single(t => t.TaskId == "task-5");
        task5.Status.Should().Be(TaskExecutionStatus.WaitingApproval);
    }

    [Fact]
    public void ExecuteNextStep_ThrowsKeyNotFoundException_WhenMissionDoesNotExist()
    {
        // Arrange
        var service = new AiMissionPlannerService();
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = () => service.ExecuteNextStep(nonExistentId);

        // Assert
        act.Should().Throw<KeyNotFoundException>()
            .WithMessage($"*{nonExistentId}*");
    }

    [Fact]
    public void GetMission_And_ListMissions_ManageLifecycleCorrectly()
    {
        // Arrange
        var service = new AiMissionPlannerService();
        var req1 = new MissionObjectiveRequest("Mission Alpha", "Goal 1", 1000m, "KSA");
        var req2 = new MissionObjectiveRequest("Mission Beta", "Goal 2", 2000m, "UAE");

        var plan1 = service.PlanMission(req1);
        var plan2 = service.PlanMission(req2);

        // Act & Assert GetMission
        service.GetMission(plan1.MissionId).Should().NotBeNull();
        service.GetMission(plan1.MissionId)!.Title.Should().Be("Mission Alpha");
        service.GetMission(Guid.NewGuid()).Should().BeNull();

        // Act & Assert ListMissions
        var list = service.ListMissions();
        list.Should().HaveCount(2);
        list.Select(m => m.MissionId).Should().Contain(new[] { plan1.MissionId, plan2.MissionId });
    }
}
