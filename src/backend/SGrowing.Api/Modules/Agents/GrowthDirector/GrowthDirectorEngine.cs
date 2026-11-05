using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI.Firewall;
using SGrowing.Core.Entities;

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
    private readonly ICascadingModelRouterService? _router;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly SGrowingDbContext? _dbContext;
    private readonly ILogger<GrowthDirectorEngine>? _logger;
    private readonly object _dbLock = new();

    private static string StoreKey(string tenantId, string missionId) => $"{tenantId}:{missionId}";

    public GrowthDirectorEngine(
        ICascadingModelRouterService? router = null,
        IServiceScopeFactory? scopeFactory = null,
        ILogger<GrowthDirectorEngine>? logger = null)
        : this(router, scopeFactory, null, logger)
    {
    }

    public GrowthDirectorEngine(
        SGrowingDbContext dbContext,
        ICascadingModelRouterService? router = null,
        ILogger<GrowthDirectorEngine>? logger = null)
        : this(router, null, dbContext, logger)
    {
    }

    public GrowthDirectorEngine(
        ICascadingModelRouterService? router,
        IServiceScopeFactory? scopeFactory,
        SGrowingDbContext? dbContext,
        ILogger<GrowthDirectorEngine>? logger)
    {
        _router = router;
        _scopeFactory = scopeFactory;
        _dbContext = dbContext;
        _logger = logger;
    }

    public GrowthDirectorEngine()
        : this(null, null, null, null)
    {
    }

    public GrowthMissionPlan DecomposeObjective(GrowthMissionObjective objective, FinancialGuardrailPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(objective);
        policy ??= new FinancialGuardrailPolicy();

        var missionGuid = Guid.NewGuid();
        var missionId = $"msn_{missionGuid:N}";
        var budget = Math.Max(0m, objective.MaxBudgetUsd);

        List<DecomposedSubTask>? subTasks = null;

        if (_router != null)
        {
            try
            {
                subTasks = DecomposeViaLlm(objective, missionId, budget, policy);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "LLM decomposition failed; falling back to heuristic subtasks.");
            }
        }

        subTasks ??= FallbackHeuristicSubTasks(missionId, objective, budget, policy);

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

        PersistMissionPlanToDb(plan, missionGuid);

        return plan;
    }

    private List<DecomposedSubTask>? DecomposeViaLlm(
        GrowthMissionObjective objective,
        string missionId,
        decimal totalBudget,
        FinancialGuardrailPolicy policy)
    {
        if (_router == null) return null;

        var systemPrompt = @"You are the Supervised Autonomous AI Growth Director for SGrowing.
Decompose growth objectives into coordinated specialist agent sub-tasks with allocated budgets.
Specialist agents:
- SEO_Specialist: search intent, keyword gaps, organic footprints.
- Content_Copywriter: high-conversion landing page copy, hooks, lead magnet assets.
- Ad_Optimizer: ad spend, conversion bidding rules, CAPI events.
- Automation_Architect: event-driven email & SMS nurture workflow DAG with lead scoring.

Respond strictly with a JSON array of objects:
[
  {
    ""agentType"": ""SEO_Specialist"",
    ""title"": ""Keyword gap & competitor search intent audit"",
    ""description"": ""..."",
    ""allocatedBudgetRatio"": 0.15
  },
  {
    ""agentType"": ""Content_Copywriter"",
    ""title"": ""Landing page & lead magnet draft"",
    ""description"": ""..."",
    ""allocatedBudgetRatio"": 0.20
  },
  {
    ""agentType"": ""Ad_Optimizer"",
    ""title"": ""Meta & Google CAPI campaign budget allocation"",
    ""description"": ""..."",
    ""allocatedBudgetRatio"": 0.50
  },
  {
    ""agentType"": ""Automation_Architect"",
    ""title"": ""Nurture workflow DAG configuration"",
    ""description"": ""..."",
    ""allocatedBudgetRatio"": 0.15
  }
]";

        var prompt = $@"Objective Title: {objective.Title}
Target Metric: {objective.TargetMetric}
Target Value: {objective.TargetValue}
Total Budget: ${totalBudget:F2}
Tenant ID: {objective.TenantId}
Deadline: {objective.DeadlineUtc:yyyy-MM-dd}

Decompose this growth objective into coordinated specialist agent sub-tasks.";

        var req = new CascadingRouteRequest(Prompt: prompt, SystemPrompt: systemPrompt);
        var routeResult = _router.ExecuteAsync(req).GetAwaiter().GetResult();

        if (!routeResult.Success || string.IsNullOrWhiteSpace(routeResult.Content))
        {
            return null;
        }

        return ParseLlmSubTasks(routeResult.Content, missionId, totalBudget, policy);
    }

    private static List<DecomposedSubTask>? ParseLlmSubTasks(
        string rawContent,
        string missionId,
        decimal totalBudget,
        FinancialGuardrailPolicy policy)
    {
        var cleaned = rawContent.Trim();
        if (cleaned.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[7..];
        }
        else if (cleaned.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[3..];
        }

        if (cleaned.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[..^3];
        }
        cleaned = cleaned.Trim();

        var array = JsonNode.Parse(cleaned) as JsonArray;
        if (array == null || array.Count == 0) return null;

        var subTasks = new List<DecomposedSubTask>();
        decimal allocatedSum = 0m;

        for (int i = 0; i < array.Count; i++)
        {
            var node = array[i];
            if (node == null) continue;

            var agentType = node["agentType"]?.GetValue<string>() ?? GrowthAgentTypes.SeoSpecialist;
            var title = node["title"]?.GetValue<string>() ?? "Subtask";
            var description = node["description"]?.GetValue<string>() ?? string.Empty;

            decimal budget;
            if (i == array.Count - 1)
            {
                budget = Math.Max(0m, totalBudget - allocatedSum);
            }
            else
            {
                var ratio = node["allocatedBudgetRatio"]?.GetValue<decimal>() ?? 0.25m;
                budget = Math.Round(totalBudget * ratio, 2);
                allocatedSum += budget;
            }

            subTasks.Add(CreateSubTask(missionId, agentType, title, description, budget, policy));
        }

        return subTasks.Count > 0 ? subTasks : null;
    }

    private static List<DecomposedSubTask> FallbackHeuristicSubTasks(
        string missionId,
        GrowthMissionObjective objective,
        decimal budget,
        FinancialGuardrailPolicy policy)
    {
        var seoBudget = Math.Round(budget * 0.15m, 2);
        var contentBudget = Math.Round(budget * 0.20m, 2);
        var adBudget = Math.Round(budget * 0.50m, 2);
        var automationBudget = Math.Max(0m, budget - (seoBudget + contentBudget + adBudget));

        return
        [
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
        ];
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
            existingPlan = LoadMissionFromDb(tenantId, missionId);
        }

        if (existingPlan == null)
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
                updatedSubTasks.Add(task with { Status = GrowthTaskStatus.Executed });
                executedCount++;
                totalApprovedSpend += task.AllocatedBudget;
            }
            else
            {
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

        UpdateMissionInDb(missionId, approvedTaskIds, totalApprovedSpend, executedCount, pendingCount, overallStatus);

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

        if (_store.TryGetValue(StoreKey(tenantId, missionId), out var plan))
        {
            return plan;
        }

        var loaded = LoadMissionFromDb(tenantId, missionId);
        if (loaded != null)
        {
            _store[StoreKey(tenantId, missionId)] = loaded;
            return loaded;
        }

        return null;
    }

    private void PersistMissionPlanToDb(GrowthMissionPlan plan, Guid missionGuid)
    {
        if (_scopeFactory == null && _dbContext == null) return;

        try
        {
            if (_dbContext != null)
            {
                lock (_dbLock)
                {
                    SaveMissionEntity(_dbContext, plan, missionGuid);
                }
            }
            else if (_scopeFactory != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<SGrowingDbContext>();
                if (db != null)
                {
                    SaveMissionEntity(db, plan, missionGuid);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to persist GrowthMissionPlan '{MissionId}' to database.", plan.MissionId);
        }
    }

    private static void SaveMissionEntity(SGrowingDbContext db, GrowthMissionPlan plan, Guid missionGuid)
    {
        var tenantGuid = ResolveTenantGuid(plan.Objective.TenantId);

        var missionEntity = new AgentMission
        {
            Id = missionGuid,
            TenantId = tenantGuid,
            Title = plan.Objective.Title,
            TargetGoal = $"{plan.Objective.TargetMetric}: {plan.Objective.TargetValue}",
            Status = plan.Status == GrowthMissionStatus.AutoApproved ? AgentMissionStatus.Running : AgentMissionStatus.PausedForApproval,
            AutonomyMode = plan.ExceedsAutoApprovalThreshold ? "Human-in-the-Loop" : "Auto-Approve",
            TotalBudgetUsd = plan.TotalEstimatedCostUsd,
            SpentBudgetUsd = 0m,
            TokensConsumed = 0,
            CurrentStepIndex = 0,
            ReasoningTrace = $"[GrowthDirector] Decomposed objective '{plan.Objective.Title}' into {plan.SubTasks.Count} subtasks. Status: {plan.Status}.",
            CreatedAtUtc = plan.CreatedAtUtc
        };

        for (int i = 0; i < plan.SubTasks.Count; i++)
        {
            var task = plan.SubTasks[i];
            var stepEntity = new AgentMissionStep
            {
                Id = Guid.NewGuid(),
                MissionId = missionGuid,
                StepIndex = i,
                ToolName = task.AgentType,
                InputArgumentsJson = JsonSerializer.Serialize(new
                {
                    taskId = task.TaskId,
                    title = task.Title,
                    allocatedBudget = task.AllocatedBudget
                }),
                ReasoningThought = task.Description,
                Status = task.Status == GrowthTaskStatus.AutoApproved ? AgentMissionStepStatus.Pending : AgentMissionStepStatus.AwaitingApproval,
                ApprovalRequired = task.RequiresHumanApproval,
                CostUsd = task.AllocatedBudget,
                CreatedAtUtc = DateTime.UtcNow
            };

            missionEntity.Steps.Add(stepEntity);
            db.AgentMissionSteps.Add(stepEntity);
        }

        db.AgentMissions.Add(missionEntity);
        db.SaveChanges();
    }

    private void UpdateMissionInDb(
        string missionId,
        List<string> approvedTaskIds,
        decimal totalApprovedSpend,
        int executedCount,
        int pendingCount,
        string overallStatus)
    {
        if (_scopeFactory == null && _dbContext == null) return;

        try
        {
            if (!TryExtractGuid(missionId, out var missionGuid)) return;

            if (_dbContext != null)
            {
                lock (_dbLock)
                {
                    ApplyMissionUpdate(_dbContext, missionGuid, approvedTaskIds, totalApprovedSpend, executedCount, pendingCount);
                }
            }
            else if (_scopeFactory != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<SGrowingDbContext>();
                if (db != null)
                {
                    ApplyMissionUpdate(db, missionGuid, approvedTaskIds, totalApprovedSpend, executedCount, pendingCount);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to update mission '{MissionId}' in database.", missionId);
        }
    }

    private static void ApplyMissionUpdate(
        SGrowingDbContext db,
        Guid missionGuid,
        List<string> approvedTaskIds,
        decimal totalApprovedSpend,
        int executedCount,
        int pendingCount)
    {
        var missionEntity = db.AgentMissions
            .Include(m => m.Steps)
            .FirstOrDefault(m => m.Id == missionGuid);

        if (missionEntity == null) return;

        var approvedSet = new HashSet<string>(approvedTaskIds, StringComparer.OrdinalIgnoreCase);

        foreach (var step in missionEntity.Steps)
        {
            string? stepTaskId = null;
            if (!string.IsNullOrWhiteSpace(step.InputArgumentsJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(step.InputArgumentsJson);
                    if (doc.RootElement.TryGetProperty("taskId", out var tidProp) && tidProp.ValueKind == JsonValueKind.String)
                        stepTaskId = tidProp.GetString();
                }
                catch { }
            }

            bool isApproved = (stepTaskId != null && approvedSet.Contains(stepTaskId))
                || approvedSet.Contains(step.ToolName)
                || approvedSet.Any(tid => tid.Contains(step.ToolName, StringComparison.OrdinalIgnoreCase));

            if (isApproved)
            {
                step.Status = AgentMissionStepStatus.Completed;
                step.ExecutedAtUtc = DateTime.UtcNow;
                step.ApprovedAtUtc = DateTime.UtcNow;
                step.ApprovedBy = "GrowthDirector";
            }
        }

        missionEntity.SpentBudgetUsd = totalApprovedSpend;
        missionEntity.Status = pendingCount == 0 && executedCount > 0 ? AgentMissionStatus.Completed : AgentMissionStatus.Running;
        if (missionEntity.Status == AgentMissionStatus.Completed)
        {
            missionEntity.CompletedAtUtc = DateTime.UtcNow;
        }

        db.SaveChanges();
    }

    private GrowthMissionPlan? LoadMissionFromDb(string tenantId, string missionId)
    {
        if (_scopeFactory == null && _dbContext == null) return null;

        try
        {
            if (!TryExtractGuid(missionId, out var missionGuid)) return null;

            if (_dbContext != null)
            {
                lock (_dbLock)
                {
                    return QueryMissionFromDb(_dbContext, tenantId, missionId, missionGuid);
                }
            }
            else if (_scopeFactory != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<SGrowingDbContext>();
                if (db != null)
                {
                    return QueryMissionFromDb(db, tenantId, missionId, missionGuid);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load mission '{MissionId}' from database.", missionId);
        }

        return null;
    }

    private static GrowthMissionPlan? QueryMissionFromDb(SGrowingDbContext db, string tenantId, string missionId, Guid missionGuid)
    {
        var tenantGuid = ResolveTenantGuid(tenantId);
        var missionEntity = db.AgentMissions
            .Include(m => m.Steps)
            .FirstOrDefault(m => m.Id == missionGuid && m.TenantId == tenantGuid);

        if (missionEntity == null) return null;

        var subTasks = missionEntity.Steps
            .OrderBy(s => s.StepIndex)
            .Select(s =>
            {
                string taskId = $"task-{missionId}-{s.ToolName.ToLowerInvariant()}";
                string title = s.ToolName;
                if (!string.IsNullOrWhiteSpace(s.InputArgumentsJson))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(s.InputArgumentsJson);
                        if (doc.RootElement.TryGetProperty("taskId", out var tidProp) && tidProp.ValueKind == JsonValueKind.String)
                            taskId = tidProp.GetString() ?? taskId;
                        if (doc.RootElement.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
                            title = titleProp.GetString() ?? title;
                    }
                    catch { }
                }

                return new DecomposedSubTask(
                    TaskId: taskId,
                    AgentType: s.ToolName,
                    Title: title,
                    Description: s.ReasoningThought,
                    AllocatedBudget: s.CostUsd,
                    RequiresHumanApproval: s.ApprovalRequired,
                    Status: s.Status == AgentMissionStepStatus.Completed ? GrowthTaskStatus.Executed : GrowthTaskStatus.PendingApproval
                );
            })
            .ToList();

        var objective = new GrowthMissionObjective(
            ObjectiveId: missionEntity.Id.ToString("N"),
            TenantId: tenantId,
            Title: missionEntity.Title,
            TargetMetric: missionEntity.TargetGoal,
            TargetValue: 0m,
            MaxBudgetUsd: missionEntity.TotalBudgetUsd,
            DeadlineUtc: DateTime.UtcNow.AddDays(30)
        );

        return new GrowthMissionPlan(
            MissionId: missionId,
            Objective: objective,
            SubTasks: subTasks,
            TotalEstimatedCostUsd: missionEntity.TotalBudgetUsd,
            ExceedsAutoApprovalThreshold: missionEntity.AutonomyMode == "Human-in-the-Loop",
            Status: missionEntity.Status == AgentMissionStatus.Completed ? GrowthMissionStatus.Executed : GrowthMissionStatus.PendingApproval,
            CreatedAtUtc: missionEntity.CreatedAtUtc
        );
    }

    private static Guid ResolveTenantGuid(string tenantId)
    {
        if (Guid.TryParse(tenantId, out var parsed))
            return parsed;

        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(tenantId));
        return new Guid(bytes);
    }

    private static bool TryExtractGuid(string missionId, out Guid guid)
    {
        guid = Guid.Empty;
        if (string.IsNullOrWhiteSpace(missionId)) return false;
        var raw = missionId.StartsWith("msn_", StringComparison.OrdinalIgnoreCase) ? missionId[4..] : missionId;
        return Guid.TryParse(raw, out guid);
    }
}
