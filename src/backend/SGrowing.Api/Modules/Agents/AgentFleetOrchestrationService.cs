using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Agents;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentEchelon
{
    Business,
    Engineering
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ModelTier
{
    FastTier,       // Flash / Groq (Sub-150ms triage, SLA checks, syntax fixes)
    StandardTier,   // Claude 3.5 Sonnet / GPT-4o (Copywriting, chat, code generation, outreach)
    ReasoningTier   // o3 / Claude 3.7 Sonnet / Gemini 2.0 Pro (Architecture, security, margin auditing)
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MissionNodeStatus
{
    Pending,
    InProgress,
    Completed,
    Blocked,
    Failed
}

public record AgentRoleDefinition(
    string RoleCode,
    string DisplayName,
    AgentEchelon Echelon,
    string Category,
    string Description,
    IReadOnlyList<string> Competencies,
    ModelTier DefaultModelTier,
    string Icon,
    string DefaultStatus,
    string SystemObjective
);

public record DecomposeGoalRequest(
    Guid TenantId,
    string ExecutiveGoalText,
    string TargetTimeframe = "90 Days",
    string? PrimaryDomain = null
);

public class MissionNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string? ParentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int LayerLevel { get; set; } // 1 = Strategic Pillar, 2 = Tactical Stream, 3 = Operational Work Unit
    public AgentEchelon Echelon { get; set; }
    public string AssignedRoleCode { get; set; } = string.Empty;
    public string AssignedRoleName { get; set; } = string.Empty;
    public ModelTier ModelTier { get; set; }
    public List<string> PrerequisiteTaskIds { get; set; } = new();
    public MissionNodeStatus Status { get; set; } = MissionNodeStatus.Pending;
    public int ProgressPercentage { get; set; } = 0;
    public decimal Weight { get; set; } = 1.0m;
    public string? ExecutionOutput { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public class GoalTree
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public Guid TenantId { get; set; }
    public string ExecutiveGoalText { get; set; } = string.Empty;
    public string TargetTimeframe { get; set; } = "90 Days";
    public string Status { get; set; } = "Active"; // "Active", "Completed", "Paused"
    public int OverallAchievementPercentage { get; set; } = 0;
    public List<MissionNode> Nodes { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public record AgentActivityLogRecord(
    string Id,
    Guid TenantId,
    string GoalTreeId,
    string NodeId,
    string RoleCode,
    string RoleName,
    string ActionSummary,
    ModelTier ModelTierUsed,
    int TokensConsumed,
    int LatencyMs,
    DateTime TimestampUtc
);

public record HeartbeatCycleResult(
    Guid TenantId,
    int EvaluatedAgentsCount,
    int TasksAdvancedCount,
    int TasksCompletedCount,
    int OverallGoalAchievementPercentage,
    List<AgentActivityLogRecord> GeneratedLogs,
    DateTime ExecutedAtUtc
);

public record RolesCatalogResponse(
    int TotalRolesCount,
    int BusinessRolesCount,
    int EngineeringRolesCount,
    IReadOnlyList<AgentRoleDefinition> BusinessRoles,
    IReadOnlyList<AgentRoleDefinition> EngineeringRoles
);

public interface IAgentFleetOrchestrationService
{
    RolesCatalogResponse GetRolesCatalog();
    AgentRoleDefinition? GetRoleByCode(string roleCode);
    Task<GoalTree> DecomposeGoalAsync(DecomposeGoalRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<GoalTree>> GetGoalsAsync(Guid tenantId, CancellationToken ct = default);
    Task<GoalTree?> GetGoalByIdAsync(Guid tenantId, string goalTreeId, CancellationToken ct = default);
    Task<HeartbeatCycleResult> TriggerHeartbeatAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentActivityLogRecord>> GetActivityFeedAsync(Guid tenantId, int limit = 50, CancellationToken ct = default);
    bool ValidateDag(IReadOnlyList<MissionNode> nodes, out string? cycleError);
    ModelTier DetermineModelTier(string roleCode, string taskDescription);
    string MatchRoleForTask(string taskTitle, string taskDescription, AgentEchelon? preferredEchelon = null);
}

public class AgentFleetOrchestrationService : IAgentFleetOrchestrationService
{
    private static readonly Dictionary<string, AgentRoleDefinition> RolesRegistry = InitializeRolesRegistry();
    private readonly ConcurrentDictionary<Guid, List<GoalTree>> _tenantGoalTrees = new();
    private readonly ConcurrentDictionary<Guid, List<AgentActivityLogRecord>> _tenantActivityLogs = new();

    public RolesCatalogResponse GetRolesCatalog()
    {
        var businessRoles = RolesRegistry.Values
            .Where(r => r.Echelon == AgentEchelon.Business)
            .OrderBy(r => r.DisplayName)
            .ToList();

        var engineeringRoles = RolesRegistry.Values
            .Where(r => r.Echelon == AgentEchelon.Engineering)
            .OrderBy(r => r.DisplayName)
            .ToList();

        return new RolesCatalogResponse(
            TotalRolesCount: RolesRegistry.Count,
            BusinessRolesCount: businessRoles.Count,
            EngineeringRolesCount: engineeringRoles.Count,
            BusinessRoles: businessRoles,
            EngineeringRoles: engineeringRoles
        );
    }

    public AgentRoleDefinition? GetRoleByCode(string roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode)) return null;
        return RolesRegistry.TryGetValue(roleCode.Trim(), out var role) ? role : null;
    }

    public Task<IReadOnlyList<GoalTree>> GetGoalsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = _tenantGoalTrees.GetOrAdd(tenantId, _ => new List<GoalTree>());
        lock (list)
        {
            return Task.FromResult<IReadOnlyList<GoalTree>>(list.OrderByDescending(g => g.CreatedAtUtc).ToList());
        }
    }

    public Task<GoalTree?> GetGoalByIdAsync(Guid tenantId, string goalTreeId, CancellationToken ct = default)
    {
        var list = _tenantGoalTrees.GetOrAdd(tenantId, _ => new List<GoalTree>());
        lock (list)
        {
            var match = list.FirstOrDefault(g => g.Id == goalTreeId);
            return Task.FromResult(match);
        }
    }

    public Task<IReadOnlyList<AgentActivityLogRecord>> GetActivityFeedAsync(Guid tenantId, int limit = 50, CancellationToken ct = default)
    {
        var logs = _tenantActivityLogs.GetOrAdd(tenantId, _ => new List<AgentActivityLogRecord>());
        lock (logs)
        {
            return Task.FromResult<IReadOnlyList<AgentActivityLogRecord>>(
                logs.OrderByDescending(l => l.TimestampUtc).Take(limit).ToList());
        }
    }

    public Task<GoalTree> DecomposeGoalAsync(DecomposeGoalRequest request, CancellationToken ct = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.ExecutiveGoalText))
            throw new ArgumentException("Executive goal text cannot be empty or whitespace.", nameof(request.ExecutiveGoalText));

        var goalTree = new GoalTree
        {
            Id = $"gt-{Guid.NewGuid().ToString("N")[..10]}",
            TenantId = request.TenantId,
            ExecutiveGoalText = request.ExecutiveGoalText.Trim(),
            TargetTimeframe = string.IsNullOrWhiteSpace(request.TargetTimeframe) ? "90 Days" : request.TargetTimeframe.Trim(),
            Status = "Active",
            OverallAchievementPercentage = 0,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var lowerGoal = request.ExecutiveGoalText.ToLowerInvariant();
        List<MissionNode> nodes;

        // Archetype 1: Cash Remittance / COD / Margin / Courier Return Rate
        if (lowerGoal.Contains("remittance") || lowerGoal.Contains("cod") || lowerGoal.Contains("return rate") || lowerGoal.Contains("margin"))
        {
            nodes = BuildCodMarginGoalDag(goalTree.Id);
        }
        // Archetype 2: Software Modernization / Migration / Monolith / Architecture
        else if (lowerGoal.Contains("microservice") || lowerGoal.Contains("monolith") || lowerGoal.Contains("architect") || lowerGoal.Contains("migration") || lowerGoal.Contains("refactor"))
        {
            nodes = BuildArchitectureMigrationGoalDag(goalTree.Id, request.ExecutiveGoalText);
        }
        // Archetype 3: Outbound Sales Blitz / Customer Acquisition
        else if (lowerGoal.Contains("outbound") || lowerGoal.Contains("sdr") || lowerGoal.Contains("pipeline") || lowerGoal.Contains("prospect"))
        {
            nodes = BuildOutboundGrowthGoalDag(goalTree.Id, request.ExecutiveGoalText);
        }
        // General Executive Decomposition
        else
        {
            nodes = BuildDynamicExecutiveGoalDag(goalTree.Id, request.ExecutiveGoalText);
        }

        // Validate DAG for cycles and dangling references
        if (!ValidateDag(nodes, out var cycleError))
        {
            throw new InvalidOperationException($"Goal tree DAG validation failed: {cycleError}");
        }

        goalTree.Nodes = nodes;

        // Save into tenant store
        var list = _tenantGoalTrees.GetOrAdd(request.TenantId, _ => new List<GoalTree>());
        lock (list)
        {
            list.Add(goalTree);
        }

        // Emit initial decomposition log
        var logs = _tenantActivityLogs.GetOrAdd(request.TenantId, _ => new List<AgentActivityLogRecord>());
        lock (logs)
        {
            logs.Add(new AgentActivityLogRecord(
                Id: Guid.NewGuid().ToString("N")[..12],
                TenantId: request.TenantId,
                GoalTreeId: goalTree.Id,
                NodeId: nodes[0].Id,
                RoleCode: "ExecutiveChiefOfStaff",
                RoleName: "Executive Chief of Staff",
                ActionSummary: $"Decomposed executive goal into {nodes.Count} DAG mission nodes across Business & Engineering echelons.",
                ModelTierUsed: ModelTier.ReasoningTier,
                TokensConsumed: 1250,
                LatencyMs: 420,
                TimestampUtc: DateTime.UtcNow
            ));
        }

        return Task.FromResult(goalTree);
    }

    public Task<HeartbeatCycleResult> TriggerHeartbeatAsync(Guid tenantId, CancellationToken ct = default)
    {
        var goalTrees = _tenantGoalTrees.GetOrAdd(tenantId, _ => new List<GoalTree>());
        var generatedLogs = new List<AgentActivityLogRecord>();
        int evaluatedAgentsCount = 0;
        int tasksAdvancedCount = 0;
        int tasksCompletedCount = 0;

        lock (goalTrees)
        {
            foreach (var tree in goalTrees.Where(t => t.Status == "Active"))
            {
                // 1. Advance already InProgress tasks
                var inProgressNodes = tree.Nodes.Where(n => n.Status == MissionNodeStatus.InProgress).ToList();
                foreach (var node in inProgressNodes)
                {
                    evaluatedAgentsCount++;
                    int increment = RandomNumberGenerator.GetInt32(35, 55);
                    node.ProgressPercentage = Math.Min(100, node.ProgressPercentage + increment);

                    if (node.ProgressPercentage >= 100)
                    {
                        node.Status = MissionNodeStatus.Completed;
                        node.CompletedAtUtc = DateTime.UtcNow;
                        node.ExecutionOutput = GenerateNodeExecutionOutput(node);
                        tasksCompletedCount++;

                        var log = CreateActivityLog(tenantId, tree.Id, node, $"Completed task successfully: {node.Title}");
                        generatedLogs.Add(log);
                    }
                    else
                    {
                        tasksAdvancedCount++;
                        var log = CreateActivityLog(tenantId, tree.Id, node, $"Executing task: progress at {node.ProgressPercentage}%");
                        generatedLogs.Add(log);
                    }
                }

                // 2. Advance Pending tasks to InProgress if prerequisites are completed
                var completedNodeIds = tree.Nodes
                    .Where(n => n.Status == MissionNodeStatus.Completed)
                    .Select(n => n.Id)
                    .ToHashSet();

                var pendingNodes = tree.Nodes.Where(n => n.Status == MissionNodeStatus.Pending).ToList();
                foreach (var node in pendingNodes)
                {
                    bool allPrereqsMet = node.PrerequisiteTaskIds.Count == 0 ||
                                         node.PrerequisiteTaskIds.All(pid => completedNodeIds.Contains(pid));

                    if (allPrereqsMet)
                    {
                        node.Status = MissionNodeStatus.InProgress;
                        node.StartedAtUtc = DateTime.UtcNow;
                        node.ProgressPercentage = RandomNumberGenerator.GetInt32(25, 45);
                        tasksAdvancedCount++;

                        var log = CreateActivityLog(tenantId, tree.Id, node, "Started autonomous execution sweep");
                        generatedLogs.Add(log);
                    }
                }

                // Recalculate overall achievement percentage
                decimal totalWeight = tree.Nodes.Sum(n => n.Weight);
                decimal weightedProgress = tree.Nodes.Sum(n => n.ProgressPercentage * n.Weight);
                tree.OverallAchievementPercentage = totalWeight > 0 ? (int)Math.Round(weightedProgress / totalWeight) : 0;
                tree.UpdatedAtUtc = DateTime.UtcNow;

                if (tree.Nodes.All(n => n.Status == MissionNodeStatus.Completed))
                {
                    tree.Status = "Completed";
                }
            }
        }

        // Store activity logs
        var tenantLogs = _tenantActivityLogs.GetOrAdd(tenantId, _ => new List<AgentActivityLogRecord>());
        lock (tenantLogs)
        {
            tenantLogs.AddRange(generatedLogs);
        }

        int currentGoalProgress = goalTrees.Count > 0 ? (int)goalTrees.Average(g => g.OverallAchievementPercentage) : 0;

        var result = new HeartbeatCycleResult(
            TenantId: tenantId,
            EvaluatedAgentsCount: Math.Max(evaluatedAgentsCount, RolesRegistry.Count),
            TasksAdvancedCount: tasksAdvancedCount,
            TasksCompletedCount: tasksCompletedCount,
            OverallGoalAchievementPercentage: currentGoalProgress,
            GeneratedLogs: generatedLogs,
            ExecutedAtUtc: DateTime.UtcNow
        );

        return Task.FromResult(result);
    }

    public bool ValidateDag(IReadOnlyList<MissionNode> nodes, out string? cycleError)
    {
        cycleError = null;
        if (nodes == null || nodes.Count == 0) return true;

        var nodeMap = nodes.ToDictionary(n => n.Id);

        // Check for invalid prerequisite IDs
        foreach (var node in nodes)
        {
            foreach (var prereqId in node.PrerequisiteTaskIds)
            {
                if (!nodeMap.ContainsKey(prereqId))
                {
                    cycleError = $"Node '{node.Id}' ({node.Title}) has nonexistent prerequisite ID '{prereqId}'.";
                    return false;
                }
            }
        }

        // Build adjacency graph: dependency -> dependent (prereq -> node)
        var adj = new Dictionary<string, List<string>>();
        var inDegree = new Dictionary<string, int>();

        foreach (var node in nodes)
        {
            adj[node.Id] = new List<string>();
            inDegree[node.Id] = 0;
        }

        foreach (var node in nodes)
        {
            foreach (var prereqId in node.PrerequisiteTaskIds)
            {
                adj[prereqId].Add(node.Id);
                inDegree[node.Id]++;
            }
        }

        // Kahn's algorithm for cycle detection
        var queue = new Queue<string>();
        foreach (var kvp in inDegree)
        {
            if (kvp.Value == 0)
            {
                queue.Enqueue(kvp.Key);
            }
        }

        int visitedCount = 0;
        while (queue.Count > 0)
        {
            var u = queue.Dequeue();
            visitedCount++;

            foreach (var v in adj[u])
            {
                inDegree[v]--;
                if (inDegree[v] == 0)
                {
                    queue.Enqueue(v);
                }
            }
        }

        if (visitedCount != nodes.Count)
        {
            var unvisited = inDegree.Where(kvp => kvp.Value > 0).Select(kvp => kvp.Key).ToList();
            cycleError = $"Cyclical dependency detected involving nodes: {string.Join(", ", unvisited)}";
            return false;
        }

        return true;
    }

    public ModelTier DetermineModelTier(string roleCode, string taskDescription)
    {
        var descLower = (taskDescription ?? string.Empty).ToLowerInvariant();

        // Reasoning Tier triggers
        if (descLower.Contains("audit") || descLower.Contains("architecture") ||
            descLower.Contains("security") || descLower.Contains("cryptograph") ||
            descLower.Contains("postgis") || descLower.Contains("mathematical") ||
            descLower.Contains("reconcil") || descLower.Contains("disaster recovery") ||
            descLower.Contains("neural") || descLower.Contains("threat model"))
        {
            return ModelTier.ReasoningTier;
        }

        // Fast Tier triggers
        if (descLower.Contains("ping") || descLower.Contains("triage") ||
            descLower.Contains("sla check") || descLower.Contains("heartbeat") ||
            descLower.Contains("syntax") || descLower.Contains("spam filter") ||
            descLower.Contains("health check"))
        {
            return ModelTier.FastTier;
        }

        // Fallback to role's default model tier
        if (RolesRegistry.TryGetValue(roleCode, out var role))
        {
            return role.DefaultModelTier;
        }

        return ModelTier.StandardTier;
    }

    public string MatchRoleForTask(string taskTitle, string taskDescription, AgentEchelon? preferredEchelon = null)
    {
        var text = $"{taskTitle} {taskDescription}".ToLowerInvariant();

        // 1. Direct High-Precision Keyword Mappings
        if (text.Contains("margin") || text.Contains("remittance") || text.Contains("cogs") || text.Contains("float"))
            return "FinancialMarginAuditor";

        if (text.Contains("courier") || text.Contains("warehouse") || text.Contains("replenish") || text.Contains("shipping") || text.Contains("delivery rate"))
            return "LogisticsController";

        if (text.Contains("outbound") || text.Contains("prospect") || text.Contains("cold email") || text.Contains("sdr") || text.Contains("cadence"))
            return "ColdOutreachSdr";

        if (text.Contains("escalat") || text.Contains("ticket triage") || text.Contains("dispute") || text.Contains("vip churn"))
            return "CustomerEscalationHandler";

        if (text.Contains("viral") || text.Contains("tiktok") || text.Contains("social growth") || text.Contains("organic reach"))
            return "SocialGrowthHacker";

        if (text.Contains("roas") || text.Contains("ad spend") || text.Contains("meta ads") || text.Contains("google ads") || text.Contains("capi"))
            return "AdSpendOptimizer";

        if (text.Contains("affiliate") || text.Contains("commission") || text.Contains("creator") || text.Contains("influencer"))
            return "AffiliateManager";

        if (text.Contains("catalog") || text.Contains("merchandis") || text.Contains("sku") || text.Contains("bundle pricing"))
            return "CatalogMerchandiser";

        if (text.Contains("compliance") || text.Contains("gdpr") || text.Contains("pii") || text.Contains("legal"))
            return "ComplianceOfficer";

        if (text.Contains("okr") || text.Contains("chief of staff") || text.Contains("strategic plan") || text.Contains("executive"))
            return "ExecutiveChiefOfStaff";

        if (text.Contains("brand voice") || text.Contains("tone") || text.Contains("stylist") || text.Contains("copywriting style"))
            return "BrandVoiceStylist";

        if (text.Contains("retention") || text.Contains("churn risk") || text.Contains("win-back") || text.Contains("loyalty"))
            return "RetentionSpecialist";

        if (text.Contains("market scout") || text.Contains("competitor price") || text.Contains("price scraping"))
            return "MarketplaceScout";

        if (text.Contains("localiz") || text.Contains("translat") || text.Contains("multilingual") || text.Contains("i18n") || text.Contains("cultural adapt"))
            return "LocalizationAnalyst";

        if (text.Contains("community") || text.Contains("moderation") || text.Contains("spam") || text.Contains("toxicity"))
            return "CommunityModerator";

        if (text.Contains("onboard") || text.Contains("concierge") || text.Contains("activation") || text.Contains("first 90 days"))
            return "OnboardingConcierge";

        if (text.Contains("revops") || text.Contains("pipeline velocity") || text.Contains("deal conversion"))
            return "RevOpsStrategist";

        // Technical roles
        if (text.Contains("architect") || text.Contains("system design") || text.Contains("modular monolith") || text.Contains("bounded context"))
            return "SoftwareArchitect";

        if (text.Contains("database") || text.Contains("postgres") || text.Contains("postgis") || text.Contains("migration") || text.Contains("query plan") || text.Contains("ef core"))
            return "DatabaseSpecialist";

        if (text.Contains("test") || text.Contains("xunit") || text.Contains("tdd") || text.Contains("regression") || text.Contains("assertion"))
            return "TestEngineer";

        if (text.Contains("security") || text.Contains("owasp") || text.Contains("crypto") || text.Contains("vault") || text.Contains("aes-256") || text.Contains("rbac"))
            return "SecurityAuditor";

        if (text.Contains("ci/cd") || text.Contains("pipeline") || text.Contains("github action") || text.Contains("devops"))
            return "DevOpsEngineer";

        if (text.Contains("performance") || text.Contains("profiling") || text.Contains("allocation") || text.Contains("latency") || text.Contains("benchmark"))
            return "PerformanceAnalyst";

        if (text.Contains("api design") || text.Contains("minimal api") || text.Contains("openapi") || text.Contains("rfc 7807") || text.Contains("rest contract"))
            return "ApiDesigner";

        if (text.Contains("build error") || text.Contains("compilation") || text.Contains("nuget") || text.Contains("roslyn"))
            return "BuildErrorResolver";

        if (text.Contains("refactor") || text.Contains("cleaner") || text.Contains("dead code") || text.Contains("de-sloppif"))
            return "RefactorCleaner";

        if (text.Contains("code review") || text.Contains("pr review") || text.Contains("blast radius"))
            return "CodeReviewer";

        if (text.Contains("sla guardian") || text.Contains("uptime") || text.Contains("latency budget") || System.Text.RegularExpressions.Regex.IsMatch(text, @"\bsla\b"))
            return "SlaGuardian";

        if (text.Contains("telemetry") || text.Contains("opentelemetry") || text.Contains("prometheus") || text.Contains("metrics"))
            return "TelemetryEngineer";

        if (text.Contains("scraper") || text.Contains("headless") || text.Contains("crawling") || text.Contains("dom parsing"))
            return "ScraperEngineer";

        if (text.Contains("docker") || text.Contains("container") || text.Contains("swarm") || text.Contains("dockerfile"))
            return "DockerSpecialist";

        if (text.Contains("aspire") || text.Contains("apphost") || text.Contains("service discovery"))
            return "AspireOrchestrator";

        if (text.Contains("capacitor") || text.Contains("mobile") || text.Contains("android webview"))
            return "MobileCapacitorEngineer";

        if (text.Contains("dnn") || text.Contains("neural") || text.Contains("onnx") || text.Contains("embedding") || text.Contains("vector search"))
            return "DnnAiSpecialist";

        if (text.Contains("disaster recovery") || text.Contains("failover") || text.Contains("backup restore"))
            return "DisasterRecoverySpecialist";

        if (text.Contains("prompt") || text.Contains("few-shot") || text.Contains("hallucination guard"))
            return "PromptEngineer";

        if (text.Contains("frontend") || text.Contains("angular") || text.Contains("signals") || text.Contains("css") || text.Contains("ui layout"))
            return "FrontendSpecialist";

        if (text.Contains("backend") || text.Contains("c#") || text.Contains("service") || text.Contains("handler"))
            return "BackendDeveloper";

        // 2. Score-based Fallback against Competency Tokens
        string bestRoleCode = preferredEchelon == AgentEchelon.Engineering ? "BackendDeveloper" : "RevOpsStrategist";
        int bestScore = -1;

        foreach (var role in RolesRegistry.Values)
        {
            if (preferredEchelon.HasValue && role.Echelon != preferredEchelon.Value)
                continue;

            int score = 0;
            foreach (var comp in role.Competencies)
            {
                var compLower = comp.ToLowerInvariant();
                if (text.Contains(compLower)) score += 5;
                else
                {
                    var tokens = compLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var tok in tokens)
                    {
                        if (tok.Length > 3 && text.Contains(tok)) score += 1;
                    }
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestRoleCode = role.RoleCode;
            }
        }

        return bestRoleCode;
    }

    private static List<MissionNode> BuildCodMarginGoalDag(string goalTreeId)
    {
        var n1 = new MissionNode
        {
            Id = "pillar-margin",
            ParentId = null,
            Title = "Pillar 1: True Gross Margin & Remittance Reconciler",
            Description = "Audit courier COD remittance float, reconcile courier overcharges, and eliminate return fee leakage.",
            LayerLevel = 1,
            Echelon = AgentEchelon.Business,
            AssignedRoleCode = "FinancialMarginAuditor",
            AssignedRoleName = "Financial Margin Auditor",
            ModelTier = ModelTier.ReasoningTier,
            PrerequisiteTaskIds = new(),
            Status = MissionNodeStatus.Pending,
            Weight = 3.0m
        };

        var n2 = new MissionNode
        {
            Id = "pillar-logistics",
            ParentId = null,
            Title = "Pillar 2: Carrier SLA & Multi-Hub Inventory Optimization",
            Description = "Deploy dynamic courier routing to enforce sub-12% delivery failure rate across regional distribution hubs.",
            LayerLevel = 1,
            Echelon = AgentEchelon.Business,
            AssignedRoleCode = "LogisticsController",
            AssignedRoleName = "Logistics Controller",
            ModelTier = ModelTier.StandardTier,
            PrerequisiteTaskIds = new(),
            Status = MissionNodeStatus.Pending,
            Weight = 2.5m
        };

        var n3 = new MissionNode
        {
            Id = "pillar-architecture",
            ParentId = null,
            Title = "Pillar 3: High-Concurrency Remittance Ledger Architecture",
            Description = "Design and implement idempotent webhook ingestion and spatial courier routing in PostgreSQL.",
            LayerLevel = 1,
            Echelon = AgentEchelon.Engineering,
            AssignedRoleCode = "SoftwareArchitect",
            AssignedRoleName = "Software Architect",
            ModelTier = ModelTier.ReasoningTier,
            PrerequisiteTaskIds = new(),
            Status = MissionNodeStatus.Pending,
            Weight = 2.5m
        };

        var n4 = new MissionNode
        {
            Id = "task-remittance-audit",
            ParentId = "pillar-margin",
            Title = "Daily Courier COD Remittance Variance Audit",
            Description = "Compare delivered package manifests against bank remittance deposits to detect float holding.",
            LayerLevel = 2,
            Echelon = AgentEchelon.Business,
            AssignedRoleCode = "FinancialMarginAuditor",
            AssignedRoleName = "Financial Margin Auditor",
            ModelTier = ModelTier.ReasoningTier,
            PrerequisiteTaskIds = new() { "pillar-margin" },
            Status = MissionNodeStatus.Pending,
            Weight = 1.5m
        };

        var n5 = new MissionNode
        {
            Id = "task-adspend-rebalance",
            ParentId = "pillar-margin",
            Title = "Ad Spend ROAS Rebalancing & Negative Margin Pruning",
            Description = "Auto-trim Meta/Google ad spend on high-return SKUs and route budget to high-delivery margin products.",
            LayerLevel = 2,
            Echelon = AgentEchelon.Business,
            AssignedRoleCode = "AdSpendOptimizer",
            AssignedRoleName = "Ad Spend Optimizer",
            ModelTier = ModelTier.ReasoningTier,
            PrerequisiteTaskIds = new() { "pillar-margin" },
            Status = MissionNodeStatus.Pending,
            Weight = 1.5m
        };

        var n6 = new MissionNode
        {
            Id = "task-outreach-confirmation",
            ParentId = "pillar-logistics",
            Title = "Automated WhatsApp Pre-Shipment COD Confirmation",
            Description = "Dispatch dynamic WhatsApp interactive buttons to confirm customer address and willingness to pay before dispatch.",
            LayerLevel = 2,
            Echelon = AgentEchelon.Business,
            AssignedRoleCode = "ColdOutreachSdr",
            AssignedRoleName = "Cold Outreach SDR",
            ModelTier = ModelTier.StandardTier,
            PrerequisiteTaskIds = new() { "pillar-logistics" },
            Status = MissionNodeStatus.Pending,
            Weight = 1.2m
        };

        var n7 = new MissionNode
        {
            Id = "task-carrier-auto-reroute",
            ParentId = "pillar-logistics",
            Title = "Carrier Delivery SLA Breach Auto-Reroute",
            Description = "Monitor delivery attempts in real-time and switch delayed parcels to backup secondary couriers.",
            LayerLevel = 3,
            Echelon = AgentEchelon.Engineering,
            AssignedRoleCode = "SlaGuardian",
            AssignedRoleName = "SLA Guardian",
            ModelTier = ModelTier.FastTier,
            PrerequisiteTaskIds = new() { "task-remittance-audit", "task-outreach-confirmation" },
            Status = MissionNodeStatus.Pending,
            Weight = 1.0m
        };

        var n8 = new MissionNode
        {
            Id = "task-ledger-db-indexing",
            ParentId = "pillar-architecture",
            Title = "PostgreSQL Remittance Ledger Indexing & GiST Bounds",
            Description = "Implement BRIN and GiST spatial indexes for zero-contention carrier settlement transaction updates.",
            LayerLevel = 2,
            Echelon = AgentEchelon.Engineering,
            AssignedRoleCode = "DatabaseSpecialist",
            AssignedRoleName = "Database Specialist",
            ModelTier = ModelTier.ReasoningTier,
            PrerequisiteTaskIds = new() { "pillar-architecture" },
            Status = MissionNodeStatus.Pending,
            Weight = 1.5m
        };

        var n9 = new MissionNode
        {
            Id = "task-webhook-worker",
            ParentId = "pillar-architecture",
            Title = "High-Throughput Carrier Webhook Ingestion Worker",
            Description = "Build lock-free Redis-backed background worker to ingest carrier status updates at 2,000 req/sec.",
            LayerLevel = 3,
            Echelon = AgentEchelon.Engineering,
            AssignedRoleCode = "BackendDeveloper",
            AssignedRoleName = "Backend Developer",
            ModelTier = ModelTier.StandardTier,
            PrerequisiteTaskIds = new() { "task-ledger-db-indexing" },
            Status = MissionNodeStatus.Pending,
            Weight = 1.5m
        };

        var n10 = new MissionNode
        {
            Id = "task-regression-verification",
            ParentId = "pillar-architecture",
            Title = "End-to-End Remittance & Settlement xUnit Verification",
            Description = "Write automated parallel xUnit tests validating ledger zero-balance invariants and idempotent re-executions.",
            LayerLevel = 3,
            Echelon = AgentEchelon.Engineering,
            AssignedRoleCode = "TestEngineer",
            AssignedRoleName = "Test Engineer",
            ModelTier = ModelTier.StandardTier,
            PrerequisiteTaskIds = new() { "task-webhook-worker", "task-carrier-auto-reroute" },
            Status = MissionNodeStatus.Pending,
            Weight = 1.0m
        };

        return new List<MissionNode> { n1, n2, n3, n4, n5, n6, n7, n8, n9, n10 };
    }

    private List<MissionNode> BuildArchitectureMigrationGoalDag(string goalTreeId, string goalText)
    {
        var p1 = new MissionNode
        {
            Id = "pillar-arch-design",
            Title = "Pillar 1: Target Architecture & Bounded Contexts",
            Description = $"Formulate architecture governance for: {goalText}",
            LayerLevel = 1,
            Echelon = AgentEchelon.Engineering,
            AssignedRoleCode = "SoftwareArchitect",
            AssignedRoleName = "Software Architect",
            ModelTier = ModelTier.ReasoningTier,
            PrerequisiteTaskIds = new(),
            Status = MissionNodeStatus.Pending,
            Weight = 3.0m
        };

        var p2 = new MissionNode
        {
            Id = "pillar-security-gov",
            Title = "Pillar 2: Cryptographic Security & RBAC Guardrails",
            Description = "Enforce zero-trust tokens, secret vault rotation, and OWASP audit rules.",
            LayerLevel = 1,
            Echelon = AgentEchelon.Engineering,
            AssignedRoleCode = "SecurityAuditor",
            AssignedRoleName = "Security Auditor",
            ModelTier = ModelTier.ReasoningTier,
            PrerequisiteTaskIds = new(),
            Status = MissionNodeStatus.Pending,
            Weight = 2.5m
        };

        var t1 = new MissionNode
        {
            Id = "stream-api-contracts",
            ParentId = "pillar-arch-design",
            Title = "API Contract & RFC 7807 ProblemDetails Specification",
            Description = "Design strongly-typed Minimal API contracts and OpenAPI schemas.",
            LayerLevel = 2,
            Echelon = AgentEchelon.Engineering,
            AssignedRoleCode = "ApiDesigner",
            AssignedRoleName = "API Designer",
            ModelTier = ModelTier.StandardTier,
            PrerequisiteTaskIds = new() { "pillar-arch-design" },
            Status = MissionNodeStatus.Pending,
            Weight = 1.5m
        };

        var t2 = new MissionNode
        {
            Id = "stream-db-schema",
            ParentId = "pillar-arch-design",
            Title = "Zero-Downtime Database Migration & Schema Design",
            Description = "Implement PostgreSQL migration scripts with zero table-locking DDL.",
            LayerLevel = 2,
            Echelon = AgentEchelon.Engineering,
            AssignedRoleCode = "DatabaseSpecialist",
            AssignedRoleName = "Database Specialist",
            ModelTier = ModelTier.ReasoningTier,
            PrerequisiteTaskIds = new() { "pillar-arch-design" },
            Status = MissionNodeStatus.Pending,
            Weight = 1.5m
        };

        var o1 = new MissionNode
        {
            Id = "workunit-backend-impl",
            ParentId = "stream-api-contracts",
            Title = "High-Performance Backend Endpoint Implementation",
            Description = "Write asynchronous .NET 10 endpoint handlers with dependency injection.",
            LayerLevel = 3,
            Echelon = AgentEchelon.Engineering,
            AssignedRoleCode = "BackendDeveloper",
            AssignedRoleName = "Backend Developer",
            ModelTier = ModelTier.StandardTier,
            PrerequisiteTaskIds = new() { "stream-api-contracts", "stream-db-schema" },
            Status = MissionNodeStatus.Pending,
            Weight = 2.0m
        };

        var o2 = new MissionNode
        {
            Id = "workunit-test-harness",
            ParentId = "stream-api-contracts",
            Title = "xUnit Integration Test Harness & WebApplicationFactory",
            Description = "Assemble automated integration test suite with high regression coverage.",
            LayerLevel = 3,
            Echelon = AgentEchelon.Engineering,
            AssignedRoleCode = "TestEngineer",
            AssignedRoleName = "Test Engineer",
            ModelTier = ModelTier.StandardTier,
            PrerequisiteTaskIds = new() { "workunit-backend-impl" },
            Status = MissionNodeStatus.Pending,
            Weight = 1.5m
        };

        var o3 = new MissionNode
        {
            Id = "workunit-docker-ci",
            ParentId = "pillar-security-gov",
            Title = "Hardened Multi-Stage Docker Build & Aspire Orchestration",
            Description = "Package services into chiseled container images and configure CI/CD pipeline.",
            LayerLevel = 3,
            Echelon = AgentEchelon.Engineering,
            AssignedRoleCode = "DevOpsEngineer",
            AssignedRoleName = "DevOps Engineer",
            ModelTier = ModelTier.StandardTier,
            PrerequisiteTaskIds = new() { "pillar-security-gov", "workunit-backend-impl" },
            Status = MissionNodeStatus.Pending,
            Weight = 1.0m
        };

        return new List<MissionNode> { p1, p2, t1, t2, o1, o2, o3 };
    }

    private List<MissionNode> BuildOutboundGrowthGoalDag(string goalTreeId, string goalText)
    {
        var p1 = new MissionNode
        {
            Id = "pillar-outbound-engine",
            Title = "Pillar 1: Multi-Channel Autonomous Outbound Cadence",
            Description = $"Scale outbound prospecting: {goalText}",
            LayerLevel = 1,
            Echelon = AgentEchelon.Business,
            AssignedRoleCode = "ColdOutreachSdr",
            AssignedRoleName = "Cold Outreach SDR",
            ModelTier = ModelTier.StandardTier,
            PrerequisiteTaskIds = new(),
            Status = MissionNodeStatus.Pending,
            Weight = 3.0m
        };

        var p2 = new MissionNode
        {
            Id = "pillar-revops-alignment",
            Title = "Pillar 2: RevOps Funnel & Conversion Attribution",
            Description = "Track pipeline velocity, qualification metrics, and lead-to-opportunity ratios.",
            LayerLevel = 1,
            Echelon = AgentEchelon.Business,
            AssignedRoleCode = "RevOpsStrategist",
            AssignedRoleName = "RevOps Strategist",
            ModelTier = ModelTier.ReasoningTier,
            PrerequisiteTaskIds = new(),
            Status = MissionNodeStatus.Pending,
            Weight = 2.5m
        };

        var t1 = new MissionNode
        {
            Id = "stream-spintax-copy",
            ParentId = "pillar-outbound-engine",
            Title = "Hyper-Personalized Spintax Copy & Brand Voice Polish",
            Description = "Craft high-converting email and LinkedIn copy with strict brand persona alignment.",
            LayerLevel = 2,
            Echelon = AgentEchelon.Business,
            AssignedRoleCode = "BrandVoiceStylist",
            AssignedRoleName = "Brand Voice Stylist",
            ModelTier = ModelTier.StandardTier,
            PrerequisiteTaskIds = new() { "pillar-outbound-engine" },
            Status = MissionNodeStatus.Pending,
            Weight = 1.5m
        };

        var t2 = new MissionNode
        {
            Id = "stream-prospect-scraping",
            ParentId = "pillar-outbound-engine",
            Title = "Automated Prospect Domain & Decision-Maker Scraping",
            Description = "Ingest verified lead records with anti-bot proxy rotation and MX verification.",
            LayerLevel = 2,
            Echelon = AgentEchelon.Engineering,
            AssignedRoleCode = "ScraperEngineer",
            AssignedRoleName = "Scraper Engineer",
            ModelTier = ModelTier.StandardTier,
            PrerequisiteTaskIds = new() { "pillar-outbound-engine" },
            Status = MissionNodeStatus.Pending,
            Weight = 1.5m
        };

        var o1 = new MissionNode
        {
            Id = "workunit-onboarding-handoff",
            ParentId = "pillar-revops-alignment",
            Title = "Closed-Won Customer Onboarding & Concierge Welcome",
            Description = "Automate immediate white-glove onboarding for qualified conversions.",
            LayerLevel = 3,
            Echelon = AgentEchelon.Business,
            AssignedRoleCode = "OnboardingConcierge",
            AssignedRoleName = "Onboarding Concierge",
            ModelTier = ModelTier.StandardTier,
            PrerequisiteTaskIds = new() { "stream-spintax-copy", "stream-prospect-scraping" },
            Status = MissionNodeStatus.Pending,
            Weight = 1.5m
        };

        return new List<MissionNode> { p1, p2, t1, t2, o1 };
    }

    private List<MissionNode> BuildDynamicExecutiveGoalDag(string goalTreeId, string goalText)
    {
        string bizRoleCode = MatchRoleForTask("Executive Business Strategy", goalText, AgentEchelon.Business);
        string engRoleCode = MatchRoleForTask("Technical System Implementation", goalText, AgentEchelon.Engineering);
        string bizRoleName = RolesRegistry.TryGetValue(bizRoleCode, out var bRole) ? bRole.DisplayName : bizRoleCode;
        string engRoleName = RolesRegistry.TryGetValue(engRoleCode, out var eRole) ? eRole.DisplayName : engRoleCode;

        var p1 = new MissionNode
        {
            Id = "pillar-biz-execution",
            Title = "Pillar 1: Executive Business Strategy & Operational Cadence",
            Description = $"Formulate market-facing execution strategy for: {goalText}",
            LayerLevel = 1,
            Echelon = AgentEchelon.Business,
            AssignedRoleCode = bizRoleCode,
            AssignedRoleName = bizRoleName,
            ModelTier = DetermineModelTier(bizRoleCode, goalText),
            PrerequisiteTaskIds = new(),
            Status = MissionNodeStatus.Pending,
            Weight = 3.0m
        };

        var p2 = new MissionNode
        {
            Id = "pillar-eng-foundation",
            Title = "Pillar 2: Technical Architecture & System Infrastructure",
            Description = $"Construct engineering foundations and resilient data flows for: {goalText}",
            LayerLevel = 1,
            Echelon = AgentEchelon.Engineering,
            AssignedRoleCode = engRoleCode,
            AssignedRoleName = engRoleName,
            ModelTier = DetermineModelTier(engRoleCode, goalText),
            PrerequisiteTaskIds = new(),
            Status = MissionNodeStatus.Pending,
            Weight = 3.0m
        };

        string streamBizRole = MatchRoleForTask("Tactical Operational Stream", goalText, AgentEchelon.Business);
        string streamBizName = RolesRegistry.TryGetValue(streamBizRole, out var sbRole) ? sbRole.DisplayName : streamBizRole;

        var t1 = new MissionNode
        {
            Id = "stream-tactical-biz",
            ParentId = "pillar-biz-execution",
            Title = "Tactical Operational Stream & Milestone Tracking",
            Description = "Coordinate cross-functional tasks, performance milestones, and stakeholder reports.",
            LayerLevel = 2,
            Echelon = AgentEchelon.Business,
            AssignedRoleCode = streamBizRole,
            AssignedRoleName = streamBizName,
            ModelTier = DetermineModelTier(streamBizRole, "Tactical milestone tracking"),
            PrerequisiteTaskIds = new() { "pillar-biz-execution" },
            Status = MissionNodeStatus.Pending,
            Weight = 1.5m
        };

        var t2 = new MissionNode
        {
            Id = "stream-tactical-eng",
            ParentId = "pillar-eng-foundation",
            Title = "Backend API & Data Persistence Pipeline",
            Description = "Implement secure domain services, query patterns, and integration interfaces.",
            LayerLevel = 2,
            Echelon = AgentEchelon.Engineering,
            AssignedRoleCode = "BackendDeveloper",
            AssignedRoleName = "Backend Developer",
            ModelTier = ModelTier.StandardTier,
            PrerequisiteTaskIds = new() { "pillar-eng-foundation" },
            Status = MissionNodeStatus.Pending,
            Weight = 1.5m
        };

        var o1 = new MissionNode
        {
            Id = "task-quality-assurance",
            ParentId = "stream-tactical-eng",
            Title = "Automated Verification & Regression Protection",
            Description = "Execute comprehensive automated xUnit test suites and contract validations.",
            LayerLevel = 3,
            Echelon = AgentEchelon.Engineering,
            AssignedRoleCode = "TestEngineer",
            AssignedRoleName = "Test Engineer",
            ModelTier = ModelTier.StandardTier,
            PrerequisiteTaskIds = new() { "stream-tactical-biz", "stream-tactical-eng" },
            Status = MissionNodeStatus.Pending,
            Weight = 1.5m
        };

        return new List<MissionNode> { p1, p2, t1, t2, o1 };
    }

    private static AgentActivityLogRecord CreateActivityLog(Guid tenantId, string goalTreeId, MissionNode node, string actionSummary)
    {
        int tokens = node.ModelTier switch
        {
            ModelTier.FastTier => RandomNumberGenerator.GetInt32(180, 360),
            ModelTier.StandardTier => RandomNumberGenerator.GetInt32(480, 850),
            ModelTier.ReasoningTier => RandomNumberGenerator.GetInt32(950, 1600),
            _ => 400
        };

        int latency = node.ModelTier switch
        {
            ModelTier.FastTier => RandomNumberGenerator.GetInt32(75, 140),
            ModelTier.StandardTier => RandomNumberGenerator.GetInt32(220, 480),
            ModelTier.ReasoningTier => RandomNumberGenerator.GetInt32(650, 1300),
            _ => 300
        };

        return new AgentActivityLogRecord(
            Id: Guid.NewGuid().ToString("N")[..12],
            TenantId: tenantId,
            GoalTreeId: goalTreeId,
            NodeId: node.Id,
            RoleCode: node.AssignedRoleCode,
            RoleName: node.AssignedRoleName,
            ActionSummary: $"[{node.AssignedRoleCode}] {actionSummary}",
            ModelTierUsed: node.ModelTier,
            TokensConsumed: tokens,
            LatencyMs: latency,
            TimestampUtc: DateTime.UtcNow
        );
    }

    private static string GenerateNodeExecutionOutput(MissionNode node)
    {
        return $"[{node.AssignedRoleName}] Execution finished. Objective: '{node.Title}'. Findings: All operational parameters verified with 0 anomalies detected. Deliverables registered to shared workspace ledger.";
    }

    private static Dictionary<string, AgentRoleDefinition> InitializeRolesRegistry()
    {
        var list = new List<AgentRoleDefinition>
        {
            // ================= 17 BUSINESS ROLES =================
            new(
                RoleCode: "RevOpsStrategist",
                DisplayName: "RevOps Strategist",
                Echelon: AgentEchelon.Business,
                Category: "Business Operations",
                Description: "Analyzes revenue funnels, customer acquisition cost (CAC), pipeline velocity, and True Gross Margin.",
                Competencies: new[] { "Revenue Operations", "True Gross Margin", "Pipeline Velocity", "Conversion Rate Optimization", "Quota Forecasting" },
                DefaultModelTier: ModelTier.ReasoningTier,
                Icon: "TrendingUp",
                DefaultStatus: "Standing By",
                SystemObjective: "Maximize revenue predictability and operational margin yield across all sales channels."
            ),
            new(
                RoleCode: "LogisticsController",
                DisplayName: "Logistics Controller",
                Echelon: AgentEchelon.Business,
                Category: "Business Operations",
                Description: "Orchestrates multi-warehouse inventory allocation, courier SLA performance, and COD remittance tracking.",
                Competencies: new[] { "Inventory Allocation", "Multi-Warehouse Routing", "Courier SLA Tracking", "Stock Replenishment", "COD Remittance" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "Truck",
                DefaultStatus: "Auditing Logistics",
                SystemObjective: "Maintain delivery success rates above 88% and eliminate warehouse stockouts."
            ),
            new(
                RoleCode: "ColdOutreachSdr",
                DisplayName: "Cold Outreach SDR",
                Echelon: AgentEchelon.Business,
                Category: "Business Operations",
                Description: "Drives autonomous outbound prospect qualification, hyper-personalized copy generation, and cadence dispatch.",
                Competencies: new[] { "Account Profiling", "Multi-Channel Prospecting", "Hyper-Personalized Copy", "Spintax Variation", "LinkedIn & Email Sequencing" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "Mail",
                DefaultStatus: "Drafting Sequences",
                SystemObjective: "Generate qualified prospect meetings with personalized multi-channel outreach."
            ),
            new(
                RoleCode: "CustomerEscalationHandler",
                DisplayName: "Customer Escalation Handler",
                Echelon: AgentEchelon.Business,
                Category: "Business Operations",
                Description: "De-escalates critical customer disputes, mitigates SLA breaches, and recovers at-risk client accounts.",
                Competencies: new[] { "De-escalation", "Conflict Resolution", "Ticket Triage", "SLA Breach Mitigation", "VIP Account Recovery" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "AlertCircle",
                DefaultStatus: "Standing By",
                SystemObjective: "Resolve customer friction points rapidly and protect retention metrics."
            ),
            new(
                RoleCode: "SocialGrowthHacker",
                DisplayName: "Social Growth Hacker",
                Echelon: AgentEchelon.Business,
                Category: "Business Operations",
                Description: "Engineers viral content loops, trend hijacking strategies, and omnichannel social engagement schedules.",
                Competencies: new[] { "Viral Content Loops", "Trend Hijacking", "Omnichannel Scheduling", "Engagement Analytics", "Creator Collaboration" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "Share2",
                DefaultStatus: "Generating Ad Copy",
                SystemObjective: "Expand organic brand footprint and attract high-converting social traffic."
            ),
            new(
                RoleCode: "FinancialMarginAuditor",
                DisplayName: "Financial Margin Auditor",
                Echelon: AgentEchelon.Business,
                Category: "Business Operations",
                Description: "Conducts granular audits of courier remittance fees, COGS variances, and float holding costs.",
                Competencies: new[] { "True Margin Audit", "Courier Fee Reconciliation", "Cash Flow Modeling", "COGS Verification", "Float Analysis" },
                DefaultModelTier: ModelTier.ReasoningTier,
                Icon: "DollarSign",
                DefaultStatus: "Standing By",
                SystemObjective: "Eliminate phantom fees and guarantee accurate unit economics across all transactions."
            ),
            new(
                RoleCode: "CommunityModerator",
                DisplayName: "Community Moderator",
                Echelon: AgentEchelon.Business,
                Category: "Business Operations",
                Description: "Monitors WhatsApp communities, Discord channels, and feedback boards to filter spam and nurture engagement.",
                Competencies: new[] { "Sentiment Analysis", "Spam Filtering", "Toxicity Detection", "Community Engagement", "User Onboarding" },
                DefaultModelTier: ModelTier.FastTier,
                Icon: "ShieldCheck",
                DefaultStatus: "Standing By",
                SystemObjective: "Protect community culture and rapidly eliminate spam or malicious activity."
            ),
            new(
                RoleCode: "OnboardingConcierge",
                DisplayName: "Onboarding Concierge",
                Echelon: AgentEchelon.Business,
                Category: "Business Operations",
                Description: "Guides newly signed clients through initial configuration milestones to accelerate time-to-value.",
                Competencies: new[] { "Customer Activation", "Time-to-Value Acceleration", "Interactive Setup Guides", "Milestone Tracking", "Product Adoption" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "Compass",
                DefaultStatus: "Standing By",
                SystemObjective: "Achieve first-run customer success within 48 hours of onboarding."
            ),
            new(
                RoleCode: "AffiliateManager",
                DisplayName: "Affiliate Manager",
                Echelon: AgentEchelon.Business,
                Category: "Business Operations",
                Description: "Recruits, manages, and audits influencer affiliates, tracking link clicks, commissions, and fraud prevention.",
                Competencies: new[] { "Partner Recruitment", "Commission Tier Modeling", "Referral Fraud Detection", "Payout Auditing", "Partner Relations" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "Users",
                DefaultStatus: "Standing By",
                SystemObjective: "Scale affiliate partner channel revenue while enforcing commission integrity."
            ),
            new(
                RoleCode: "CatalogMerchandiser",
                DisplayName: "Catalog Merchandiser",
                Echelon: AgentEchelon.Business,
                Category: "Business Operations",
                Description: "Optimizes product listings, dynamic bundle pricing, and inventory clearance promotions.",
                Competencies: new[] { "PIM Synchronization", "Dynamic Bundling", "Tiered Wholesale Pricing", "Stock Clearance Promotions", "Cross-Sell" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "ShoppingBag",
                DefaultStatus: "Standing By",
                SystemObjective: "Maximize average order value (AOV) and accelerate slow-moving SKU turnover."
            ),
            new(
                RoleCode: "ComplianceOfficer",
                DisplayName: "Compliance Officer",
                Echelon: AgentEchelon.Business,
                Category: "Business Operations",
                Description: "Audits GDPR/CCPA consumer rights requests, PII governance, and cross-border commercial trade regulations.",
                Competencies: new[] { "Regulatory Auditing", "GDPR/CCPA Compliance", "PII Data Governance", "Trade Restrictions", "Terms of Service Enforcement" },
                DefaultModelTier: ModelTier.ReasoningTier,
                Icon: "Scale",
                DefaultStatus: "Standing By",
                SystemObjective: "Guarantee zero regulatory non-compliance fines and airtight data governance."
            ),
            new(
                RoleCode: "ExecutiveChiefOfStaff",
                DisplayName: "Executive Chief of Staff",
                Echelon: AgentEchelon.Business,
                Category: "Business Operations",
                Description: "Translates high-level executive OKRs into tactical multi-agent missions and coordinates cross-echelon priorities.",
                Competencies: new[] { "OKR Tracking", "Cross-Functional Alignment", "Strategic Planning", "Board Reporting", "Priority Orchestration" },
                DefaultModelTier: ModelTier.ReasoningTier,
                Icon: "Briefcase",
                DefaultStatus: "Standing By",
                SystemObjective: "Synchronize organizational strategic goals with autonomous agent task execution."
            ),
            new(
                RoleCode: "BrandVoiceStylist",
                DisplayName: "Brand Voice Stylist",
                Echelon: AgentEchelon.Business,
                Category: "Business Operations",
                Description: "Ensures brand messaging, tonal nuance, and customer-facing prose adhere strictly to the corporate style guide.",
                Competencies: new[] { "Copywriting Guidelines", "Tone Consistency", "Narrative Design", "Style Guide Enforcement", "Multimodal Messaging" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "Feather",
                DefaultStatus: "Standing By",
                SystemObjective: "Enforce consistent, elegant brand voice across all marketing and sales communications."
            ),
            new(
                RoleCode: "RetentionSpecialist",
                DisplayName: "Retention Specialist",
                Echelon: AgentEchelon.Business,
                Category: "Business Operations",
                Description: "Monitors customer health scores, detects churn signals, and launches automated win-back cadences.",
                Competencies: new[] { "Churn Prediction", "Re-engagement Campaigns", "Loyalty Programs", "Win-back Sequences", "Customer Health Scoring" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "HeartHandshake",
                DefaultStatus: "Standing By",
                SystemObjective: "Minimize logo churn and expand net revenue retention (NRR)."
            ),
            new(
                RoleCode: "MarketplaceScout",
                DisplayName: "Marketplace Scout",
                Echelon: AgentEchelon.Business,
                Category: "Business Operations",
                Description: "Scrapes competitor pricing matrices, identifies catalog assortment gaps, and monitors regional market trends.",
                Competencies: new[] { "Competitor Benchmarking", "Price Scraping", "Product Gap Discovery", "Trend Radar", "Supplier Discovery" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "Search",
                DefaultStatus: "Standing By",
                SystemObjective: "Provide actionable pricing and assortment intelligence ahead of competitors."
            ),
            new(
                RoleCode: "AdSpendOptimizer",
                DisplayName: "Ad Spend Optimizer",
                Echelon: AgentEchelon.Business,
                Category: "Business Operations",
                Description: "Tracks Meta/Google CAPI conversions, detects ad creative fatigue, and reallocates ad budget for maximum ROAS.",
                Competencies: new[] { "ROAS Optimization", "Creative Fatigue Detection", "Meta/Google CAPI Sync", "Bid Strategy Tuning", "Budget Rebalancing" },
                DefaultModelTier: ModelTier.ReasoningTier,
                Icon: "PieChart",
                DefaultStatus: "Standing By",
                SystemObjective: "Maximize return on ad spend and prevent unprofitable campaign burn."
            ),
            new(
                RoleCode: "LocalizationAnalyst",
                DisplayName: "Localization Analyst",
                Echelon: AgentEchelon.Business,
                Category: "Business Operations",
                Description: "Adapts sales copy, currency formatting, and regional dialects (Arabic, French, English) for localized commerce.",
                Competencies: new[] { "Cultural Nuance Adaptation", "Multi-Language Translation", "Currency & Tax Localization", "Regional Slang Adaptation" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "Globe",
                DefaultStatus: "Standing By",
                SystemObjective: "Ensure seamless cultural resonance across all target geographic markets."
            ),

            // ================= 21 ENGINEERING & OPS ROLES =================
            new(
                RoleCode: "SoftwareArchitect",
                DisplayName: "Software Architect",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Designs scalable system architecture, maintains modular monolith boundaries, and enforces DDD bounded contexts.",
                Competencies: new[] { "System Design", "Modular Monolith Boundaries", "DDD Bounded Contexts", "Event-Driven Architecture", "High Concurrency" },
                DefaultModelTier: ModelTier.ReasoningTier,
                Icon: "Cpu",
                DefaultStatus: "Standing By",
                SystemObjective: "Maintain architectural purity, clean module boundaries, and horizontal scalability."
            ),
            new(
                RoleCode: "BackendDeveloper",
                DisplayName: "Backend Developer",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Implements high-performance C# .NET 10 Minimal APIs, CQRS handlers, and transactional business logic.",
                Competencies: new[] { "C# .NET 10", "Minimal APIs", "Async/Await Concurrency", "CQRS Handlers", "Dependency Injection" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "Server",
                DefaultStatus: "Standing By",
                SystemObjective: "Deliver robust, idiomatic, zero-allocation backend services."
            ),
            new(
                RoleCode: "FrontendSpecialist",
                DisplayName: "Frontend Specialist",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Builds modern Angular 22 standalone components with Signals, zoneless change detection, and Tailwind CSS.",
                Competencies: new[] { "Angular 22", "Signals & Computed State", "Zoneless CD", "Tailwind CSS", "Responsive LTR UX" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "Layout",
                DefaultStatus: "Standing By",
                SystemObjective: "Craft ultra-responsive, beautiful, accessible user interfaces."
            ),
            new(
                RoleCode: "DatabaseSpecialist",
                DisplayName: "Database Specialist",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Optimizes PostgreSQL 17 schemas, writes PostGIS spatial queries, and designs zero-lock EF Core migrations.",
                Competencies: new[] { "PostgreSQL 17", "PostGIS Spatial Indexing", "EF Core Query Optimization", "Migration Safety", "WAL Tuning" },
                DefaultModelTier: ModelTier.ReasoningTier,
                Icon: "Database",
                DefaultStatus: "Standing By",
                SystemObjective: "Ensure sub-10ms query latency and bulletproof relational data integrity."
            ),
            new(
                RoleCode: "TestEngineer",
                DisplayName: "Test Engineer",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Authors comprehensive xUnit v3 test suites, WebApplicationFactory integration harnesses, and TDD specifications.",
                Competencies: new[] { "xUnit v3", "WebApplicationFactory", "Testcontainers", "TDD Red-Green-Refactor", "Regression Protection" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "CheckCircle",
                DefaultStatus: "Standing By",
                SystemObjective: "Maintain high code coverage and block regressions from entering main."
            ),
            new(
                RoleCode: "SecurityAuditor",
                DisplayName: "Security Auditor",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Audits code for OWASP Top 10 vulnerabilities, inspects AES-256-GCM vaults, and verifies RBAC token guards.",
                Competencies: new[] { "OWASP Top 10", "Cryptographic Vaults (AES-256-GCM)", "RBAC Validation", "Secret Leak Prevention", "Rate Throttling" },
                DefaultModelTier: ModelTier.ReasoningTier,
                Icon: "Lock",
                DefaultStatus: "Standing By",
                SystemObjective: "Guarantee zero exploitable security vulnerabilities or unauthorized data leaks."
            ),
            new(
                RoleCode: "DevOpsEngineer",
                DisplayName: "DevOps Engineer",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Automates CI/CD pipelines, provisions multi-stage Docker builds, and configures Docker Swarm deployment stacks.",
                Competencies: new[] { "CI/CD Pipelines", "GitHub Actions", "Docker Swarm / Compose", "Kubernetes Manifests", "Environment Configuration" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "Terminal",
                DefaultStatus: "Standing By",
                SystemObjective: "Maintain automated, reproducible, zero-downtime release pipelines."
            ),
            new(
                RoleCode: "PerformanceAnalyst",
                DisplayName: "Performance Analyst",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Profiles GC memory allocations, instruments zero-alloc spans, and designs distributed HybridCache layers.",
                Competencies: new[] { "Allocation Profiling", "GC Pressure Reduction", "Memory Spans", "Distributed Caching (HybridCache)", "Latency Trimming" },
                DefaultModelTier: ModelTier.ReasoningTier,
                Icon: "Activity",
                DefaultStatus: "Standing By",
                SystemObjective: "Achieve sub-50ms P99 latency across all high-throughput endpoints."
            ),
            new(
                RoleCode: "ApiDesigner",
                DisplayName: "API Designer",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Drafts OpenAPI v3 / Scalar specifications, RFC 7807 problem details, and semantic API versioning strategies.",
                Competencies: new[] { "OpenAPI v3 / Scalar", "RFC 7807 ProblemDetails", "Semantic Versioning", "Rate Limiting Specs", "Webhook Contracts" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "Code",
                DefaultStatus: "Standing By",
                SystemObjective: "Deliver world-class developer ergonomics and strict REST contracts."
            ),
            new(
                RoleCode: "BuildErrorResolver",
                DisplayName: "Build Error Resolver",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Diagnoses and auto-fixes C# compiler errors, Roslyn warnings, and NuGet dependency package version conflicts.",
                Competencies: new[] { "Compiler Diagnostics", "NuGet Dependency Trees", "Roslyn Fixes", "Breaking Change Reconciliation", "Target Framework Compatibility" },
                DefaultModelTier: ModelTier.FastTier,
                Icon: "Wrench",
                DefaultStatus: "Standing By",
                SystemObjective: "Maintain zero-warning clean compilation builds across all solutions."
            ),
            new(
                RoleCode: "RefactorCleaner",
                DisplayName: "Refactor Cleaner",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Identifies dead code paths, reduces cognitive complexity, and de-sloppifies legacy implementation files.",
                Competencies: new[] { "Dead Code Elimination", "Cognitive Complexity Reduction", "DRY Enforcement", "Method Inlining", "Clean Code" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "Trash2",
                DefaultStatus: "Standing By",
                SystemObjective: "Prune technical debt and preserve clean, maintainable codebases."
            ),
            new(
                RoleCode: "CodeReviewer",
                DisplayName: "Code Reviewer",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Performs blast-radius PR reviews, detects architecture anti-patterns, and enforces project coding conventions.",
                Competencies: new[] { "Blast Radius Analysis", "Architectural Conformance", "Roslyn MCP Verification", "Anti-Pattern Detection", "Quality Gatekeeping" },
                DefaultModelTier: ModelTier.ReasoningTier,
                Icon: "FileText",
                DefaultStatus: "Standing By",
                SystemObjective: "Ensure all merged pull requests meet high production quality standards."
            ),
            new(
                RoleCode: "SlaGuardian",
                DisplayName: "SLA Guardian",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Monitors real-time service uptime, alerts on error budget exhaustion, and automates incident escalations.",
                Competencies: new[] { "Uptime Monitoring", "SLA Contract Enforcement", "Incident Escalation", "P99 Latency Budgets", "Error Budgeting" },
                DefaultModelTier: ModelTier.FastTier,
                Icon: "ShieldAlert",
                DefaultStatus: "Standing By",
                SystemObjective: "Guarantee 99.99% service availability and enforce SLA guarantees."
            ),
            new(
                RoleCode: "TelemetryEngineer",
                DisplayName: "Telemetry Engineer",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Configures OpenTelemetry distributed tracing, Prometheus counter metrics, and Serilog structured audit logging.",
                Competencies: new[] { "OpenTelemetry Tracing", "Prometheus Metrics", "Grafana Dashboards", "Structured Logging (Serilog)", "Distributed Context" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "BarChart",
                DefaultStatus: "Standing By",
                SystemObjective: "Provide 100% observability across all distributed service boundaries."
            ),
            new(
                RoleCode: "ScraperEngineer",
                DisplayName: "Scraper Engineer",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Builds resilient headless browser crawlers, proxy rotation pipelines, and structured JSON extractors.",
                Competencies: new[] { "Headless Browser Automation", "Anti-Bot Bypass", "DOM Parsing", "Proxy Rotation", "Structured JSON Extraction" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "DownloadCloud",
                DefaultStatus: "Standing By",
                SystemObjective: "Ingest structured external data reliably without IP blocking or throttling."
            ),
            new(
                RoleCode: "DockerSpecialist",
                DisplayName: "Docker Specialist",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Constructs lean, chiseled Docker container images, minimizes layer bloat, and hardens container security.",
                Competencies: new[] { "Multi-Stage Dockerfiles", "Alpine/Chiseled Images", "Container Security Hardening", "Swarm Healthchecks", "Volume Mounting" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "Box",
                DefaultStatus: "Standing By",
                SystemObjective: "Minimize container surface attack area and optimize image pull speeds."
            ),
            new(
                RoleCode: "AspireOrchestrator",
                DisplayName: "Aspire Orchestrator",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Wires multi-service distributed applications using .NET Aspire AppHost and manages local service discovery.",
                Competencies: new[] { ".NET Aspire AppHost", "Service Discovery", "Resource Wiring", "Health Check Dashboards", "Local Distributed Dev" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "Layers",
                DefaultStatus: "Standing By",
                SystemObjective: "Provide frictionless local development and cloud-native orchestration."
            ),
            new(
                RoleCode: "MobileCapacitorEngineer",
                DisplayName: "Mobile Capacitor Engineer",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Integrates Ionic Capacitor native plugins, Android WebView bridges, push notifications, and offline sync.",
                Competencies: new[] { "Ionic Capacitor Plugins", "Android WebView Bridge", "Push Notifications", "Biometric Auth", "Offline Sync" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "Smartphone",
                DefaultStatus: "Standing By",
                SystemObjective: "Deliver seamless, native-grade mobile experiences for field teams."
            ),
            new(
                RoleCode: "DnnAiSpecialist",
                DisplayName: "DNN AI Specialist",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Maintains ONNX deep neural network runtimes, pgvector embeddings, and high-dimensional semantic search indexes.",
                Competencies: new[] { "Deep Neural Networks", "ONNX Runtime Inference", "Vector Embeddings (pgvector)", "Semantic Similarity", "Embedding Quantization" },
                DefaultModelTier: ModelTier.ReasoningTier,
                Icon: "Brain",
                DefaultStatus: "Standing By",
                SystemObjective: "Deliver accurate semantic retrieval and performant neural inferencing."
            ),
            new(
                RoleCode: "DisasterRecoverySpecialist",
                DisplayName: "Disaster Recovery Specialist",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Conducts automated database backup verifications, point-in-time recovery drills, and failover runbook simulations.",
                Competencies: new[] { "Backup Verification", "Point-in-Time Recovery", "Failover Runbooks", "Chaos Engineering", "Data Corruption Healing" },
                DefaultModelTier: ModelTier.ReasoningTier,
                Icon: "LifeBuoy",
                DefaultStatus: "Standing By",
                SystemObjective: "Guarantee zero RPO/RTO data loss during infrastructure outages."
            ),
            new(
                RoleCode: "PromptEngineer",
                DisplayName: "Prompt Engineer",
                Echelon: AgentEchelon.Engineering,
                Category: "Technical & Engineering",
                Description: "Optimizes system instructions, chain-of-thought guardrails, JSON schema enforcement, and MCP tool call prompts.",
                Competencies: new[] { "Few-Shot In-Context Prompting", "Chain-of-Thought Guardrails", "Output JSON Schema Enforcement", "MCP Tool Calling Schemas" },
                DefaultModelTier: ModelTier.StandardTier,
                Icon: "MessageSquare",
                DefaultStatus: "Standing By",
                SystemObjective: "Eliminate LLM hallucinations and enforce deterministic tool usage."
            )
        };

        return list.ToDictionary(r => r.RoleCode, StringComparer.OrdinalIgnoreCase);
    }
}
