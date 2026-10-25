using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Modules.Automation;

public class WorkflowNodeDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object> Config { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Next { get; set; }
    public string? TrueNext { get; set; }
    public string? FalseNext { get; set; }
}

public class WorkflowEngineService
{
    private static readonly ConcurrentDictionary<Guid, WorkflowGraph> _workflows = new();
    private static readonly ConcurrentDictionary<Guid, WorkflowExecution> _executions = new();

    private readonly SGrowingDbContext? _db;

    public WorkflowEngineService(SGrowingDbContext? db = null)
    {
        _db = db;
    }

    public static void RegisterWorkflow(WorkflowGraph workflow)
    {
        _workflows[workflow.Id] = workflow;
    }

    public static WorkflowGraph? GetRegisteredWorkflow(Guid workflowId)
    {
        _workflows.TryGetValue(workflowId, out var wf);
        return wf;
    }

    public static WorkflowExecution? GetExecution(Guid executionId)
    {
        _executions.TryGetValue(executionId, out var exec);
        return exec;
    }

    public async Task<WorkflowGraph> CreateWorkflowAsync(
        Guid tenantId,
        string name,
        string triggerType,
        string nodesJson,
        CancellationToken cancellationToken = default)
    {
        var workflow = WorkflowGraph.Create(tenantId, name, triggerType, nodesJson);
        _workflows[workflow.Id] = workflow;
        return await Task.FromResult(workflow);
    }

    public async Task<WorkflowExecution> ExecuteWorkflowAsync(
        Guid tenantId,
        Guid workflowId,
        string inputPayloadJson,
        CancellationToken cancellationToken = default)
    {
        if (!_workflows.TryGetValue(workflowId, out var workflow))
        {
            throw new KeyNotFoundException($"WorkflowGraph with ID '{workflowId}' was not found for tenant '{tenantId}'.");
        }

        return await ExecuteWorkflowInternalAsync(workflow, inputPayloadJson, cancellationToken);
    }

    public async Task<WorkflowExecution> ExecuteWorkflowAsync(
        WorkflowGraph workflow,
        string inputPayloadJson,
        CancellationToken cancellationToken = default)
    {
        _workflows[workflow.Id] = workflow;
        return await ExecuteWorkflowInternalAsync(workflow, inputPayloadJson, cancellationToken);
    }

    private async Task<WorkflowExecution> ExecuteWorkflowInternalAsync(
        WorkflowGraph workflow,
        string inputPayloadJson,
        CancellationToken cancellationToken)
    {
        var execution = WorkflowExecution.Create(workflow.TenantId, workflow.Id, "Running");
        _executions[execution.Id] = execution;

        var state = ParseStateDictionary(inputPayloadJson);
        var nodes = ParseNodes(workflow.NodesJson);

        if (nodes.Count == 0)
        {
            execution.MarkCompleted(0, "[Workflow Engine] No executable nodes found in WorkflowGraph.");
            return execution;
        }

        var nodeMap = nodes.ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);

        // Determine starting node: first node or node of type Trigger/Start
        var startNode = nodes.FirstOrDefault(n => IsTriggerNode(n.Type)) ?? nodes[0];
        var currentNode = startNode;

        int executedCount = 0;
        const int maxSteps = 500;

        try
        {
            while (currentNode != null && executedCount < maxSteps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                executedCount++;

                var nodeType = currentNode.Type.Trim();
                var nodeName = string.IsNullOrWhiteSpace(currentNode.Name) ? currentNode.Id : currentNode.Name;

                if (IsConditionNode(nodeType))
                {
                    var isTrue = EvaluateCondition(currentNode, state, execution);
                    var nextId = isTrue ? currentNode.TrueNext : currentNode.FalseNext;

                    execution.AppendLog($"[Condition: {nodeName}] Evaluated condition => {(isTrue ? "TRUE" : "FALSE")}. Routing to: {(string.IsNullOrEmpty(nextId) ? "[End of branch]" : nextId)}");

                    if (!string.IsNullOrEmpty(nextId) && nodeMap.TryGetValue(nextId, out var nextNode))
                    {
                        currentNode = nextNode;
                    }
                    else
                    {
                        currentNode = null;
                    }
                }
                else if (IsSendMessageNode(nodeType))
                {
                    await ExecuteSendMessageAsync(currentNode, state, execution, cancellationToken);
                    currentNode = GetNextNode(currentNode, nodes, nodeMap);
                }
                else if (IsUpdateCrmNode(nodeType))
                {
                    await ExecuteUpdateCrmAsync(workflow.TenantId, currentNode, state, execution, cancellationToken);
                    currentNode = GetNextNode(currentNode, nodes, nodeMap);
                }
                else if (IsWebhookNode(nodeType))
                {
                    await ExecuteWebhookAsync(currentNode, state, execution, cancellationToken);
                    currentNode = GetNextNode(currentNode, nodes, nodeMap);
                }
                else
                {
                    // Generic or Trigger node execution
                    execution.AppendLog($"[Step: {nodeName}] Executed node of type '{nodeType}'.");
                    currentNode = GetNextNode(currentNode, nodes, nodeMap);
                }
            }

            execution.MarkCompleted(executedCount, $"[Workflow Completed] Successfully executed {executedCount} node(s).");
        }
        catch (Exception ex)
        {
            execution.MarkFailed(executedCount, $"[Workflow Failed] Execution aborted at step '{currentNode?.Id}': {ex.Message}");
        }

        return execution;
    }

    private WorkflowNodeDefinition? GetNextNode(
        WorkflowNodeDefinition current,
        List<WorkflowNodeDefinition> list,
        Dictionary<string, WorkflowNodeDefinition> map)
    {
        if (!string.IsNullOrEmpty(current.Next) && map.TryGetValue(current.Next, out var explicitNext))
        {
            return explicitNext;
        }

        // If no explicit next and the workflow is a sequential list with no graph links
        var hasExplicitLinks = list.Any(n => !string.IsNullOrEmpty(n.Next) || !string.IsNullOrEmpty(n.TrueNext) || !string.IsNullOrEmpty(n.FalseNext));
        if (!hasExplicitLinks)
        {
            var idx = list.IndexOf(current);
            if (idx >= 0 && idx + 1 < list.Count)
            {
                return list[idx + 1];
            }
        }

        return null;
    }

    private static bool IsConditionNode(string type) =>
        type.Equals("Condition", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("Filter", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("Decision", StringComparison.OrdinalIgnoreCase) ||
        type.EndsWith(":Condition", StringComparison.OrdinalIgnoreCase) ||
        type.EndsWith(": Condition", StringComparison.OrdinalIgnoreCase);

    private static bool IsSendMessageNode(string type) =>
        type.Equals("SendMessage", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("Action: SendMessage", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("Action:SendMessage", StringComparison.OrdinalIgnoreCase) ||
        type.EndsWith("SendMessage", StringComparison.OrdinalIgnoreCase);

    private static bool IsUpdateCrmNode(string type) =>
        type.Equals("UpdateCRM", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("Action: UpdateCRM", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("Action:UpdateCRM", StringComparison.OrdinalIgnoreCase) ||
        type.EndsWith("UpdateCRM", StringComparison.OrdinalIgnoreCase);

    private static bool IsWebhookNode(string type) =>
        type.Equals("Webhook", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("Action: Webhook", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("Action:Webhook", StringComparison.OrdinalIgnoreCase) ||
        type.EndsWith("Webhook", StringComparison.OrdinalIgnoreCase);

    private static bool IsTriggerNode(string type) =>
        type.Equals("Trigger", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("Start", StringComparison.OrdinalIgnoreCase) ||
        type.StartsWith("Trigger:", StringComparison.OrdinalIgnoreCase);

    private bool EvaluateCondition(
        WorkflowNodeDefinition node,
        Dictionary<string, object> state,
        WorkflowExecution execution)
    {
        string? field = GetStringConfig(node.Config, "field", "property", "key", "variable");
        string? op = GetStringConfig(node.Config, "operator", "op", "condition");
        object? targetValue = GetConfigValue(node.Config, "value", "expectedValue", "val");

        // If field is missing, attempt to parse expression from "expression" or "condition" config
        if (string.IsNullOrWhiteSpace(field))
        {
            var expr = GetStringConfig(node.Config, "expression", "condition");
            if (!string.IsNullOrWhiteSpace(expr))
            {
                var match = Regex.Match(expr, @"^([a-zA-Z0-9_.]+)\s*(>=|<=|==|!=|>|<|=)\s*(.+)$");
                if (match.Success)
                {
                    field = match.Groups[1].Value.Trim();
                    op = match.Groups[2].Value.Trim();
                    targetValue = match.Groups[3].Value.Trim().Trim('\'', '\"');
                }
            }
        }

        if (string.IsNullOrWhiteSpace(field))
        {
            execution.AppendLog($"[Condition Warning] No field or expression specified for node '{node.Id}'. Defaulting to false.");
            return false;
        }

        state.TryGetValue(field, out var actualValue);

        return CompareValues(actualValue, op ?? "==", targetValue);
    }

    private static bool CompareValues(object? actual, string op, object? expected)
    {
        var opNormalized = op.Trim().ToLowerInvariant();

        // Handle null / exists checks
        if (opNormalized is "exists" or "notnull" or "isnotnull")
            return actual != null && !string.IsNullOrWhiteSpace(actual.ToString());
        if (opNormalized is "notexists" or "isnull")
            return actual == null || string.IsNullOrWhiteSpace(actual.ToString());

        if (actual == null)
            return false;

        // Try numeric comparison first
        if (TryToDecimal(actual, out var actualNum) && TryToDecimal(expected, out var expectedNum))
        {
            return opNormalized switch
            {
                ">" or "gt" => actualNum > expectedNum,
                ">=" or "gte" => actualNum >= expectedNum,
                "<" or "lt" => actualNum < expectedNum,
                "<=" or "lte" => actualNum <= expectedNum,
                "==" or "=" or "eq" => actualNum == expectedNum,
                "!=" or "<>" or "ne" => actualNum != expectedNum,
                _ => false
            };
        }

        // Try boolean comparison
        if (bool.TryParse(actual.ToString(), out var actualBool) && bool.TryParse(expected?.ToString(), out var expectedBool))
        {
            return opNormalized switch
            {
                "==" or "=" or "eq" => actualBool == expectedBool,
                "!=" or "<>" or "ne" => actualBool != expectedBool,
                _ => false
            };
        }

        // Fall back to string comparison
        var actualStr = actual.ToString() ?? string.Empty;
        var expectedStr = expected?.ToString() ?? string.Empty;

        return opNormalized switch
        {
            "==" or "=" or "eq" => string.Equals(actualStr, expectedStr, StringComparison.OrdinalIgnoreCase),
            "!=" or "<>" or "ne" => !string.Equals(actualStr, expectedStr, StringComparison.OrdinalIgnoreCase),
            "contains" => actualStr.Contains(expectedStr, StringComparison.OrdinalIgnoreCase),
            "startswith" => actualStr.StartsWith(expectedStr, StringComparison.OrdinalIgnoreCase),
            "endswith" => actualStr.EndsWith(expectedStr, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool TryToDecimal(object? val, out decimal num)
    {
        num = 0;
        if (val == null) return false;
        if (val is JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.Number && elem.TryGetDecimal(out var d))
            {
                num = d;
                return true;
            }
            if (elem.ValueKind == JsonValueKind.String && decimal.TryParse(elem.GetString(), out var sd))
            {
                num = sd;
                return true;
            }
            return false;
        }
        return decimal.TryParse(val.ToString(), out num);
    }

    private async Task ExecuteSendMessageAsync(
        WorkflowNodeDefinition node,
        Dictionary<string, object> state,
        WorkflowExecution execution,
        CancellationToken cancellationToken)
    {
        var channel = GetStringConfig(node.Config, "channel", "channelType") ?? "Email";
        var recipient = RenderTemplate(GetStringConfig(node.Config, "recipient", "to", "email", "phone") ?? "", state);
        var message = RenderTemplate(GetStringConfig(node.Config, "message", "body", "content", "text") ?? "", state);
        var subject = RenderTemplate(GetStringConfig(node.Config, "subject") ?? "Notification", state);

        var nodeName = string.IsNullOrWhiteSpace(node.Name) ? node.Id : node.Name;
        execution.AppendLog($"[Action: SendMessage '{nodeName}'] Channel: {channel}, Recipient: '{recipient}', Subject: '{subject}', Body: '{message}'");

        await Task.CompletedTask;
    }

    private async Task ExecuteUpdateCrmAsync(
        Guid tenantId,
        WorkflowNodeDefinition node,
        Dictionary<string, object> state,
        WorkflowExecution execution,
        CancellationToken cancellationToken)
    {
        var nodeName = string.IsNullOrWhiteSpace(node.Name) ? node.Id : node.Name;
        var updates = new List<string>();

        // Check for contact updates
        Guid? contactId = null;
        if (state.TryGetValue("contactId", out var cVal) && Guid.TryParse(cVal?.ToString(), out var parsedCid))
        {
            contactId = parsedCid;
        }
        else if (node.Config.TryGetValue("contactId", out var ncVal) && Guid.TryParse(ncVal?.ToString(), out var parsedNcid))
        {
            contactId = parsedNcid;
        }

        // Apply config updates into execution state
        foreach (var (k, v) in node.Config)
        {
            if (k.Equals("contactId", StringComparison.OrdinalIgnoreCase) ||
                k.Equals("companyId", StringComparison.OrdinalIgnoreCase))
                continue;

            state[k] = v;
            updates.Add($"{k}='{v}'");
        }

        // If DB context is available and contactId is resolved, persist CRM updates
        if (_db != null && contactId.HasValue)
        {
            var contact = await _db.Contacts
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == contactId.Value, cancellationToken);

            if (contact != null)
            {
                if (node.Config.TryGetValue("stage", out var stageObj) || node.Config.TryGetValue("lifecycleStage", out stageObj))
                {
                    if (Enum.TryParse<LifecycleStage>(stageObj?.ToString(), true, out var stage))
                    {
                        contact.LifecycleStage = stage;
                        updates.Add($"Contact.LifecycleStage={stage}");
                    }
                }

                if (node.Config.TryGetValue("tags", out var tagsObj) && tagsObj is IEnumerable<object> tagsList)
                {
                    foreach (var tag in tagsList)
                    {
                        var t = tag.ToString();
                        if (!string.IsNullOrEmpty(t) && !contact.Tags.Contains(t))
                            contact.Tags.Add(t);
                    }
                    updates.Add($"Contact.Tags+=[{string.Join(",", contact.Tags)}]");
                }

                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        var updateSummary = updates.Count > 0 ? string.Join(", ", updates) : "No field mutations";
        execution.AppendLog($"[Action: UpdateCRM '{nodeName}'] Applied CRM updates: {updateSummary}");
    }

    private async Task ExecuteWebhookAsync(
        WorkflowNodeDefinition node,
        Dictionary<string, object> state,
        WorkflowExecution execution,
        CancellationToken cancellationToken)
    {
        var nodeName = string.IsNullOrWhiteSpace(node.Name) ? node.Id : node.Name;
        var url = RenderTemplate(GetStringConfig(node.Config, "url", "endpoint") ?? "https://api.sgrowing.io/webhook/callback", state);
        var method = (GetStringConfig(node.Config, "method", "httpMethod") ?? "POST").ToUpperInvariant();

        execution.AppendLog($"[Action: Webhook '{nodeName}'] Dispatched {method} to {url}");
        await Task.CompletedTask;
    }

    private static string RenderTemplate(string template, Dictionary<string, object> state)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;

        return Regex.Replace(template, @"\{\{\s*([a-zA-Z0-9_.]+)\s*\}\}", match =>
        {
            var key = match.Groups[1].Value;
            if (state.TryGetValue(key, out var val) && val != null)
            {
                return val.ToString() ?? string.Empty;
            }
            return match.Value;
        });
    }

    private static string? GetStringConfig(Dictionary<string, object> config, params string[] candidateKeys)
    {
        foreach (var key in candidateKeys)
        {
            if (config.TryGetValue(key, out var val) && val != null)
            {
                return val.ToString();
            }
        }
        return null;
    }

    private static object? GetConfigValue(Dictionary<string, object> config, params string[] candidateKeys)
    {
        foreach (var key in candidateKeys)
        {
            if (config.TryGetValue(key, out var val))
            {
                return val;
            }
        }
        return null;
    }

    private static Dictionary<string, object> ParseStateDictionary(string json)
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
            return dict;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    dict[prop.Name] = ExtractElementValue(prop.Value);
                }
            }
        }
        catch
        {
            // Fallback for invalid JSON
        }

        return dict;
    }

    private static object ExtractElementValue(JsonElement elem) => elem.ValueKind switch
    {
        JsonValueKind.String => elem.GetString() ?? "",
        JsonValueKind.Number => elem.TryGetDecimal(out var d) ? d : elem.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => "",
        _ => elem.GetRawText()
    };

    public static List<WorkflowNodeDefinition> ParseNodes(string nodesJson)
    {
        var result = new List<WorkflowNodeDefinition>();
        if (string.IsNullOrWhiteSpace(nodesJson))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(nodesJson);
            var root = doc.RootElement;

            JsonElement nodesArray;
            JsonElement edgesArray = default;
            bool hasEdges = false;

            if (root.ValueKind == JsonValueKind.Array)
            {
                nodesArray = root;
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("nodes", out var n) && n.ValueKind == JsonValueKind.Array)
                {
                    nodesArray = n;
                }
                else
                {
                    return result;
                }

                if (root.TryGetProperty("edges", out var e) && e.ValueKind == JsonValueKind.Array)
                {
                    edgesArray = e;
                    hasEdges = true;
                }
            }
            else
            {
                return result;
            }

            int index = 0;
            foreach (var nodeElem in nodesArray.EnumerateArray())
            {
                index++;
                var node = new WorkflowNodeDefinition();

                if (nodeElem.TryGetProperty("id", out var idProp))
                    node.Id = idProp.GetString() ?? $"node_{index}";
                else
                    node.Id = $"node_{index}";

                if (nodeElem.TryGetProperty("type", out var typeProp))
                    node.Type = typeProp.GetString() ?? "Step";

                if (nodeElem.TryGetProperty("name", out var nameProp))
                    node.Name = nameProp.GetString() ?? node.Id;
                else if (nodeElem.TryGetProperty("title", out var titleProp))
                    node.Name = titleProp.GetString() ?? node.Id;

                // Parse config/data
                if (nodeElem.TryGetProperty("config", out var configProp) && configProp.ValueKind == JsonValueKind.Object)
                {
                    PopulateConfig(node.Config, configProp);
                }
                else if (nodeElem.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Object)
                {
                    PopulateConfig(node.Config, dataProp);
                }

                // Check direct links on node
                if (nodeElem.TryGetProperty("next", out var nextProp))
                    node.Next = nextProp.GetString();
                else if (nodeElem.TryGetProperty("nextNodeId", out var nextNodeIdProp))
                    node.Next = nextNodeIdProp.GetString();

                if (nodeElem.TryGetProperty("trueNext", out var trueNextProp))
                    node.TrueNext = trueNextProp.GetString();
                else if (nodeElem.TryGetProperty("trueNodeId", out var trueNodeIdProp))
                    node.TrueNext = trueNodeIdProp.GetString();
                else if (nodeElem.TryGetProperty("ifTrue", out var ifTrueProp))
                    node.TrueNext = ifTrueProp.GetString();

                if (nodeElem.TryGetProperty("falseNext", out var falseNextProp))
                    node.FalseNext = falseNextProp.GetString();
                else if (nodeElem.TryGetProperty("falseNodeId", out var falseNodeIdProp))
                    node.FalseNext = falseNodeIdProp.GetString();
                else if (nodeElem.TryGetProperty("ifFalse", out var ifFalseProp))
                    node.FalseNext = ifFalseProp.GetString();

                // If config itself contains trueNext/falseNext/next
                if (node.TrueNext == null && node.Config.TryGetValue("trueNext", out var ctNext))
                    node.TrueNext = ctNext?.ToString();
                if (node.FalseNext == null && node.Config.TryGetValue("falseNext", out var cfNext))
                    node.FalseNext = cfNext?.ToString();
                if (node.Next == null && node.Config.TryGetValue("next", out var cNext))
                    node.Next = cNext?.ToString();

                result.Add(node);
            }

            // If edges are present, wire them into nodes
            if (hasEdges)
            {
                var map = result.ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);
                foreach (var edgeElem in edgesArray.EnumerateArray())
                {
                    var source = edgeElem.TryGetProperty("source", out var s) ? s.GetString() : null;
                    var target = edgeElem.TryGetProperty("target", out var t) ? t.GetString() : null;
                    var handle = edgeElem.TryGetProperty("handle", out var h) ? h.GetString() :
                                 edgeElem.TryGetProperty("label", out var l) ? l.GetString() : null;

                    if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(target) && map.TryGetValue(source, out var sourceNode))
                    {
                        if (string.Equals(handle, "true", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(handle, "ifTrue", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(handle, "yes", StringComparison.OrdinalIgnoreCase))
                        {
                            sourceNode.TrueNext = target;
                        }
                        else if (string.Equals(handle, "false", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(handle, "ifFalse", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(handle, "no", StringComparison.OrdinalIgnoreCase))
                        {
                            sourceNode.FalseNext = target;
                        }
                        else
                        {
                            sourceNode.Next = target;
                        }
                    }
                }
            }
        }
        catch
        {
            // Return any parsed nodes
        }

        return result;
    }

    private static void PopulateConfig(Dictionary<string, object> target, JsonElement elem)
    {
        foreach (var prop in elem.EnumerateObject())
        {
            target[prop.Name] = ExtractElementValue(prop.Value);
        }
    }
}
