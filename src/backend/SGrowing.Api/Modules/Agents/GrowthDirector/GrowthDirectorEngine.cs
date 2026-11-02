using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Agents.GrowthDirector;

public interface IGrowthDirectorEngine
{
    GrowthMissionPlan DecomposeObjective(GrowthMissionObjective objective, FinancialGuardrailPolicy? policy = null);
    ExecuteMissionResult ApproveAndExecuteMission(string tenantId, string missionId, List<string> approvedTaskIds);
    GrowthMissionPlan? GetMission(string tenantId, string missionId);
}

public class GrowthDirectorEngine : IGrowthDirectorEngine
{
    private readonly ConcurrentDictionary<string, GrowthMissionPlan> _store = new(StringComparer.OrdinalIgnoreCase);

    private static string StoreKey(string tenantId, string missionId) => $"{tenantId}:{missionId}";

    public GrowthMissionPlan DecomposeObjective(GrowthMissionObjective objective, FinancialGuardrailPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(objective);
        policy ??= new FinancialGuardrailPolicy();

        var missionId = $"msn_{Guid.NewGuid():N}";
        var budget = Math.Max(0m, objective.MaxBudgetUsd);

        // Sub-task budget allocation:
        // 1. SEO Specialist: 15%
        // 2. Content Copywriter: 20%
        // 3. Ad Optimizer: 50%
        // 4. Automation Architect: 15% (remainder ensures 100% budget match)
        var seoBudget = Math.Round(budget * 0.15m, 2);
        var contentBudget = Math.Round(budget * 0.20m, 2);
        var adBudget = Math.Round(budget * 0.50m, 2);
        var automationBudget = Math.Max(0m, budget - (seoBudget + contentBudget + adBudget));

        var subTasks = new List<DecomposedSubTask>
        {
            CreateSubTask(
                missionId,
                GrowthAgentTypes.SeoSpecialist,
                "Keyword gap & competitor search intent audit",
                $"Audit search intent, high-converting keyword gaps, and competitor organic footprints for '{objective.Title}' targeting {objective.TargetMetric}.",
                seoBudget,
                policy),

            CreateSubTask(
                missionId,
                GrowthAgentTypes.ContentCopywriter,
                "Landing page & lead magnet draft",
                $"Draft high-conversion landing page copy, value-proposition hooks, and gated lead magnet assets for '{objective.Title}'.",
                contentBudget,
                policy),

            CreateSubTask(
                missionId,
                GrowthAgentTypes.AdOptimizer,
                "Meta & Google CAPI campaign budget allocation",
                $"Calibrate ad spend, conversion bidding rules, and server-side Conversions API (CAPI) events to achieve {objective.TargetValue} {objective.TargetMetric}.",
                adBudget,
                policy),

            CreateSubTask(
                missionId,
                GrowthAgentTypes.AutomationArchitect,
                "Nurture workflow DAG configuration",
                $"Configure event-driven multi-stage email and SMS nurture workflow DAG with automated lead scoring.",
                automationBudget,
                policy)
        };

        var totalEstimatedCost = subTasks.Sum(t => t.AllocatedBudget);
        var exceedsAutoApproval = totalEstimatedCost > policy.AutoApproveSpendLimitUsd;
        var requiresAnyApproval = exceedsAutoApproval || subTasks.Any(t => t.RequiresHumanApproval);
        var status = requiresAnyApproval ? GrowthMissionStatus.PendingApproval : GrowthMissionStatus.AutoApproved;

        var plan = new GrowthMissionPlan(
            MissionId: missionId,
            Objective: objective,
            SubTasks: subTasks,
            TotalEstimatedCostUsd: totalEstimatedCost,
            ExceedsAutoApprovalThreshold: exceedsAutoApproval,
            Status: status,
            CreatedAtUtc: DateTime.UtcNow
        );

        _store[StoreKey(objective.TenantId, missionId)] = plan;
        return plan;
    }

    private static DecomposedSubTask CreateSubTask(
        string missionId,
        string agentType,
        string title,
        string description,
        decimal budget,
        FinancialGuardrailPolicy policy)
    {
        var requiresApproval = budget > policy.AutoApproveSpendLimitUsd;
        var status = requiresApproval ? GrowthTaskStatus.PendingApproval : GrowthTaskStatus.AutoApproved;
        var taskId = $"task-{missionId}-{agentType.ToLowerInvariant()}";

        return new DecomposedSubTask(
            TaskId: taskId,
            AgentType: agentType,
            Title: title,
            Description: description,
            AllocatedBudget: budget,
            RequiresHumanApproval: requiresApproval,
            Status: status
        );
    }

    public ExecuteMissionResult ApproveAndExecuteMission(string tenantId, string missionId, List<string> approvedTaskIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(missionId);
        approvedTaskIds ??= new List<string>();

        var key = StoreKey(tenantId, missionId);
        if (!_store.TryGetValue(key, out var existingPlan))
        {
            return new ExecuteMissionResult(
                Success: false,
                MissionId: missionId,
                TasksExecuted: 0,
                TasksPendingApproval: 0,
                TotalApprovedSpend: 0m,
                StatusMessage: $"Growth mission '{missionId}' not found for tenant '{tenantId}'.",
                ExecutedAtUtc: DateTime.UtcNow
            );
        }

        var approvedSet = new HashSet<string>(approvedTaskIds, StringComparer.OrdinalIgnoreCase);
        var updatedSubTasks = new List<DecomposedSubTask>();
        int executedCount = 0;
        int pendingCount = 0;
        decimal totalApprovedSpend = 0m;

        foreach (var task in existingPlan.SubTasks)
        {
            if (approvedSet.Contains(task.TaskId))
            {
                // Execute only approved tasks
                updatedSubTasks.Add(task with { Status = GrowthTaskStatus.Executed });
                executedCount++;
                totalApprovedSpend += task.AllocatedBudget;
            }
            else
            {
                // Unapproved tasks remain in their holding / pending status
                updatedSubTasks.Add(task);
                if (task.Status != GrowthTaskStatus.Executed)
                {
                    pendingCount++;
                }
            }
        }

        string overallStatus;
        if (pendingCount == 0 && executedCount > 0)
        {
            overallStatus = GrowthMissionStatus.Executed;
        }
        else if (executedCount > 0)
        {
            overallStatus = GrowthMissionStatus.PartiallyExecuted;
        }
        else
        {
            overallStatus = existingPlan.Status;
        }

        var updatedPlan = existingPlan with
        {
            SubTasks = updatedSubTasks,
            Status = overallStatus
        };

        _store[key] = updatedPlan;

        return new ExecuteMissionResult(
            Success: true,
            MissionId: missionId,
            TasksExecuted: executedCount,
            TasksPendingApproval: pendingCount,
            TotalApprovedSpend: totalApprovedSpend,
            StatusMessage: $"Executed {executedCount} approved tasks with spend ${totalApprovedSpend:F2}. {pendingCount} tasks pending approval in holding state.",
            ExecutedAtUtc: DateTime.UtcNow
        );
    }

    public GrowthMissionPlan? GetMission(string tenantId, string missionId)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(missionId))
        {
            return null;
        }

        return _store.TryGetValue(StoreKey(tenantId, missionId), out var plan) ? plan : null;
    }
}
