using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;

namespace SGrowing.Api.Modules.Journeys;

public record SaveJourneyDefinitionRequest(
    Guid? Id,
    string Name,
    string Description,
    string TriggerEvent,
    List<string>? ConversionEvents,
    List<JourneyNode> Nodes,
    bool IsActive = true
);

public record StartJourneyRequest(
    Guid ContactId,
    Dictionary<string, object>? Attributes = null
);

public record IngestJourneyEventRequest(
    Guid ContactId,
    string EventName,
    Dictionary<string, object>? Payload = null
);

public interface IStatefulJourneyEngineService
{
    DagValidationResult ValidateDag(List<JourneyNode> nodes);
    Task<JourneyDefinition> SaveJourneyDefinitionAsync(Guid tenantId, SaveJourneyDefinitionRequest request, CancellationToken ct = default);
    Task<List<JourneyDefinition>> GetJourneyDefinitionsAsync(Guid tenantId, CancellationToken ct = default);
    Task<JourneyDefinition?> GetJourneyDefinitionByIdAsync(Guid tenantId, Guid journeyId, CancellationToken ct = default);
    Task<JourneyInstance> StartJourneyAsync(Guid tenantId, Guid journeyId, Guid contactId, Dictionary<string, object>? initialAttributes = null, CancellationToken ct = default);
    Task<JourneyEventIngestResult> IngestEventAsync(Guid tenantId, Guid contactId, string eventName, Dictionary<string, object>? payload = null, CancellationToken ct = default);
    Task<int> SweepExpiredTimeoutsAsync(Guid tenantId, CancellationToken ct = default);
    Task<JourneyMetricsResponse> GetJourneyMetricsAsync(Guid tenantId, Guid journeyId, CancellationToken ct = default);
    Task<List<JourneyInstance>> GetJourneyInstancesAsync(Guid tenantId, Guid journeyId, CancellationToken ct = default);
}

public class StatefulJourneyEngineService : IStatefulJourneyEngineService
{
    private readonly SGrowingDbContext _db;
    private readonly IOutbox? _outbox;
    private readonly ILogger<StatefulJourneyEngineService>? _logger;

    public StatefulJourneyEngineService(
        SGrowingDbContext db,
        IOutbox? outbox = null,
        ILogger<StatefulJourneyEngineService>? logger = null)
    {
        _db = db;
        _outbox = outbox;
        _logger = logger;
    }

    // =========================================================================
    // 1. Visual DAG Validation & Cycle Detection (Kahn's Algorithm + DFS Trace)
    // =========================================================================

    public DagValidationResult ValidateDag(List<JourneyNode> nodes)
    {
        if (nodes == null || nodes.Count == 0)
        {
            return new DagValidationResult(false, "Journey DAG must contain at least one node.");
        }

        var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                return new DagValidationResult(false, "Every node in the DAG must have a valid non-empty Id.");
            }
            if (!nodeIds.Add(node.Id))
            {
                return new DagValidationResult(false, $"Duplicate node Id detected: '{node.Id}'.");
            }
        }

        var hasTrigger = nodes.Any(n => n.Type == JourneyNodeType.Trigger);
        if (!hasTrigger)
        {
            return new DagValidationResult(false, "Journey DAG must contain at least one Trigger node as an entrypoint.");
        }

        // Validate that all outgoing edges point to valid nodes
        var adj = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            var targets = GetOutgoingTargets(node);
            foreach (var target in targets)
            {
                if (!nodeIds.Contains(target))
                {
                    return new DagValidationResult(false, $"Node '{node.Id}' ({node.Name}) references nonexistent target node '{target}'.");
                }
            }
            adj[node.Id] = targets;
        }

        // Kahn's Algorithm for Topological Sort & Cycle Detection
        var inDegree = nodes.ToDictionary(n => n.Id, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var (_, targets) in adj)
        {
            foreach (var target in targets)
            {
                inDegree[target]++;
            }
        }

        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var visited = new List<string>();

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            visited.Add(curr);

            if (adj.TryGetValue(curr, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0)
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        if (visited.Count < nodes.Count)
        {
            var unvisited = nodes.Select(n => n.Id).Except(visited, StringComparer.OrdinalIgnoreCase).ToList();
            var cyclePath = FindCyclePathDfs(adj, unvisited);
            var cycleStr = cyclePath != null && cyclePath.Count > 0
                ? string.Join(" -> ", cyclePath)
                : string.Join(", ", unvisited);

            return new DagValidationResult(
                false,
                $"Cycle detected in Journey DAG: {cycleStr}. Graphs must be strictly acyclic.",
                cyclePath
            );
        }

        return new DagValidationResult(true);
    }

    private static List<string> GetOutgoingTargets(JourneyNode node)
    {
        var targets = new List<string>();
        switch (node.Type)
        {
            case JourneyNodeType.Trigger:
            case JourneyNodeType.Delay:
            case JourneyNodeType.ChannelAction:
                if (!string.IsNullOrWhiteSpace(node.NextNodeId))
                    targets.Add(node.NextNodeId);
                break;

            case JourneyNodeType.WaitUntilEvent:
                if (!string.IsNullOrWhiteSpace(node.SuccessNextNodeId))
                    targets.Add(node.SuccessNextNodeId);
                if (!string.IsNullOrWhiteSpace(node.TimeoutNextNodeId))
                    targets.Add(node.TimeoutNextNodeId);
                break;

            case JourneyNodeType.ConditionBranch:
                if (!string.IsNullOrWhiteSpace(node.TrueNextNodeId))
                    targets.Add(node.TrueNextNodeId);
                if (!string.IsNullOrWhiteSpace(node.FalseNextNodeId))
                    targets.Add(node.FalseNextNodeId);
                break;

            case JourneyNodeType.Exit:
                // Exit nodes terminate flow
                break;
        }
        return targets;
    }

    private static List<string>? FindCyclePathDfs(Dictionary<string, List<string>> adj, IEnumerable<string> startNodes)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var recStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = new List<string>();

        foreach (var start in startNodes)
        {
            if (Dfs(start, adj, visited, recStack, path))
            {
                var cycleStart = path.Last();
                var cycleStartIndex = path.IndexOf(cycleStart);
                if (cycleStartIndex >= 0 && cycleStartIndex < path.Count - 1)
                {
                    return path.Skip(cycleStartIndex).ToList();
                }
                return path;
            }
        }

        return null;

        static bool Dfs(
            string u,
            Dictionary<string, List<string>> adj,
            HashSet<string> visited,
            HashSet<string> recStack,
            List<string> path)
        {
            visited.Add(u);
            recStack.Add(u);
            path.Add(u);

            if (adj.TryGetValue(u, out var neighbors))
            {
                foreach (var v in neighbors)
                {
                    if (!visited.Contains(v))
                    {
                        if (Dfs(v, adj, visited, recStack, path))
                            return true;
                    }
                    else if (recStack.Contains(v))
                    {
                        path.Add(v); // Close cycle
                        return true;
                    }
                }
            }

            recStack.Remove(u);
            path.RemoveAt(path.Count - 1);
            return false;
        }
    }

    // =========================================================================
    // 2. Journey Definition CRUD
    // =========================================================================

    public async Task<JourneyDefinition> SaveJourneyDefinitionAsync(
        Guid tenantId,
        SaveJourneyDefinitionRequest request,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));

        var validation = ValidateDag(request.Nodes);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Invalid Journey DAG: {validation.ErrorMessage}");
        }

        JourneyDefinition definition;
        if (request.Id.HasValue && request.Id.Value != Guid.Empty)
        {
            definition = await _db.JourneyDefinitions
                .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == request.Id.Value, ct)
                ?? new JourneyDefinition { Id = request.Id.Value, TenantId = tenantId };

            if (!_db.JourneyDefinitions.Local.Contains(definition))
            {
                _db.JourneyDefinitions.Add(definition);
            }
        }
        else
        {
            definition = new JourneyDefinition { Id = Guid.NewGuid(), TenantId = tenantId };
            _db.JourneyDefinitions.Add(definition);
        }

        definition.Name = request.Name;
        definition.Description = request.Description;
        definition.TriggerEvent = request.TriggerEvent;
        definition.ConversionEvents = request.ConversionEvents ?? new List<string>();
        definition.Nodes = request.Nodes;
        definition.IsActive = request.IsActive;
        definition.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return definition;
    }

    public async Task<List<JourneyDefinition>> GetJourneyDefinitionsAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.JourneyDefinitions
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.UpdatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<JourneyDefinition?> GetJourneyDefinitionByIdAsync(Guid tenantId, Guid journeyId, CancellationToken ct = default)
    {
        return await _db.JourneyDefinitions
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == journeyId, ct);
    }

    public async Task<List<JourneyInstance>> GetJourneyInstancesAsync(Guid tenantId, Guid journeyId, CancellationToken ct = default)
    {
        return await _db.JourneyInstances
            .Where(i => i.TenantId == tenantId && i.JourneyDefinitionId == journeyId)
            .OrderByDescending(i => i.EnrolledAtUtc)
            .ToListAsync(ct);
    }

    // =========================================================================
    // 3. Journey Start & State Machine Traversal
    // =========================================================================

    public async Task<JourneyInstance> StartJourneyAsync(
        Guid tenantId,
        Guid journeyId,
        Guid contactId,
        Dictionary<string, object>? initialAttributes = null,
        CancellationToken ct = default)
    {
        var definition = await GetJourneyDefinitionByIdAsync(tenantId, journeyId, ct)
            ?? throw new KeyNotFoundException($"JourneyDefinition '{journeyId}' not found for tenant '{tenantId}'.");

        var triggerNode = definition.Nodes.FirstOrDefault(n => n.Type == JourneyNodeType.Trigger)
            ?? throw new InvalidOperationException($"Journey '{journeyId}' does not have a Trigger node.");

        var instance = new JourneyInstance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JourneyDefinitionId = journeyId,
            ContactId = contactId,
            CurrentNodeId = triggerNode.Id,
            Status = JourneyInstanceStatus.Active,
            EnrolledAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var attributes = initialAttributes != null
            ? new Dictionary<string, object>(initialAttributes, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        // Fetch contact details from DB if available to enrich attributes
        var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.Id == contactId, ct);
        if (contact != null)
        {
            if (!attributes.ContainsKey("first_name")) attributes["first_name"] = contact.FirstName ?? "";
            if (!attributes.ContainsKey("last_name")) attributes["last_name"] = contact.LastName ?? "";
            if (!attributes.ContainsKey("email") && !string.IsNullOrWhiteSpace(contact.Email)) attributes["email"] = contact.Email;
            if (!attributes.ContainsKey("phone") && !string.IsNullOrWhiteSpace(contact.Phone)) attributes["phone"] = contact.Phone;
            if (!attributes.ContainsKey("tags") && contact.Tags != null && contact.Tags.Count > 0) attributes["tags"] = contact.Tags;

            // Enrich from contact custom fields if present
            var custom = contact.GetCustomFields();
            if (custom != null)
            {
                foreach (var (k, v) in custom)
                {
                    if (!attributes.ContainsKey(k)) attributes[k] = v;
                }
            }
        }

        instance.CustomerAttributes = attributes;
        _db.JourneyInstances.Add(instance);

        await ExecuteJourneyAsync(instance, definition, ct);
        await _db.SaveChangesAsync(ct);

        return instance;
    }

    private async Task ExecuteJourneyAsync(
        JourneyInstance instance,
        JourneyDefinition definition,
        CancellationToken ct)
    {
        const int maxSteps = 100;
        int stepsTaken = 0;

        while (instance.Status == JourneyInstanceStatus.Active && stepsTaken < maxSteps)
        {
            stepsTaken++;
            var currentNode = definition.Nodes.FirstOrDefault(n => string.Equals(n.Id, instance.CurrentNodeId, StringComparison.OrdinalIgnoreCase));
            if (currentNode == null)
            {
                instance.Status = JourneyInstanceStatus.Completed;
                instance.CompletedAtUtc = DateTime.UtcNow;
                AddStepLog(instance, instance.CurrentNodeId, "Unknown Node", JourneyNodeType.Exit, "Completed", "Next node was not found. Halting journey.");
                break;
            }

            switch (currentNode.Type)
            {
                case JourneyNodeType.Trigger:
                    AddStepLog(instance, currentNode.Id, currentNode.Name, currentNode.Type, "Executed", $"Trigger activated for event '{currentNode.EventName}'.");
                    if (string.IsNullOrWhiteSpace(currentNode.NextNodeId))
                    {
                        instance.Status = JourneyInstanceStatus.Completed;
                        instance.CompletedAtUtc = DateTime.UtcNow;
                    }
                    else
                    {
                        instance.CurrentNodeId = currentNode.NextNodeId;
                    }
                    break;

                case JourneyNodeType.Delay:
                    if (currentNode.DelaySeconds <= 0)
                    {
                        AddStepLog(instance, currentNode.Id, currentNode.Name, currentNode.Type, "Executed", "Zero delay specified; advanced immediately.");
                        if (string.IsNullOrWhiteSpace(currentNode.NextNodeId))
                        {
                            instance.Status = JourneyInstanceStatus.Completed;
                            instance.CompletedAtUtc = DateTime.UtcNow;
                        }
                        else
                        {
                            instance.CurrentNodeId = currentNode.NextNodeId;
                        }
                    }
                    else
                    {
                        instance.NextExecutionAtUtc = DateTime.UtcNow.AddSeconds(currentNode.DelaySeconds);
                        AddStepLog(instance, currentNode.Id, currentNode.Name, currentNode.Type, "Waiting",
                            $"Delaying execution for {currentNode.DelaySeconds}s until {instance.NextExecutionAtUtc:O} (Timezone: {currentNode.Timezone ?? "UTC"}).");
                        // For delay progression in asynchronous mode, pointer advances to NextNodeId
                        instance.CurrentNodeId = currentNode.NextNodeId ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(instance.CurrentNodeId))
                        {
                            instance.Status = JourneyInstanceStatus.Completed;
                            instance.CompletedAtUtc = DateTime.UtcNow;
                        }
                        return; // Halt until delay completes
                    }
                    break;

                case JourneyNodeType.WaitUntilEvent:
                    var ttl = currentNode.TtlSeconds > 0 ? currentNode.TtlSeconds : 86400; // default 24h
                    instance.Status = JourneyInstanceStatus.WaitingForEvent;
                    instance.WaitUntilEventName = currentNode.EventName;
                    instance.WaitUntilExpiresAtUtc = DateTime.UtcNow.AddSeconds(ttl);
                    AddStepLog(instance, currentNode.Id, currentNode.Name, currentNode.Type, "Waiting",
                        $"Waiting for event '{currentNode.EventName}' with sliding TTL of {ttl}s (Expires at {instance.WaitUntilExpiresAtUtc:O}).");
                    return; // Halt state machine; resumes upon event ingest or timeout sweep

                case JourneyNodeType.ConditionBranch:
                    var conditionExpr = currentNode.ConditionExpression ?? string.Empty;
                    var isMatch = EvaluateCondition(conditionExpr, instance.CustomerAttributes);

                    if (isMatch)
                    {
                        AddStepLog(instance, currentNode.Id, currentNode.Name, currentNode.Type, "BranchTrue",
                            $"Condition '{conditionExpr}' evaluated to TRUE. Routing to '{currentNode.TrueNextNodeId}'.");
                        instance.CurrentNodeId = currentNode.TrueNextNodeId ?? string.Empty;
                    }
                    else
                    {
                        AddStepLog(instance, currentNode.Id, currentNode.Name, currentNode.Type, "BranchFalse",
                            $"Condition '{conditionExpr}' evaluated to FALSE. Routing to '{currentNode.FalseNextNodeId}'.");
                        instance.CurrentNodeId = currentNode.FalseNextNodeId ?? string.Empty;
                    }

                    if (string.IsNullOrWhiteSpace(instance.CurrentNodeId))
                    {
                        instance.Status = JourneyInstanceStatus.Completed;
                        instance.CompletedAtUtc = DateTime.UtcNow;
                    }
                    break;

                case JourneyNodeType.ChannelAction:
                    var payload = InterpolateTemplate(currentNode.ActionPayload ?? string.Empty, instance.CustomerAttributes);
                    await DispatchChannelActionAsync(instance.TenantId, instance.ContactId, currentNode.ChannelType ?? "Email", payload, ct);

                    AddStepLog(instance, currentNode.Id, currentNode.Name, currentNode.Type, "Executed",
                        $"Dispatched multi-channel payload [{currentNode.ChannelType}]: {payload}");

                    if (string.IsNullOrWhiteSpace(currentNode.NextNodeId))
                    {
                        instance.Status = JourneyInstanceStatus.Completed;
                        instance.CompletedAtUtc = DateTime.UtcNow;
                    }
                    else
                    {
                        instance.CurrentNodeId = currentNode.NextNodeId;
                    }
                    break;

                case JourneyNodeType.Exit:
                    instance.Status = JourneyInstanceStatus.Completed;
                    instance.CompletedAtUtc = DateTime.UtcNow;
                    AddStepLog(instance, currentNode.Id, currentNode.Name, currentNode.Type, "Completed",
                        "Journey reached Exit node and marked Completed.");
                    break;
            }
        }

        instance.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static void AddStepLog(
        JourneyInstance instance,
        string nodeId,
        string nodeName,
        JourneyNodeType type,
        string status,
        string details)
    {
        var history = instance.ExecutionHistory;
        history.Add(new JourneyStepExecutionRecord
        {
            NodeId = nodeId,
            NodeName = nodeName,
            NodeType = type,
            Status = status,
            Details = details,
            ExecutedAtUtc = DateTime.UtcNow
        });
        instance.ExecutionHistory = history;
    }

    // =========================================================================
    // 4. Multi-Channel Dispatch & Template Interpolation
    // =========================================================================

    private async Task DispatchChannelActionAsync(
        Guid tenantId,
        Guid contactId,
        string channelType,
        string payload,
        CancellationToken ct)
    {
        if (_outbox != null)
        {
            var eventType = channelType.ToLowerInvariant() switch
            {
                "whatsapp" => "Journey.WhatsApp.Send",
                "sms" => "Journey.Sms.Send",
                "webhook" => "Journey.Webhook.Dispatch",
                _ => "Journey.Email.Send"
            };

            await _outbox.EnqueueAsync(tenantId, eventType, new
            {
                contactId,
                channelType,
                payload,
                dispatchedAtUtc = DateTime.UtcNow
            }, ct);
        }
        else
        {
            _logger?.LogInformation("Channel action dispatched: Tenant {TenantId}, Contact {ContactId}, Channel {Channel}, Payload: {Payload}",
                tenantId, contactId, channelType, payload);
        }
    }

    public static string InterpolateTemplate(string template, Dictionary<string, object> attributes)
    {
        if (string.IsNullOrWhiteSpace(template)) return string.Empty;

        var result = template;
        foreach (var (key, value) in attributes)
        {
            var placeholder = "{{" + key + "}}";
            result = result.Replace(placeholder, value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }

    // =========================================================================
    // 5. Customer Attribute Condition Evaluator
    // =========================================================================

    public static bool EvaluateCondition(string conditionExpression, Dictionary<string, object> attributes)
    {
        if (string.IsNullOrWhiteSpace(conditionExpression)) return true;

        var trimmed = conditionExpression.Trim();
        if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (trimmed.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;

        // 1. TagContains pattern: TagContains("VIP") or Tags.Contains("VIP") or TagContains('VIP')
        var tagMatch = Regex.Match(trimmed, @"(?:TagContains|Tags\.Contains)\s*\(\s*['""]([^'""]+)['""]\s*\)", RegexOptions.IgnoreCase);
        if (tagMatch.Success)
        {
            var expectedTag = tagMatch.Groups[1].Value.Trim();
            return CheckTagAttribute(attributes, expectedTag);
        }

        // Also support "Tags contains 'VIP'"
        var tagContainsMatch = Regex.Match(trimmed, @"(?:Tags|Tag)\s+contains\s+['""]?([^'""]+)['""]?", RegexOptions.IgnoreCase);
        if (tagContainsMatch.Success)
        {
            var expectedTag = tagContainsMatch.Groups[1].Value.Trim();
            return CheckTagAttribute(attributes, expectedTag);
        }

        // 2. Comparison operators: >=, <=, !=, ==, >, <
        string[] operators = [">=", "<=", "!=", "==", ">", "<"];
        foreach (var op in operators)
        {
            var opIndex = trimmed.IndexOf(op, StringComparison.Ordinal);
            if (opIndex > 0)
            {
                var leftField = trimmed[..opIndex].Trim();
                var rightValue = trimmed[(opIndex + op.Length)..].Trim();

                // Strip quotes from right value if present
                if ((rightValue.StartsWith('"') && rightValue.EndsWith('"')) ||
                    (rightValue.StartsWith('\'') && rightValue.EndsWith('\'')))
                {
                    rightValue = rightValue[1..^1];
                }

                // Look up attribute value
                if (!TryGetAttributeValue(attributes, leftField, out var attrVal))
                {
                    return op == "!="; // missing attribute is not equal
                }

                return CompareValues(attrVal, op, rightValue);
            }
        }

        // If no operator found, check boolean truthiness of named attribute
        if (TryGetAttributeValue(attributes, trimmed, out var boolVal))
        {
            if (boolVal is bool b) return b;
            if (bool.TryParse(boolVal?.ToString(), out var parsedBool)) return parsedBool;
            return !string.IsNullOrWhiteSpace(boolVal?.ToString());
        }

        return false;
    }

    private static bool CheckTagAttribute(Dictionary<string, object> attributes, string expectedTag)
    {
        string[] tagKeys = ["Tags", "Tag", "tags", "tag"];
        foreach (var key in tagKeys)
        {
            if (attributes.TryGetValue(key, out var val) && val != null)
            {
                if (val is IEnumerable<string> strList)
                {
                    return strList.Any(t => string.Equals(t.Trim(), expectedTag, StringComparison.OrdinalIgnoreCase));
                }
                if (val is IEnumerable<object> objList)
                {
                    return objList.Any(o => string.Equals(o?.ToString()?.Trim(), expectedTag, StringComparison.OrdinalIgnoreCase));
                }
                var strVal = val.ToString() ?? "";
                var tokens = strVal.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Any(t => string.Equals(t.Trim(), expectedTag, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
                if (strVal.Contains(expectedTag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool TryGetAttributeValue(Dictionary<string, object> attributes, string key, out object? value)
    {
        foreach (var (k, v) in attributes)
        {
            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
            {
                value = v;
                return true;
            }
        }
        value = null;
        return false;
    }

    private static bool CompareValues(object? left, string op, string rightStr)
    {
        var leftStr = left?.ToString() ?? string.Empty;

        // Try numeric comparison
        if (decimal.TryParse(leftStr, out var leftNum) && decimal.TryParse(rightStr, out var rightNum))
        {
            return op switch
            {
                ">" => leftNum > rightNum,
                ">=" => leftNum >= rightNum,
                "<" => leftNum < rightNum,
                "<=" => leftNum <= rightNum,
                "==" => leftNum == rightNum,
                "!=" => leftNum != rightNum,
                _ => false
            };
        }

        // String comparison
        var strEqual = string.Equals(leftStr, rightStr, StringComparison.OrdinalIgnoreCase);
        return op switch
        {
            "==" => strEqual,
            "!=" => !strEqual,
            _ => false
        };
    }

    // =========================================================================
    // 6. Ingest Event: Dynamic Unenrollment, Wait-Until Matching, Auto-Enrollment
    // =========================================================================

    public async Task<JourneyEventIngestResult> IngestEventAsync(
        Guid tenantId,
        Guid contactId,
        string eventName,
        Dictionary<string, object>? payload = null,
        CancellationToken ct = default)
    {
        int unenrolledCount = 0;
        int advancedCount = 0;
        int enrolledCount = 0;

        // 1. Dynamic Unenrollment: Immediately purge contact from journeys when conversion event occurs
        var activeOrWaitingInstances = await _db.JourneyInstances
            .Where(i => i.TenantId == tenantId &&
                        i.ContactId == contactId &&
                        (i.Status == JourneyInstanceStatus.Active || i.Status == JourneyInstanceStatus.WaitingForEvent))
            .ToListAsync(ct);

        foreach (var instance in activeOrWaitingInstances)
        {
            var def = await GetJourneyDefinitionByIdAsync(tenantId, instance.JourneyDefinitionId, ct);
            if (def != null && def.ConversionEvents.Any(e => string.Equals(e, eventName, StringComparison.OrdinalIgnoreCase)))
            {
                instance.Status = JourneyInstanceStatus.Unenrolled;
                instance.CompletedAtUtc = DateTime.UtcNow;
                instance.UpdatedAtUtc = DateTime.UtcNow;
                AddStepLog(instance, instance.CurrentNodeId, "Dynamic Unenrollment", JourneyNodeType.Exit, "Unenrolled",
                    $"Immediately purged from journey due to conversion event '{eventName}'.");
                unenrolledCount++;
            }
        }

        // 2. Wait-Until Event Matching: Advance instances waiting for this specific event before TTL expires
        var waitingInstances = await _db.JourneyInstances
            .Where(i => i.TenantId == tenantId &&
                        i.ContactId == contactId &&
                        i.Status == JourneyInstanceStatus.WaitingForEvent &&
                        i.WaitUntilEventName != null &&
                        i.WaitUntilEventName.ToLower() == eventName.ToLower())
            .ToListAsync(ct);

        foreach (var instance in waitingInstances)
        {
            // Check if not expired
            if (!instance.WaitUntilExpiresAtUtc.HasValue || instance.WaitUntilExpiresAtUtc.Value > DateTime.UtcNow)
            {
                var def = await GetJourneyDefinitionByIdAsync(tenantId, instance.JourneyDefinitionId, ct);
                if (def != null)
                {
                    var waitNode = def.Nodes.FirstOrDefault(n => string.Equals(n.Id, instance.CurrentNodeId, StringComparison.OrdinalIgnoreCase));
                    if (waitNode != null && !string.IsNullOrWhiteSpace(waitNode.SuccessNextNodeId))
                    {
                        instance.Status = JourneyInstanceStatus.Active;
                        instance.WaitUntilEventName = null;
                        instance.WaitUntilExpiresAtUtc = null;

                        // Merge any payload data into customer attributes
                        if (payload != null && payload.Count > 0)
                        {
                            var attrs = instance.CustomerAttributes;
                            foreach (var (k, v) in payload) attrs[k] = v;
                            instance.CustomerAttributes = attrs;
                        }

                        AddStepLog(instance, waitNode.Id, waitNode.Name, JourneyNodeType.WaitUntilEvent, "MatchedEvent",
                            $"Event '{eventName}' occurred before TTL. Advancing down success branch to '{waitNode.SuccessNextNodeId}'.");

                        instance.CurrentNodeId = waitNode.SuccessNextNodeId;
                        await ExecuteJourneyAsync(instance, def, ct);
                        advancedCount++;
                    }
                }
            }
        }

        // 3. Trigger Ingestion: Auto-enroll contact into active journeys listening to this trigger event
        var matchingDefinitions = await _db.JourneyDefinitions
            .Where(d => d.TenantId == tenantId && d.IsActive && d.TriggerEvent.ToLower() == eventName.ToLower())
            .ToListAsync(ct);

        foreach (var def in matchingDefinitions)
        {
            // Avoid duplicate active enrollment in same journey
            var alreadyEnrolled = await _db.JourneyInstances.AnyAsync(
                i => i.TenantId == tenantId &&
                     i.JourneyDefinitionId == def.Id &&
                     i.ContactId == contactId &&
                     (i.Status == JourneyInstanceStatus.Active || i.Status == JourneyInstanceStatus.WaitingForEvent),
                ct);

            if (!alreadyEnrolled)
            {
                await StartJourneyAsync(tenantId, def.Id, contactId, payload, ct);
                enrolledCount++;
            }
        }

        await _db.SaveChangesAsync(ct);

        return new JourneyEventIngestResult(
            IngestedEventName: eventName,
            UnenrolledCount: unenrolledCount,
            AdvancedCount: advancedCount,
            EnrolledCount: enrolledCount,
            Message: $"Event '{eventName}' ingested. Unenrolled: {unenrolledCount}, Advanced: {advancedCount}, Enrolled: {enrolledCount}."
        );
    }

    // =========================================================================
    // 7. Timeout Sweeper: Advance Expired Wait-Until Instances Down Timeout Branch
    // =========================================================================

    public async Task<int> SweepExpiredTimeoutsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expiredInstances = await _db.JourneyInstances
            .Where(i => i.TenantId == tenantId &&
                        i.Status == JourneyInstanceStatus.WaitingForEvent &&
                        i.WaitUntilExpiresAtUtc.HasValue &&
                        i.WaitUntilExpiresAtUtc.Value <= now)
            .Take(100)
            .ToListAsync(ct);

        int sweptCount = 0;

        foreach (var instance in expiredInstances)
        {
            var def = await GetJourneyDefinitionByIdAsync(tenantId, instance.JourneyDefinitionId, ct);
            if (def == null) continue;

            var waitNode = def.Nodes.FirstOrDefault(n => string.Equals(n.Id, instance.CurrentNodeId, StringComparison.OrdinalIgnoreCase));
            if (waitNode != null && !string.IsNullOrWhiteSpace(waitNode.TimeoutNextNodeId))
            {
                instance.Status = JourneyInstanceStatus.Active;
                instance.WaitUntilEventName = null;
                instance.WaitUntilExpiresAtUtc = null;

                AddStepLog(instance, waitNode.Id, waitNode.Name, JourneyNodeType.WaitUntilEvent, "TimedOut",
                    $"Wait-Until TTL expired for event '{waitNode.EventName}' without receiving event. Advancing down timeout branch to '{waitNode.TimeoutNextNodeId}'.");

                instance.CurrentNodeId = waitNode.TimeoutNextNodeId;
                await ExecuteJourneyAsync(instance, def, ct);
                sweptCount++;
            }
        }

        if (sweptCount > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return sweptCount;
    }

    // =========================================================================
    // 8. Analytics & Metrics Computation
    // =========================================================================

    public async Task<JourneyMetricsResponse> GetJourneyMetricsAsync(Guid tenantId, Guid journeyId, CancellationToken ct = default)
    {
        var definition = await GetJourneyDefinitionByIdAsync(tenantId, journeyId, ct)
            ?? throw new KeyNotFoundException($"JourneyDefinition '{journeyId}' not found.");

        var instances = await _db.JourneyInstances
            .Where(i => i.TenantId == tenantId && i.JourneyDefinitionId == journeyId)
            .ToListAsync(ct);

        int totalEnrolled = instances.Count;
        int currentlyActive = instances.Count(i => i.Status == JourneyInstanceStatus.Active);
        int currentlyWaiting = instances.Count(i => i.Status == JourneyInstanceStatus.WaitingForEvent);
        int totalCompleted = instances.Count(i => i.Status == JourneyInstanceStatus.Completed);
        int totalUnenrolled = instances.Count(i => i.Status == JourneyInstanceStatus.Unenrolled);

        double completionRate = totalEnrolled > 0
            ? Math.Round((double)totalCompleted / totalEnrolled * 100.0, 1)
            : 0.0;

        double dropOffRate = totalEnrolled > 0
            ? Math.Round((double)totalUnenrolled / totalEnrolled * 100.0, 1)
            : 0.0;

        var nodeMetrics = new List<JourneyNodeMetric>();

        foreach (var node in definition.Nodes)
        {
            int executionsCount = instances.Count(i => i.ExecutionHistory.Any(h => string.Equals(h.NodeId, node.Id, StringComparison.OrdinalIgnoreCase)));
            int currentContactsCount = instances.Count(i =>
                string.Equals(i.CurrentNodeId, node.Id, StringComparison.OrdinalIgnoreCase) &&
                (i.Status == JourneyInstanceStatus.Active || i.Status == JourneyInstanceStatus.WaitingForEvent));

            int dropOffCount = instances.Count(i =>
                i.Status == JourneyInstanceStatus.Unenrolled &&
                string.Equals(i.ExecutionHistory.LastOrDefault()?.NodeId, node.Id, StringComparison.OrdinalIgnoreCase));

            double conversionPercent = totalEnrolled > 0
                ? Math.Round((double)executionsCount / totalEnrolled * 100.0, 1)
                : 0.0;

            nodeMetrics.Add(new JourneyNodeMetric(
                NodeId: node.Id,
                NodeName: node.Name,
                NodeType: node.Type,
                ExecutionsCount: executionsCount,
                CurrentContactsCount: currentContactsCount,
                ConversionPercent: conversionPercent,
                DropOffCount: dropOffCount
            ));
        }

        return new JourneyMetricsResponse(
            JourneyId: journeyId,
            JourneyName: definition.Name,
            TotalEnrolled: totalEnrolled,
            CurrentlyActive: currentlyActive,
            CurrentlyWaiting: currentlyWaiting,
            TotalCompleted: totalCompleted,
            TotalUnenrolled: totalUnenrolled,
            CompletionRate: completionRate,
            DropOffRate: dropOffRate,
            NodeMetrics: nodeMetrics
        );
    }
}
