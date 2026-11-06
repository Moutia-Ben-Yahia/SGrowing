using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.AI.Missions;

/// <summary>
/// Service contract for autonomous multi-agent mission planning and task DAG execution.
/// </summary>
public interface IAiMissionPlannerService
{
    /// <summary>
    /// Decomposes a high-level marketing or growth objective into a structured task DAG.
    /// </summary>
    AgentMissionPlan PlanMission(MissionObjectiveRequest request);

    /// <summary>
    /// Advances autonomous tasks whose dependencies have succeeded, halting at human approval gates.
    /// When an approved task ID is provided, unlocks and executes that gate.
    /// </summary>
    AgentMissionPlan ExecuteNextStep(Guid missionId, string? approvedTaskId = null);

    /// <summary>
    /// Retrieves an existing mission by its unique identifier.
    /// </summary>
    AgentMissionPlan? GetMission(Guid missionId);

    /// <summary>
    /// Lists all current agent mission plans.
    /// </summary>
    List<AgentMissionPlan> ListMissions();
}

/// <summary>
/// In-memory implementation of the autonomous multi-agent mission planner and task DAG execution engine.
/// Coordinates agent roles (MarketingDirector, ResearchAgent, SeoAgent, ContentAgent, AdvertisingAgent, ApprovalGatekeeper)
/// with safe human-supervised autonomy.
/// </summary>
public class AiMissionPlannerService : IAiMissionPlannerService
{
    private readonly ConcurrentDictionary<Guid, AgentMissionPlan> _missions = new();
    private readonly ConcurrentDictionary<Guid, string> _targetGeos = new();

    /// <inheritdoc />
    public AgentMissionPlan PlanMission(MissionObjectiveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var missionId = Guid.NewGuid();
        var estimatedCost = request.BudgetLimit > 0 ? request.BudgetLimit : 250.00m;

        // Autonomous goal decomposition into execution DAG
        var taskDag = new List<MissionTaskNode>
        {
            new(
                TaskId: "task-1",
                Title: "Market & Competitor Signal Analysis",
                AgentRole: "ResearchAgent",
                DependsOnTaskIds: [],
                Status: TaskExecutionStatus.Pending,
                RequiresHumanApproval: false
            ),
            new(
                TaskId: "task-2",
                Title: "SEO Keyword Strategy & Content Brief Generation",
                AgentRole: "SeoAgent",
                DependsOnTaskIds: ["task-1"],
                Status: TaskExecutionStatus.Pending,
                RequiresHumanApproval: false
            ),
            new(
                TaskId: "task-3",
                Title: "Multilingual Landing Page & Editorial Copywriting",
                AgentRole: "ContentAgent",
                DependsOnTaskIds: ["task-2"],
                Status: TaskExecutionStatus.Pending,
                RequiresHumanApproval: false
            ),
            new(
                TaskId: "task-4",
                Title: "Omnichannel Spintax Ad Campaigns & Creative Synthesis",
                AgentRole: "AdvertisingAgent",
                DependsOnTaskIds: ["task-1"],
                Status: TaskExecutionStatus.Pending,
                RequiresHumanApproval: false
            ),
            new(
                TaskId: "task-5",
                Title: "Budget Allocation & Campaign Launch Sign-Off",
                AgentRole: "ApprovalGatekeeper",
                DependsOnTaskIds: ["task-3", "task-4"],
                Status: TaskExecutionStatus.Pending,
                RequiresHumanApproval: true
            )
        };

        var plan = new AgentMissionPlan(
            MissionId: missionId,
            Title: request.Title,
            StrategicGoal: request.StrategicGoal,
            Status: MissionStatus.InProgress,
            TaskDag: taskDag,
            EstimatedCost: estimatedCost,
            CreatedAtUtc: DateTime.UtcNow,
            CompletedAtUtc: null
        );

        _missions[missionId] = plan;
        _targetGeos[missionId] = string.IsNullOrWhiteSpace(request.TargetGeo) ? "Global" : request.TargetGeo;

        return plan;
    }

    /// <inheritdoc />
    public AgentMissionPlan ExecuteNextStep(Guid missionId, string? approvedTaskId = null)
    {
        if (!_missions.TryGetValue(missionId, out var plan))
        {
            throw new KeyNotFoundException($"Mission with ID '{missionId}' was not found.");
        }

        if (plan.Status == MissionStatus.Completed)
        {
            return plan;
        }

        var targetGeo = _targetGeos.TryGetValue(missionId, out var geo) ? geo : "Global";
        var taskList = new List<MissionTaskNode>(plan.TaskDag);

        bool advancedAny;
        bool haltedForApproval = false;

        do
        {
            advancedAny = false;

            var completedTaskIds = taskList
                .Where(t => t.Status == TaskExecutionStatus.Succeeded)
                .Select(t => t.TaskId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var runnableCandidates = taskList
                .Where(t => (t.Status == TaskExecutionStatus.Pending || 
                             (t.Status == TaskExecutionStatus.WaitingApproval && 
                              !string.IsNullOrWhiteSpace(approvedTaskId) && 
                              string.Equals(t.TaskId, approvedTaskId, StringComparison.OrdinalIgnoreCase)))
                         && t.DependsOnTaskIds.All(depId => completedTaskIds.Contains(depId)))
                .ToList();

            if (runnableCandidates.Count == 0)
            {
                break;
            }

            foreach (var task in runnableCandidates)
            {
                var isExplicitlyApproved = !string.IsNullOrWhiteSpace(approvedTaskId) &&
                                           string.Equals(task.TaskId, approvedTaskId, StringComparison.OrdinalIgnoreCase);

                if (task.RequiresHumanApproval && !isExplicitlyApproved)
                {
                    var waitingNode = task with { Status = TaskExecutionStatus.WaitingApproval };
                    ReplaceTaskNode(taskList, waitingNode);
                    haltedForApproval = true;
                }
                else
                {
                    var succeededNode = task with
                    {
                        Status = TaskExecutionStatus.Succeeded,
                        OutputPayload = GenerateMockOutputPayload(task.TaskId, plan.StrategicGoal, targetGeo, plan.EstimatedCost)
                    };
                    ReplaceTaskNode(taskList, succeededNode);
                    advancedAny = true;
                }
            }

            if (haltedForApproval)
            {
                break;
            }

        } while (advancedAny);

        var allSucceeded = taskList.All(t => t.Status == TaskExecutionStatus.Succeeded);
        var anyWaitingApproval = taskList.Any(t => t.Status == TaskExecutionStatus.WaitingApproval);
        var anyFailed = taskList.Any(t => t.Status == TaskExecutionStatus.Failed);

        MissionStatus newStatus = allSucceeded
            ? MissionStatus.Completed
            : anyWaitingApproval
                ? MissionStatus.AwaitingApproval
                : anyFailed
                    ? MissionStatus.Failed
                    : MissionStatus.InProgress;

        DateTime? completedAtUtc = allSucceeded
            ? (plan.CompletedAtUtc ?? DateTime.UtcNow)
            : null;

        var updatedPlan = plan with
        {
            Status = newStatus,
            TaskDag = taskList,
            CompletedAtUtc = completedAtUtc
        };

        _missions[missionId] = updatedPlan;
        return updatedPlan;
    }

    /// <inheritdoc />
    public AgentMissionPlan? GetMission(Guid missionId)
    {
        _missions.TryGetValue(missionId, out var plan);
        return plan;
    }

    /// <inheritdoc />
    public List<AgentMissionPlan> ListMissions()
    {
        return _missions.Values
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToList();
    }

    private static void ReplaceTaskNode(List<MissionTaskNode> tasks, MissionTaskNode replacement)
    {
        var index = tasks.FindIndex(t => string.Equals(t.TaskId, replacement.TaskId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            tasks[index] = replacement;
        }
    }

    private static string GenerateMockOutputPayload(string taskId, string strategicGoal, string targetGeo, decimal estimatedCost)
    {
        return taskId switch
        {
            "task-1" => $"Research complete: identified 4 competitor pricing anomalies in {targetGeo} and mapped high-intent acquisition signals for '{strategicGoal}'.",
            "task-2" => $"SEO strategy complete: generated 18 high-intent keyword clusters and programmatic content briefs for '{strategicGoal}'.",
            "task-3" => $"Content complete: drafted multilingual landing page copy, value propositions, and localized conversion CTAs for {targetGeo}.",
            "task-4" => $"Advertising complete: synthesized omnichannel Spintax ad permutations and creative assets across Meta, Google, and TikTok.",
            "task-5" => $"Approval complete: Human Gatekeeper signed off. Budget of ${estimatedCost:N2} allocated and campaign launched in {targetGeo}.",
            _ => $"Execution completed successfully for task '{taskId}'."
        };
    }
}
