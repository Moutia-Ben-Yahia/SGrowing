using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Automation.WorkflowTelemetry;

public interface IWorkflowTelemetryEngine
{
    DagValidationResult ValidateDag(WorkflowDefinition workflow);
    WorkflowSimulationResult SimulateWorkflow(WorkflowSimulationRequest request);
    WorkflowTelemetryRecord? GetTelemetry(string tenantId, string executionId);
}

public class WorkflowTelemetryEngine : IWorkflowTelemetryEngine
{
    // Key: tenantId:executionId -> WorkflowTelemetryRecord
    private readonly ConcurrentDictionary<string, WorkflowTelemetryRecord> _telemetryStore = new();

    public DagValidationResult ValidateDag(WorkflowDefinition workflow)
    {
        var errors = new List<string>();
        var nodeMap = workflow.Nodes.ToDictionary(n => n.NodeId);

        if (!nodeMap.ContainsKey(workflow.TriggerNodeId))
        {
            errors.Add($"Trigger node '{workflow.TriggerNodeId}' does not exist in workflow nodes.");
        }

        // Build adjacency list
        var adj = new Dictionary<string, List<string>>();
        foreach (var node in workflow.Nodes)
        {
            adj[node.NodeId] = new List<string>();
        }

        foreach (var edge in workflow.Edges)
        {
            if (!nodeMap.ContainsKey(edge.SourceNodeId))
            {
                errors.Add($"Edge references non-existent source node '{edge.SourceNodeId}'.");
                continue;
            }
            if (!nodeMap.ContainsKey(edge.TargetNodeId))
            {
                errors.Add($"Edge references non-existent target node '{edge.TargetNodeId}'.");
                continue;
            }

            adj[edge.SourceNodeId].Add(edge.TargetNodeId);
        }

        // Cycle Detection via DFS (0 = unvisited, 1 = visiting, 2 = visited)
        var state = new Dictionary<string, int>();
        foreach (var nodeId in nodeMap.Keys)
        {
            state[nodeId] = 0;
        }

        var cyclePath = new List<string>();
        bool hasCycles = false;

        bool DfsCycle(string current, List<string> path)
        {
            state[current] = 1;
            path.Add(current);

            if (adj.TryGetValue(current, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    if (state[neighbor] == 1)
                    {
                        var cycleStartIndex = path.IndexOf(neighbor);
                        if (cycleStartIndex >= 0)
                        {
                            cyclePath = path.Skip(cycleStartIndex).ToList();
                            cyclePath.Add(neighbor);
                        }
                        return true;
                    }

                    if (state[neighbor] == 0)
                    {
                        if (DfsCycle(neighbor, path))
                            return true;
                    }
                }
            }

            state[current] = 2;
            path.RemoveAt(path.Count - 1);
            return false;
        }

        foreach (var nodeId in nodeMap.Keys)
        {
            if (state[nodeId] == 0)
            {
                if (DfsCycle(nodeId, new List<string>()))
                {
                    hasCycles = true;
                    errors.Add($"Workflow contains a circular dependency cycle: {string.Join(" -> ", cyclePath)}");
                    break;
                }
            }
        }

        // Reachability analysis from trigger
        var reachable = new HashSet<string>();
        if (nodeMap.ContainsKey(workflow.TriggerNodeId))
        {
            var queue = new Queue<string>();
            queue.Enqueue(workflow.TriggerNodeId);
            reachable.Add(workflow.TriggerNodeId);

            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                if (adj.TryGetValue(curr, out var nextNodes))
                {
                    foreach (var n in nextNodes)
                    {
                        if (reachable.Add(n))
                        {
                            queue.Enqueue(n);
                        }
                    }
                }
            }
        }

        var unreachable = nodeMap.Keys.Where(k => !reachable.Contains(k)).ToList();

        return new DagValidationResult(
            IsValid: errors.Count == 0 && !hasCycles,
            HasCycles: hasCycles,
            CyclePath: cyclePath,
            UnreachableNodeIds: unreachable,
            ValidationErrors: errors
        );
    }

    public WorkflowSimulationResult SimulateWorkflow(WorkflowSimulationRequest request)
    {
        var validation = ValidateDag(request.Workflow);
        var executionId = $"exec_{Guid.NewGuid():N}";
        var now = DateTime.UtcNow;

        if (!validation.IsValid)
        {
            return new WorkflowSimulationResult(
                CompletedSuccessfully: false,
                ExecutionId: executionId,
                WorkflowId: request.Workflow.WorkflowId,
                ContactId: request.Contact.ContactId,
                TotalStepsExecuted: 0,
                TotalSimulatedDuration: TimeSpan.Zero,
                ExecutionPath: new List<NodeExecutionSnapshot>(),
                ActionsDispatched: new List<string>(),
                FinalStatus: $"ValidationFailed: {string.Join("; ", validation.ValidationErrors)}",
                SimulatedAtUtc: now
            );
        }

        var nodeMap = request.Workflow.Nodes.ToDictionary(n => n.NodeId);
        var edgeList = request.Workflow.Edges;

        var executionPath = new List<NodeExecutionSnapshot>();
        var actionsDispatched = new List<string>();
        var simulatedDuration = TimeSpan.Zero;

        string? currentNodeId = request.Workflow.TriggerNodeId;
        int stepNumber = 1;

        while (!string.IsNullOrEmpty(currentNodeId) && stepNumber <= request.MaxSteps)
        {
            if (!nodeMap.TryGetValue(currentNodeId, out var node))
            {
                break;
            }

            var stepLatency = TimeSpan.FromMilliseconds(15);
            simulatedDuration += stepLatency;

            string status = "Completed";
            string output = string.Empty;
            string? branchDecision = null;

            switch (node.NodeType)
            {
                case WorkflowNodeType.Trigger:
                    status = "Triggered";
                    output = $"Trigger fired for contact '{request.Contact.Email}' (Score: {request.Contact.LeadScore})";
                    break;

                case WorkflowNodeType.Filter:
                    bool filterPassed = EvaluateFilter(node.Configuration, request.Contact);
                    if (filterPassed)
                    {
                        status = "FilterPassed";
                        output = "Contact met filter criteria.";
                    }
                    else
                    {
                        status = "FilterBlocked";
                        output = "Contact did not meet filter criteria. Halting flow.";
                    }
                    break;

                case WorkflowNodeType.BranchCondition:
                    bool condResult = EvaluateBranchCondition(node.Configuration, request.Contact);
                    branchDecision = condResult ? "true" : "false";
                    status = condResult ? "ConditionMet" : "ConditionNotMet";
                    output = $"Branch condition evaluated to '{branchDecision}'.";
                    break;

                case WorkflowNodeType.ActionEmail:
                    var template = node.Configuration?.GetValueOrDefault("TemplateId", "welcome_email");
                    var emailAction = $"SendEmail({template}) -> {request.Contact.Email}";
                    actionsDispatched.Add(emailAction);
                    status = "ActionDispatched";
                    output = $"Dispatched email using template '{template}'.";
                    break;

                case WorkflowNodeType.ActionSms:
                    var smsText = node.Configuration?.GetValueOrDefault("Message", "Notification");
                    var smsAction = $"SendSms -> {request.Contact.ContactId}: {smsText}";
                    actionsDispatched.Add(smsAction);
                    status = "ActionDispatched";
                    output = $"Dispatched SMS notification.";
                    break;

                case WorkflowNodeType.ActionWebhook:
                    var targetUrl = node.Configuration?.GetValueOrDefault("Url", "https://api.crm.internal/events");
                    var webhookAction = $"WebhookPost -> {targetUrl}";
                    actionsDispatched.Add(webhookAction);
                    status = "ActionDispatched";
                    output = $"Sent webhook payload to '{targetUrl}'.";
                    break;

                case WorkflowNodeType.Delay:
                    int delayMinutes = 60;
                    if (node.Configuration != null && node.Configuration.TryGetValue("DelayMinutes", out var dm) && int.TryParse(dm, out var dVal))
                    {
                        delayMinutes = dVal;
                    }
                    simulatedDuration += TimeSpan.FromMinutes(delayMinutes);
                    status = "Delayed";
                    output = $"Simulated time delay of {delayMinutes} minutes.";
                    break;

                case WorkflowNodeType.AiDecisionNode:
                    branchDecision = EvaluateAiDecision(request.Contact);
                    status = "AiDecisionEmitted";
                    output = $"AI Agent evaluated contact signals -> Chosen Branch: '{branchDecision}'.";
                    break;

                case WorkflowNodeType.GoalAchieved:
                    status = "GoalAchieved";
                    output = "Target milestone reached.";
                    break;

                case WorkflowNodeType.Terminate:
                    status = "Terminated";
                    output = "Workflow terminated normally.";
                    break;
            }

            executionPath.Add(new NodeExecutionSnapshot(
                StepNumber: stepNumber,
                NodeId: node.NodeId,
                NodeName: node.Name,
                NodeType: node.NodeType,
                Status: status,
                OutputSummary: output,
                Latency: stepLatency,
                ExecutedAtUtc: now.Add(simulatedDuration)
            ));

            if (status == "FilterBlocked" || node.NodeType == WorkflowNodeType.Terminate || node.NodeType == WorkflowNodeType.GoalAchieved)
            {
                break;
            }

            // Find next node
            currentNodeId = ResolveNextNode(node.NodeId, edgeList, branchDecision);
            stepNumber++;
        }

        var finalStatus = executionPath.LastOrDefault()?.Status == "GoalAchieved"
            ? "GoalAchieved"
            : (executionPath.LastOrDefault()?.Status == "FilterBlocked" ? "BlockedByFilter" : "Completed");

        var simulationResult = new WorkflowSimulationResult(
            CompletedSuccessfully: true,
            ExecutionId: executionId,
            WorkflowId: request.Workflow.WorkflowId,
            ContactId: request.Contact.ContactId,
            TotalStepsExecuted: executionPath.Count,
            TotalSimulatedDuration: simulatedDuration,
            ExecutionPath: executionPath,
            ActionsDispatched: actionsDispatched,
            FinalStatus: finalStatus,
            SimulatedAtUtc: now
        );

        // Store telemetry record
        var telemetryKey = $"{request.TenantId}:{executionId}";
        _telemetryStore[telemetryKey] = new WorkflowTelemetryRecord(
            ExecutionId: executionId,
            TenantId: request.TenantId,
            WorkflowId: request.Workflow.WorkflowId,
            ContactId: request.Contact.ContactId,
            StartedAtUtc: now,
            FinishedAtUtc: now.Add(simulatedDuration),
            Status: finalStatus,
            Steps: executionPath
        );

        return simulationResult;
    }

    public WorkflowTelemetryRecord? GetTelemetry(string tenantId, string executionId)
    {
        var key = $"{tenantId}:{executionId}";
        return _telemetryStore.TryGetValue(key, out var record) ? record : null;
    }

    private static bool EvaluateFilter(Dictionary<string, string>? config, SimulationContactContext contact)
    {
        if (config == null) return true;

        if (config.TryGetValue("MinLeadScore", out var minScoreStr) && int.TryParse(minScoreStr, out var minScore))
        {
            if (contact.LeadScore < minScore) return false;
        }

        if (config.TryGetValue("AllowedCountry", out var allowedCountry) && !string.IsNullOrWhiteSpace(allowedCountry))
        {
            if (!string.Equals(contact.Country, allowedCountry, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }

    private static bool EvaluateBranchCondition(Dictionary<string, string>? config, SimulationContactContext contact)
    {
        if (config == null) return true;

        if (config.TryGetValue("Field", out var field))
        {
            if (string.Equals(field, "LeadScore", StringComparison.OrdinalIgnoreCase))
            {
                if (config.TryGetValue("Threshold", out var threshStr) && int.TryParse(threshStr, out var thresh))
                {
                    return contact.LeadScore >= thresh;
                }
            }
            else if (string.Equals(field, "HasPurchased", StringComparison.OrdinalIgnoreCase))
            {
                return contact.HasPurchased;
            }
        }

        return true;
    }

    private static string EvaluateAiDecision(SimulationContactContext contact)
    {
        // AI heuristics simulation based on composite growth signals
        if (contact.LeadScore >= 75 || contact.LifetimeValue > 1000m)
        {
            return "high_intent";
        }
        return "nurture";
    }

    private static string? ResolveNextNode(string currentNodeId, List<WorkflowEdge> edges, string? branchDecision)
    {
        var outgoing = edges.Where(e => e.SourceNodeId == currentNodeId).ToList();
        if (outgoing.Count == 0) return null;

        if (!string.IsNullOrEmpty(branchDecision))
        {
            var match = outgoing.FirstOrDefault(e => string.Equals(e.BranchConditionValue, branchDecision, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match.TargetNodeId;
        }

        // Default or unconditioned edge
        var fallback = outgoing.FirstOrDefault(e => string.IsNullOrEmpty(e.BranchConditionValue)) ?? outgoing.First();
        return fallback.TargetNodeId;
    }
}
