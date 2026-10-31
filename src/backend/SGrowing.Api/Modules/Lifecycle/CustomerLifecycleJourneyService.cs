using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Lifecycle;

#region Enums & Models

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LifecycleNodeType
{
    Trigger,
    Delay,
    Condition,
    Action
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TriggerType
{
    Event,
    Segment
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DelayUnit
{
    Minutes,
    Hours,
    Days,
    Weeks
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ActionChannel
{
    WhatsApp,
    Email,
    Push,
    Sms,
    Webhook,
    CrmTask
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum JourneyLifecycleStatus
{
    Draft,
    Active,
    Paused,
    Archived
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContactJourneyStatus
{
    InProgress,
    Paused,
    Completed,
    Exited
}

public class TriggerNodeConfig
{
    public TriggerType Type { get; set; } = TriggerType.Event;
    public string EventName { get; set; } = string.Empty;
    public string SegmentName { get; set; } = string.Empty;
    public Dictionary<string, string> FilterCriteria { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class DelayNodeConfig
{
    public double Value { get; set; } = 1;
    public DelayUnit Unit { get; set; } = DelayUnit.Days;

    public TimeSpan ToTimeSpan() => Unit switch
    {
        DelayUnit.Minutes => TimeSpan.FromMinutes(Math.Max(0.1, Value)),
        DelayUnit.Hours => TimeSpan.FromHours(Math.Max(0.1, Value)),
        DelayUnit.Days => TimeSpan.FromDays(Math.Max(0.1, Value)),
        DelayUnit.Weeks => TimeSpan.FromDays(Math.Max(0.1, Value) * 7),
        _ => TimeSpan.FromMinutes(Value)
    };

    public DateTime CalculateResumeAt(DateTime fromUtc) => fromUtc.Add(ToTimeSpan());
}

public class ConditionNodeConfig
{
    public string Property { get; set; } = string.Empty;
    public string Operator { get; set; } = "Equals"; // Equals, NotEquals, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual, Contains, IsTrue, IsFalse
    public string Value { get; set; } = string.Empty;
    public string? TrueNodeId { get; set; }
    public string? FalseNodeId { get; set; }
}

public class ActionNodeConfig
{
    public ActionChannel Channel { get; set; } = ActionChannel.WhatsApp;
    public ActionChannel? FallbackChannel { get; set; } = ActionChannel.Email;
    public string TemplateId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string MessageBody { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
    public string? CrmTaskTitle { get; set; }
    public string? CrmAssignee { get; set; }
    public bool SimulateChannelFailure { get; set; }
}

public class LifecycleJourneyNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public LifecycleNodeType Type { get; set; }
    public string? Description { get; set; }
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public List<string> NextNodeIds { get; set; } = new();

    public TriggerNodeConfig? TriggerConfig { get; set; }
    public DelayNodeConfig? DelayConfig { get; set; }
    public ConditionNodeConfig? ConditionConfig { get; set; }
    public ActionNodeConfig? ActionConfig { get; set; }
}

public class LifecycleJourneyEdge
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SourceNodeId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string? Label { get; set; }
}

public class CustomerLifecycleJourney
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JourneyLifecycleStatus Status { get; set; } = JourneyLifecycleStatus.Active;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<LifecycleJourneyNode> Nodes { get; set; } = new();
    public List<LifecycleJourneyEdge> Edges { get; set; } = new();

    // Aggregated Metrics
    public int TotalEnrolled { get; set; }
    public int TotalCompleted { get; set; }
    public int TotalExited { get; set; }
    public int ActiveCount { get; set; }
    public int FallbackRecoveryCount { get; set; }
    public double ConversionRate => TotalEnrolled > 0
        ? Math.Round((double)TotalCompleted / TotalEnrolled * 100, 1)
        : 0.0;
}

public class JourneyBreadcrumb
{
    public string NodeId { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public LifecycleNodeType NodeType { get; set; }
    public DateTime EnteredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExitedAtUtc { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class JourneyDeliveryLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string NodeId { get; set; } = string.Empty;
    public ActionChannel Channel { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool WasFallback { get; set; }
    public ActionChannel? OriginalChannel { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Details { get; set; } = string.Empty;
}

public class JourneyStatePointer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JourneyId { get; set; }
    public string ContactId { get; set; } = string.Empty;
    public string CurrentNodeId { get; set; } = string.Empty;
    public ContactJourneyStatus Status { get; set; } = ContactJourneyStatus.InProgress;
    public DateTime? ScheduledResumeAtUtc { get; set; }
    public DateTime EnrolledAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    public Dictionary<string, object> ContactProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<JourneyBreadcrumb> Breadcrumbs { get; set; } = new();
    public List<JourneyDeliveryLog> DeliveryLogs { get; set; } = new();
}

public class DagValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> TopologicalOrder { get; set; } = new();
}

public record CreateJourneyRequest(
    string Name,
    string Description,
    Guid? TenantId,
    List<LifecycleJourneyNode>? Nodes,
    List<LifecycleJourneyEdge>? Edges,
    JourneyLifecycleStatus? Status
);

public record UpdateJourneyRequest(
    string? Name,
    string? Description,
    List<LifecycleJourneyNode>? Nodes,
    List<LifecycleJourneyEdge>? Edges,
    JourneyLifecycleStatus? Status
);

public record TriggerContactRequest(
    string ContactId,
    string? EventName,
    string? SegmentName,
    Dictionary<string, object>? ContactProperties,
    bool ForceReEnroll = false
);

public class EnrollmentResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public JourneyStatePointer? StatePointer { get; set; }
    public int StepsExecutedImmediately { get; set; }
}

public class AdvanceDueResult
{
    public Guid JourneyId { get; set; }
    public int DueContactsFound { get; set; }
    public int AdvancedCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> ExecutionSummaries { get; set; } = new();
}

public class JourneyMetricsSummary
{
    public int TotalJourneys { get; set; }
    public int ActiveJourneys { get; set; }
    public int TotalEnrolledContacts { get; set; }
    public int ActiveInFlightContacts { get; set; }
    public int TotalCompletedContacts { get; set; }
    public int TotalFallbackRecoveries { get; set; }
    public double OverallConversionRate { get; set; }
    public Dictionary<string, int> ContactsPerStepDistribution { get; set; } = new();
}

#endregion

#region Service Interface

public interface ICustomerLifecycleJourneyService
{
    Task<IReadOnlyList<CustomerLifecycleJourney>> GetJourneysAsync(
        Guid? tenantId = null,
        string? search = null,
        JourneyLifecycleStatus? status = null,
        CancellationToken ct = default);

    Task<CustomerLifecycleJourney?> GetJourneyByIdAsync(Guid journeyId, CancellationToken ct = default);

    Task<CustomerLifecycleJourney> CreateJourneyAsync(CreateJourneyRequest request, CancellationToken ct = default);

    Task<CustomerLifecycleJourney> UpdateJourneyAsync(Guid journeyId, UpdateJourneyRequest request, CancellationToken ct = default);

    Task<DagValidationResult> ValidateJourneyDagAsync(Guid journeyId, CancellationToken ct = default);

    DagValidationResult ValidateDag(CustomerLifecycleJourney journey);

    Task<EnrollmentResult> TriggerJourneyAsync(Guid journeyId, TriggerContactRequest request, CancellationToken ct = default);

    Task<AdvanceDueResult> AdvanceDueContactsAsync(Guid journeyId, CancellationToken ct = default);

    Task<IReadOnlyList<JourneyStatePointer>> GetActiveContactsAsync(Guid journeyId, int limit = 100, CancellationToken ct = default);

    Task<JourneyStatePointer?> GetContactStateAsync(Guid journeyId, string contactId, CancellationToken ct = default);

    Task<bool> PauseJourneyAsync(Guid journeyId, CancellationToken ct = default);

    Task<bool> ResumeJourneyAsync(Guid journeyId, CancellationToken ct = default);

    Task<JourneyMetricsSummary> GetMetricsAsync(Guid? journeyId = null, CancellationToken ct = default);
}

#endregion

#region Service Implementation

public class CustomerLifecycleJourneyService : ICustomerLifecycleJourneyService
{
    private readonly ConcurrentDictionary<Guid, CustomerLifecycleJourney> _journeys = new();
    private readonly ConcurrentDictionary<string, JourneyStatePointer> _pointers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, object> _locks = new(StringComparer.OrdinalIgnoreCase);

    public CustomerLifecycleJourneyService()
    {
        SeedLifecycleJourneys();
    }

    private static string PointerKey(Guid journeyId, string contactId) => $"{journeyId}:{contactId.Trim().ToLowerInvariant()}";

    private object GetLockObject(string key) => _locks.GetOrAdd(key, _ => new object());

    #region CRUD & Lifecycle Operations

    public Task<IReadOnlyList<CustomerLifecycleJourney>> GetJourneysAsync(
        Guid? tenantId = null,
        string? search = null,
        JourneyLifecycleStatus? status = null,
        CancellationToken ct = default)
    {
        var query = _journeys.Values.AsEnumerable();

        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            query = query.Where(j => j.TenantId == tenantId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(j => j.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(j =>
                j.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                j.Description.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        // Refresh dynamic metrics before returning
        var result = query.Select(j =>
        {
            RefreshJourneyMetrics(j);
            return j;
        }).OrderByDescending(j => j.UpdatedAtUtc).ToList();

        return Task.FromResult<IReadOnlyList<CustomerLifecycleJourney>>(result);
    }

    public Task<CustomerLifecycleJourney?> GetJourneyByIdAsync(Guid journeyId, CancellationToken ct = default)
    {
        if (_journeys.TryGetValue(journeyId, out var journey))
        {
            RefreshJourneyMetrics(journey);
            return Task.FromResult<CustomerLifecycleJourney?>(journey);
        }
        return Task.FromResult<CustomerLifecycleJourney?>(null);
    }

    public Task<CustomerLifecycleJourney> CreateJourneyAsync(CreateJourneyRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Journey name is required.", nameof(request));
        }

        var journey = new CustomerLifecycleJourney
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Status = request.Status ?? JourneyLifecycleStatus.Active,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Nodes = request.Nodes ?? new List<LifecycleJourneyNode>(),
            Edges = request.Edges ?? new List<LifecycleJourneyEdge>()
        };

        // Align edge list with NextNodeIds
        SyncEdgesAndNodes(journey);

        // Validate DAG topology
        var validation = ValidateDag(journey);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Invalid DAG topology: {string.Join("; ", validation.Errors)}");
        }

        _journeys[journey.Id] = journey;
        return Task.FromResult(journey);
    }

    public Task<CustomerLifecycleJourney> UpdateJourneyAsync(Guid journeyId, UpdateJourneyRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_journeys.TryGetValue(journeyId, out var journey))
        {
            throw new KeyNotFoundException($"Journey {journeyId} was not found.");
        }

        lock (GetLockObject($"journey:{journeyId}"))
        {
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                journey.Name = request.Name.Trim();
            }

            if (request.Description is not null)
            {
                journey.Description = request.Description.Trim();
            }

            if (request.Status.HasValue)
            {
                journey.Status = request.Status.Value;
            }

            if (request.Nodes is not null)
            {
                journey.Nodes = request.Nodes;
            }

            if (request.Edges is not null)
            {
                journey.Edges = request.Edges;
            }

            SyncEdgesAndNodes(journey);

            var validation = ValidateDag(journey);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException($"Invalid DAG topology: {string.Join("; ", validation.Errors)}");
            }

            journey.UpdatedAtUtc = DateTime.UtcNow;
            RefreshJourneyMetrics(journey);
        }

        return Task.FromResult(journey);
    }

    public Task<bool> PauseJourneyAsync(Guid journeyId, CancellationToken ct = default)
    {
        if (_journeys.TryGetValue(journeyId, out var journey))
        {
            lock (GetLockObject($"journey:{journeyId}"))
            {
                journey.Status = JourneyLifecycleStatus.Paused;
                journey.UpdatedAtUtc = DateTime.UtcNow;
            }
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> ResumeJourneyAsync(Guid journeyId, CancellationToken ct = default)
    {
        if (_journeys.TryGetValue(journeyId, out var journey))
        {
            lock (GetLockObject($"journey:{journeyId}"))
            {
                journey.Status = JourneyLifecycleStatus.Active;
                journey.UpdatedAtUtc = DateTime.UtcNow;
            }
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    #endregion

    #region DAG Validation & Topological Analysis

    public Task<DagValidationResult> ValidateJourneyDagAsync(Guid journeyId, CancellationToken ct = default)
    {
        if (!_journeys.TryGetValue(journeyId, out var journey))
        {
            throw new KeyNotFoundException($"Journey {journeyId} not found.");
        }

        return Task.FromResult(ValidateDag(journey));
    }

    public DagValidationResult ValidateDag(CustomerLifecycleJourney journey)
    {
        var result = new DagValidationResult();

        if (journey.Nodes.Count == 0)
        {
            result.IsValid = false;
            result.Errors.Add("Journey DAG must contain at least one node.");
            return result;
        }

        var nodeMap = journey.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

        // 1. Entry node verification
        var triggerNodes = journey.Nodes.Where(n => n.Type == LifecycleNodeType.Trigger).ToList();
        if (triggerNodes.Count == 0)
        {
            result.IsValid = false;
            result.Errors.Add("Journey must contain at least one TriggerNode entrypoint.");
        }

        // 2. Outgoing edge reference verification
        foreach (var node in journey.Nodes)
        {
            // Collect next nodes from NextNodeIds and configs
            var targets = new HashSet<string>(node.NextNodeIds, StringComparer.OrdinalIgnoreCase);
            if (node.ConditionConfig != null)
            {
                if (!string.IsNullOrEmpty(node.ConditionConfig.TrueNodeId))
                    targets.Add(node.ConditionConfig.TrueNodeId);
                if (!string.IsNullOrEmpty(node.ConditionConfig.FalseNodeId))
                    targets.Add(node.ConditionConfig.FalseNodeId);
            }

            foreach (var targetId in targets)
            {
                if (!nodeMap.ContainsKey(targetId))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Node '{node.Name}' ({node.Id}) references non-existent downstream target '{targetId}'.");
                }
            }

            // Check self-loop
            if (targets.Contains(node.Id))
            {
                result.IsValid = false;
                result.Errors.Add($"Self-loop detected on node '{node.Name}' ({node.Id}).");
            }
        }

        // 3. Cycle Detection using DFS 3-state graph coloring (0: Unvisited, 1: Visiting, 2: Visited)
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in journey.Nodes)
        {
            state[node.Id] = 0;
        }

        var cycleFound = false;
        var path = new List<string>();

        bool DfsHasCycle(string currentId)
        {
            state[currentId] = 1; // Visiting
            path.Add(currentId);

            if (nodeMap.TryGetValue(currentId, out var current))
            {
                var downstream = GetDirectDownstreamNodeIds(current);
                foreach (var nextId in downstream)
                {
                    if (!nodeMap.ContainsKey(nextId)) continue;

                    if (state[nextId] == 1) // Cycle detected!
                    {
                        var cycleStartIndex = path.IndexOf(nextId);
                        var cycleNodes = path.Skip(cycleStartIndex).Append(nextId).Select(id => nodeMap.TryGetValue(id, out var n) ? n.Name : id);
                        result.Errors.Add($"Cycle detected: {string.Join(" -> ", cycleNodes)}");
                        cycleFound = true;
                        return true;
                    }

                    if (state[nextId] == 0)
                    {
                        if (DfsHasCycle(nextId))
                        {
                            return true;
                        }
                    }
                }
            }

            path.RemoveAt(path.Count - 1);
            state[currentId] = 2; // Visited
            return false;
        }

        foreach (var node in journey.Nodes)
        {
            if (state[node.Id] == 0)
            {
                if (DfsHasCycle(node.Id))
                {
                    break;
                }
            }
        }

        // 4. Compute topological order if acyclic
        if (!cycleFound && result.Errors.Count == 0)
        {
            var topoOrder = ComputeTopologicalOrder(journey.Nodes, nodeMap);
            result.TopologicalOrder = topoOrder;
            result.IsValid = true;
        }
        else
        {
            result.IsValid = false;
        }

        return result;
    }

    private static List<string> GetDirectDownstreamNodeIds(LifecycleJourneyNode node)
    {
        var targets = new HashSet<string>(node.NextNodeIds, StringComparer.OrdinalIgnoreCase);
        if (node.ConditionConfig != null)
        {
            if (!string.IsNullOrEmpty(node.ConditionConfig.TrueNodeId))
                targets.Add(node.ConditionConfig.TrueNodeId);
            if (!string.IsNullOrEmpty(node.ConditionConfig.FalseNodeId))
                targets.Add(node.ConditionConfig.FalseNodeId);
        }
        return targets.ToList();
    }

    private static List<string> ComputeTopologicalOrder(
        List<LifecycleJourneyNode> nodes,
        Dictionary<string, LifecycleJourneyNode> nodeMap)
    {
        var inDegree = nodes.ToDictionary(n => n.Id, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            foreach (var target in GetDirectDownstreamNodeIds(node))
            {
                if (inDegree.ContainsKey(target))
                {
                    inDegree[target]++;
                }
            }
        }

        var queue = new Queue<string>(nodes.Where(n => inDegree[n.Id] == 0).Select(n => n.Id));
        var order = new List<string>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            order.Add(current);

            if (nodeMap.TryGetValue(current, out var node))
            {
                foreach (var target in GetDirectDownstreamNodeIds(node))
                {
                    if (inDegree.ContainsKey(target))
                    {
                        inDegree[target]--;
                        if (inDegree[target] == 0)
                        {
                            queue.Enqueue(target);
                        }
                    }
                }
            }
        }

        return order;
    }

    #endregion

    #region Trigger & Enrollment

    public Task<EnrollmentResult> TriggerJourneyAsync(
        Guid journeyId,
        TriggerContactRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ContactId))
        {
            return Task.FromResult(new EnrollmentResult
            {
                Success = false,
                Message = "ContactId is required."
            });
        }

        if (!_journeys.TryGetValue(journeyId, out var journey))
        {
            return Task.FromResult(new EnrollmentResult
            {
                Success = false,
                Message = $"Journey {journeyId} not found."
            });
        }

        if (journey.Status == JourneyLifecycleStatus.Paused)
        {
            return Task.FromResult(new EnrollmentResult
            {
                Success = false,
                Message = $"Journey '{journey.Name}' is currently paused. New contact enrollment is suspended."
            });
        }

        var contactId = request.ContactId.Trim();
        var pointerKey = PointerKey(journeyId, contactId);
        var lockObj = GetLockObject(pointerKey);

        lock (lockObj)
        {
            // Check existing pointer
            if (_pointers.TryGetValue(pointerKey, out var existingPointer))
            {
                if (!request.ForceReEnroll && existingPointer.Status == ContactJourneyStatus.InProgress)
                {
                    return Task.FromResult(new EnrollmentResult
                    {
                        Success = false,
                        Message = $"Contact '{contactId}' is already actively enrolled in this journey (Step: {existingPointer.CurrentNodeId}).",
                        StatePointer = existingPointer,
                        StepsExecutedImmediately = 0
                    });
                }
            }

            // Find matching trigger node
            var triggerNode = FindMatchingTriggerNode(journey, request.EventName, request.SegmentName);
            if (triggerNode == null)
            {
                return Task.FromResult(new EnrollmentResult
                {
                    Success = false,
                    Message = $"No matching TriggerNode found for event '{request.EventName}' or segment '{request.SegmentName}' in journey '{journey.Name}'."
                });
            }

            var nodeMap = journey.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

            // Create new atomic state pointer
            var pointer = new JourneyStatePointer
            {
                Id = Guid.NewGuid(),
                JourneyId = journeyId,
                ContactId = contactId,
                CurrentNodeId = triggerNode.Id,
                Status = ContactJourneyStatus.InProgress,
                EnrolledAtUtc = DateTime.UtcNow,
                LastUpdatedAtUtc = DateTime.UtcNow,
                ContactProperties = request.ContactProperties != null
                    ? new Dictionary<string, object>(request.ContactProperties, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            };

            // Record initial Trigger breadcrumb
            pointer.Breadcrumbs.Add(new JourneyBreadcrumb
            {
                NodeId = triggerNode.Id,
                NodeName = triggerNode.Name,
                NodeType = LifecycleNodeType.Trigger,
                EnteredAtUtc = DateTime.UtcNow,
                ExitedAtUtc = DateTime.UtcNow,
                Outcome = $"Enrolled via TriggerNode [{triggerNode.Name}] (Event: {request.EventName ?? "N/A"}, Segment: {request.SegmentName ?? "N/A"})"
            });

            _pointers[pointerKey] = pointer;
            journey.TotalEnrolled++;

            // Execute immediate downstream chain until a DelayNode or end of journey
            var steps = ExecuteImmediateChain(journey, pointer, triggerNode, nodeMap);

            RefreshJourneyMetrics(journey);

            return Task.FromResult(new EnrollmentResult
            {
                Success = true,
                Message = $"Contact '{contactId}' enrolled successfully into '{journey.Name}'. Executed {steps} steps immediately.",
                StatePointer = pointer,
                StepsExecutedImmediately = steps
            });
        }
    }

    private static LifecycleJourneyNode? FindMatchingTriggerNode(
        CustomerLifecycleJourney journey,
        string? eventName,
        string? segmentName)
    {
        var triggers = journey.Nodes.Where(n => n.Type == LifecycleNodeType.Trigger).ToList();
        if (triggers.Count == 0) return null;

        // 1. Try exact event match
        if (!string.IsNullOrEmpty(eventName))
        {
            var match = triggers.FirstOrDefault(t =>
                t.TriggerConfig?.Type == TriggerType.Event &&
                string.Equals(t.TriggerConfig?.EventName, eventName, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        // 2. Try exact segment match
        if (!string.IsNullOrEmpty(segmentName))
        {
            var match = triggers.FirstOrDefault(t =>
                t.TriggerConfig?.Type == TriggerType.Segment &&
                string.Equals(t.TriggerConfig?.SegmentName, segmentName, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        // 3. Fallback to first trigger node
        return triggers[0];
    }

    #endregion

    #region Temporal Sweep & Advance Engine

    public Task<AdvanceDueResult> AdvanceDueContactsAsync(Guid journeyId, CancellationToken ct = default)
    {
        if (!_journeys.TryGetValue(journeyId, out var journey))
        {
            throw new KeyNotFoundException($"Journey {journeyId} not found.");
        }

        var result = new AdvanceDueResult { JourneyId = journeyId };

        if (journey.Status == JourneyLifecycleStatus.Paused)
        {
            result.ExecutionSummaries.Add($"Journey '{journey.Name}' is paused. Temporal sweep skipped.");
            return Task.FromResult(result);
        }

        var now = DateTime.UtcNow;
        var nodeMap = journey.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

        // Find contacts with due delays
        var duePointers = _pointers.Values
            .Where(p => p.JourneyId == journeyId &&
                        p.Status == ContactJourneyStatus.InProgress &&
                        p.ScheduledResumeAtUtc.HasValue &&
                        p.ScheduledResumeAtUtc.Value <= now)
            .ToList();

        result.DueContactsFound = duePointers.Count;

        foreach (var pointer in duePointers)
        {
            var lockObj = GetLockObject(PointerKey(journeyId, pointer.ContactId));
            lock (lockObj)
            {
                // Re-verify under lock
                if (pointer.Status != ContactJourneyStatus.InProgress ||
                    !pointer.ScheduledResumeAtUtc.HasValue ||
                    pointer.ScheduledResumeAtUtc.Value > DateTime.UtcNow)
                {
                    continue;
                }

                try
                {
                    if (!nodeMap.TryGetValue(pointer.CurrentNodeId, out var currentDelayNode))
                    {
                        pointer.Status = ContactJourneyStatus.Exited;
                        pointer.LastUpdatedAtUtc = DateTime.UtcNow;
                        result.FailedCount++;
                        result.ExecutionSummaries.Add($"Contact '{pointer.ContactId}' delay node '{pointer.CurrentNodeId}' missing from graph. Marked Exited.");
                        continue;
                    }

                    // Complete delay breadcrumb
                    var activeDelayBreadcrumb = pointer.Breadcrumbs.LastOrDefault(b => b.NodeId == currentDelayNode.Id && b.ExitedAtUtc == null);
                    if (activeDelayBreadcrumb != null)
                    {
                        activeDelayBreadcrumb.ExitedAtUtc = DateTime.UtcNow;
                        activeDelayBreadcrumb.Outcome = $"Delay period completed ({currentDelayNode.DelayConfig?.Value} {currentDelayNode.DelayConfig?.Unit}). Advancing.";
                    }

                    pointer.ScheduledResumeAtUtc = null;
                    pointer.LastUpdatedAtUtc = DateTime.UtcNow;

                    // Progress to downstream nodes
                    var steps = ExecuteImmediateChain(journey, pointer, currentDelayNode, nodeMap);
                    result.AdvancedCount++;
                    if (pointer.Status == ContactJourneyStatus.Completed)
                    {
                        result.CompletedCount++;
                    }

                    result.ExecutionSummaries.Add($"Advanced contact '{pointer.ContactId}' past delay '{currentDelayNode.Name}' through {steps} steps (Status: {pointer.Status}).");
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.ExecutionSummaries.Add($"Error advancing contact '{pointer.ContactId}': {ex.Message}");
                }
            }
        }

        RefreshJourneyMetrics(journey);
        return Task.FromResult(result);
    }

    #endregion

    #region Immediate Chain Traversal & Execution

    private int ExecuteImmediateChain(
        CustomerLifecycleJourney journey,
        JourneyStatePointer pointer,
        LifecycleJourneyNode fromNode,
        Dictionary<string, LifecycleJourneyNode> nodeMap)
    {
        var stepsExecuted = 0;
        var currentNode = fromNode;

        while (true)
        {
            var nextNodeId = DetermineNextNodeId(journey, pointer, currentNode);

            if (string.IsNullOrEmpty(nextNodeId) || !nodeMap.TryGetValue(nextNodeId, out var nextNode))
            {
                // End of journey reached!
                pointer.Status = ContactJourneyStatus.Completed;
                pointer.CompletedAtUtc = DateTime.UtcNow;
                pointer.LastUpdatedAtUtc = DateTime.UtcNow;
                pointer.Breadcrumbs.Add(new JourneyBreadcrumb
                {
                    NodeId = "end",
                    NodeName = "Journey Completed",
                    NodeType = LifecycleNodeType.Action,
                    EnteredAtUtc = DateTime.UtcNow,
                    ExitedAtUtc = DateTime.UtcNow,
                    Outcome = "Contact successfully traversed all journey steps."
                });
                break;
            }

            pointer.CurrentNodeId = nextNode.Id;
            pointer.LastUpdatedAtUtc = DateTime.UtcNow;
            stepsExecuted++;

            if (nextNode.Type == LifecycleNodeType.Delay)
            {
                // Schedule pause and stop immediate traversal
                var delayConfig = nextNode.DelayConfig ?? new DelayNodeConfig();
                var resumeAt = delayConfig.CalculateResumeAt(DateTime.UtcNow);
                pointer.ScheduledResumeAtUtc = resumeAt;

                pointer.Breadcrumbs.Add(new JourneyBreadcrumb
                {
                    NodeId = nextNode.Id,
                    NodeName = nextNode.Name,
                    NodeType = LifecycleNodeType.Delay,
                    EnteredAtUtc = DateTime.UtcNow,
                    ExitedAtUtc = null, // In progress
                    Outcome = $"Delaying for {delayConfig.Value} {delayConfig.Unit}. Scheduled resume at {resumeAt:O}."
                });

                break;
            }
            else if (nextNode.Type == LifecycleNodeType.Condition)
            {
                // Evaluate condition
                var condResult = EvaluateCondition(nextNode.ConditionConfig, pointer.ContactProperties);
                pointer.Breadcrumbs.Add(new JourneyBreadcrumb
                {
                    NodeId = nextNode.Id,
                    NodeName = nextNode.Name,
                    NodeType = LifecycleNodeType.Condition,
                    EnteredAtUtc = DateTime.UtcNow,
                    ExitedAtUtc = DateTime.UtcNow,
                    Outcome = $"Condition [{nextNode.ConditionConfig?.Property} {nextNode.ConditionConfig?.Operator} {nextNode.ConditionConfig?.Value}] evaluated to '{condResult}'."
                });

                // Set next node based on condition result
                currentNode = nextNode;
            }
            else if (nextNode.Type == LifecycleNodeType.Action)
            {
                // Outbound delivery with failover
                var actionConfig = nextNode.ActionConfig ?? new ActionNodeConfig();
                var delivery = DispatchOutboundAction(journey, pointer, nextNode, actionConfig);

                pointer.DeliveryLogs.Add(delivery);

                pointer.Breadcrumbs.Add(new JourneyBreadcrumb
                {
                    NodeId = nextNode.Id,
                    NodeName = nextNode.Name,
                    NodeType = LifecycleNodeType.Action,
                    EnteredAtUtc = DateTime.UtcNow,
                    ExitedAtUtc = DateTime.UtcNow,
                    Outcome = delivery.Success
                        ? (delivery.WasFallback
                            ? $"Dispatched via Fallback [{delivery.Channel}] (Original: {delivery.OriginalChannel}). Details: {delivery.Details}"
                            : $"Dispatched via Primary [{delivery.Channel}]. Details: {delivery.Details}")
                        : $"Action failed on [{delivery.Channel}]. Error: {delivery.ErrorMessage}"
                });

                currentNode = nextNode;
            }
            else
            {
                // Trigger or other node type encountered downstream
                pointer.Breadcrumbs.Add(new JourneyBreadcrumb
                {
                    NodeId = nextNode.Id,
                    NodeName = nextNode.Name,
                    NodeType = nextNode.Type,
                    EnteredAtUtc = DateTime.UtcNow,
                    ExitedAtUtc = DateTime.UtcNow,
                    Outcome = $"Passed through node {nextNode.Name}."
                });

                currentNode = nextNode;
            }
        }

        return stepsExecuted;
    }

    private static string? DetermineNextNodeId(
        CustomerLifecycleJourney journey,
        JourneyStatePointer pointer,
        LifecycleJourneyNode currentNode)
    {
        if (currentNode.Type == LifecycleNodeType.Condition && currentNode.ConditionConfig != null)
        {
            var condResult = EvaluateCondition(currentNode.ConditionConfig, pointer.ContactProperties);
            return condResult
                ? currentNode.ConditionConfig.TrueNodeId
                : currentNode.ConditionConfig.FalseNodeId;
        }

        if (currentNode.NextNodeIds.Count > 0)
        {
            return currentNode.NextNodeIds[0];
        }

        // Look in edges if NextNodeIds was empty
        var edge = journey.Edges.FirstOrDefault(e => string.Equals(e.SourceNodeId, currentNode.Id, StringComparison.OrdinalIgnoreCase));
        return edge?.TargetNodeId;
    }

    #endregion

    #region Condition Evaluation

    public static bool EvaluateCondition(ConditionNodeConfig? config, Dictionary<string, object> properties)
    {
        if (config == null || string.IsNullOrWhiteSpace(config.Property))
        {
            return true;
        }

        // Try exact key or case-insensitive match
        object? rawVal = null;
        if (properties.TryGetValue(config.Property, out var foundVal))
        {
            rawVal = foundVal;
        }
        else
        {
            var entry = properties.FirstOrDefault(kv => string.Equals(kv.Key, config.Property, StringComparison.OrdinalIgnoreCase));
            rawVal = entry.Value;
        }

        var propStr = rawVal?.ToString()?.Trim() ?? string.Empty;
        var targetVal = config.Value?.Trim() ?? string.Empty;
        var op = config.Operator?.Trim() ?? "Equals";

        switch (op.ToLowerInvariant())
        {
            case "istrue":
            case "true":
                return bool.TryParse(propStr, out var bTrue) ? bTrue : string.Equals(propStr, "1", StringComparison.OrdinalIgnoreCase);

            case "isfalse":
            case "false":
                return bool.TryParse(propStr, out var bFalse) ? !bFalse : !string.Equals(propStr, "1", StringComparison.OrdinalIgnoreCase);

            case "equals":
            case "eq":
            case "==":
                if (double.TryParse(propStr, out var d1) && double.TryParse(targetVal, out var d2))
                {
                    return Math.Abs(d1 - d2) < 0.0001;
                }
                if (bool.TryParse(propStr, out var bp) && bool.TryParse(targetVal, out var bt))
                {
                    return bp == bt;
                }
                return string.Equals(propStr, targetVal, StringComparison.OrdinalIgnoreCase);

            case "notequals":
            case "neq":
            case "!=":
                if (double.TryParse(propStr, out var nd1) && double.TryParse(targetVal, out var nd2))
                {
                    return Math.Abs(nd1 - nd2) >= 0.0001;
                }
                return !string.Equals(propStr, targetVal, StringComparison.OrdinalIgnoreCase);

            case "greaterthan":
            case "gt":
            case ">":
                if (double.TryParse(propStr, out var g1) && double.TryParse(targetVal, out var g2))
                {
                    return g1 > g2;
                }
                return false;

            case "greaterthanorequal":
            case "gte":
            case ">=":
                if (double.TryParse(propStr, out var ge1) && double.TryParse(targetVal, out var ge2))
                {
                    return ge1 >= ge2;
                }
                return false;

            case "lessthan":
            case "lt":
            case "<":
                if (double.TryParse(propStr, out var l1) && double.TryParse(targetVal, out var l2))
                {
                    return l1 < l2;
                }
                return false;

            case "lessthanorequal":
            case "lte":
            case "<=":
                if (double.TryParse(propStr, out var le1) && double.TryParse(targetVal, out var le2))
                {
                    return le1 <= le2;
                }
                return false;

            case "contains":
                return propStr.Contains(targetVal, StringComparison.OrdinalIgnoreCase);

            default:
                return string.Equals(propStr, targetVal, StringComparison.OrdinalIgnoreCase);
        }
    }

    #endregion

    #region Cross-Channel Delivery & Failover

    private JourneyDeliveryLog DispatchOutboundAction(
        CustomerLifecycleJourney journey,
        JourneyStatePointer pointer,
        LifecycleJourneyNode node,
        ActionNodeConfig config)
    {
        // Check if failure simulation is triggered (either via node config or contact test property)
        var shouldFailPrimary = config.SimulateChannelFailure;
        if (pointer.ContactProperties.TryGetValue("simulate_channel_failure", out var simVal))
        {
            if (bool.TryParse(simVal?.ToString(), out var bSim) && bSim) shouldFailPrimary = true;
        }
        if (pointer.ContactProperties.TryGetValue("fail_whatsapp", out var fwVal))
        {
            if (config.Channel == ActionChannel.WhatsApp && bool.TryParse(fwVal?.ToString(), out var bFw) && bFw)
                shouldFailPrimary = true;
        }

        if (!shouldFailPrimary)
        {
            // Primary channel success
            return new JourneyDeliveryLog
            {
                Id = Guid.NewGuid(),
                NodeId = node.Id,
                Channel = config.Channel,
                Success = true,
                WasFallback = false,
                TimestampUtc = DateTime.UtcNow,
                Details = $"Successfully dispatched via {config.Channel}. Subject: '{config.Subject}'."
            };
        }

        // Primary channel failed! Attempt cross-channel fallback
        if (config.FallbackChannel.HasValue)
        {
            var fallback = config.FallbackChannel.Value;
            journey.FallbackRecoveryCount++;

            return new JourneyDeliveryLog
            {
                Id = Guid.NewGuid(),
                NodeId = node.Id,
                Channel = fallback,
                OriginalChannel = config.Channel,
                Success = true,
                WasFallback = true,
                TimestampUtc = DateTime.UtcNow,
                Details = $"Primary channel {config.Channel} delivery rejected/bounced. Cross-channel failover routed to {fallback} successfully.",
                ErrorMessage = $"Simulated/network bounce on {config.Channel}."
            };
        }

        // Failure with no fallback configured
        return new JourneyDeliveryLog
        {
            Id = Guid.NewGuid(),
            NodeId = node.Id,
            Channel = config.Channel,
            Success = false,
            WasFallback = false,
            TimestampUtc = DateTime.UtcNow,
            ErrorMessage = $"Delivery on {config.Channel} failed and no fallback channel was defined."
        };
    }

    #endregion

    #region State Pointers & Metrics

    public Task<IReadOnlyList<JourneyStatePointer>> GetActiveContactsAsync(
        Guid journeyId,
        int limit = 100,
        CancellationToken ct = default)
    {
        var active = _pointers.Values
            .Where(p => p.JourneyId == journeyId)
            .OrderByDescending(p => p.LastUpdatedAtUtc)
            .Take(Math.Clamp(limit, 1, 500))
            .ToList();

        return Task.FromResult<IReadOnlyList<JourneyStatePointer>>(active);
    }

    public Task<JourneyStatePointer?> GetContactStateAsync(Guid journeyId, string contactId, CancellationToken ct = default)
    {
        var key = PointerKey(journeyId, contactId);
        _pointers.TryGetValue(key, out var pointer);
        return Task.FromResult(pointer);
    }

    public Task<JourneyMetricsSummary> GetMetricsAsync(Guid? journeyId = null, CancellationToken ct = default)
    {
        var allJourneys = _journeys.Values.ToList();
        var relevantJourneys = journeyId.HasValue
            ? allJourneys.Where(j => j.Id == journeyId.Value).ToList()
            : allJourneys;

        var jIds = new HashSet<Guid>(relevantJourneys.Select(j => j.Id));
        var pointers = _pointers.Values.Where(p => jIds.Contains(p.JourneyId)).ToList();

        var totalEnrolled = relevantJourneys.Sum(j => j.TotalEnrolled);
        var totalCompleted = relevantJourneys.Sum(j => j.TotalCompleted);
        var activeInFlight = pointers.Count(p => p.Status == ContactJourneyStatus.InProgress);
        var fallbackRecoveries = relevantJourneys.Sum(j => j.FallbackRecoveryCount);

        var stepDistribution = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in pointers.Where(p => p.Status == ContactJourneyStatus.InProgress))
        {
            stepDistribution[p.CurrentNodeId] = stepDistribution.GetValueOrDefault(p.CurrentNodeId, 0) + 1;
        }

        var summary = new JourneyMetricsSummary
        {
            TotalJourneys = relevantJourneys.Count,
            ActiveJourneys = relevantJourneys.Count(j => j.Status == JourneyLifecycleStatus.Active),
            TotalEnrolledContacts = totalEnrolled,
            ActiveInFlightContacts = activeInFlight,
            TotalCompletedContacts = totalCompleted,
            TotalFallbackRecoveries = fallbackRecoveries,
            OverallConversionRate = totalEnrolled > 0
                ? Math.Round((double)totalCompleted / totalEnrolled * 100, 1)
                : 0.0,
            ContactsPerStepDistribution = stepDistribution
        };

        return Task.FromResult(summary);
    }

    private void RefreshJourneyMetrics(CustomerLifecycleJourney journey)
    {
        var pointers = _pointers.Values.Where(p => p.JourneyId == journey.Id).ToList();
        journey.ActiveCount = pointers.Count(p => p.Status == ContactJourneyStatus.InProgress);
        journey.TotalCompleted = pointers.Count(p => p.Status == ContactJourneyStatus.Completed);
        journey.TotalExited = pointers.Count(p => p.Status == ContactJourneyStatus.Exited);
        if (pointers.Count > journey.TotalEnrolled)
        {
            journey.TotalEnrolled = pointers.Count;
        }
    }

    private static void SyncEdgesAndNodes(CustomerLifecycleJourney journey)
    {
        var edgeSet = new HashSet<string>();
        foreach (var edge in journey.Edges)
        {
            edgeSet.Add($"{edge.SourceNodeId}->{edge.TargetNodeId}");
        }

        // Mirror NextNodeIds into edges if missing
        foreach (var node in journey.Nodes)
        {
            foreach (var nextId in node.NextNodeIds)
            {
                var key = $"{node.Id}->{nextId}";
                if (!edgeSet.Contains(key))
                {
                    journey.Edges.Add(new LifecycleJourneyEdge
                    {
                        SourceNodeId = node.Id,
                        TargetNodeId = nextId,
                        Label = "Next"
                    });
                    edgeSet.Add(key);
                }
            }

            if (node.ConditionConfig != null)
            {
                if (!string.IsNullOrEmpty(node.ConditionConfig.TrueNodeId))
                {
                    var trueKey = $"{node.Id}->{node.ConditionConfig.TrueNodeId}";
                    if (!edgeSet.Contains(trueKey))
                    {
                        journey.Edges.Add(new LifecycleJourneyEdge
                        {
                            SourceNodeId = node.Id,
                            TargetNodeId = node.ConditionConfig.TrueNodeId,
                            Label = "True"
                        });
                        edgeSet.Add(trueKey);
                    }
                }
                if (!string.IsNullOrEmpty(node.ConditionConfig.FalseNodeId))
                {
                    var falseKey = $"{node.Id}->{node.ConditionConfig.FalseNodeId}";
                    if (!edgeSet.Contains(falseKey))
                    {
                        journey.Edges.Add(new LifecycleJourneyEdge
                        {
                            SourceNodeId = node.Id,
                            TargetNodeId = node.ConditionConfig.FalseNodeId,
                            Label = "False"
                        });
                        edgeSet.Add(falseKey);
                    }
                }
            }
        }
    }

    #endregion

    #region Pre-Seeded Lifecycle Journeys

    private void SeedLifecycleJourneys()
    {
        var defaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        // 1. SaaS Onboarding & Activation Journey
        var saasJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555551");
        var nTrigger = new LifecycleJourneyNode
        {
            Id = "saas_trg_1",
            Name = "User Sign-up Event",
            Type = LifecycleNodeType.Trigger,
            Description = "Enrolls contact upon user registration",
            PositionX = 100,
            PositionY = 150,
            NextNodeIds = new() { "saas_act_welcome" },
            TriggerConfig = new TriggerNodeConfig
            {
                Type = TriggerType.Event,
                EventName = "user_signed_up"
            }
        };

        var nWelcome = new LifecycleJourneyNode
        {
            Id = "saas_act_welcome",
            Name = "Welcome Email & Onboarding Checklist",
            Type = LifecycleNodeType.Action,
            Description = "Delivers initial platform credentials and activation guide",
            PositionX = 350,
            PositionY = 150,
            NextNodeIds = new() { "saas_dly_wait1d" },
            ActionConfig = new ActionNodeConfig
            {
                Channel = ActionChannel.Email,
                Subject = "Welcome to SGrowing! Here is your quickstart activation guide",
                MessageBody = "Hi there! Welcome to SGrowing. Log in to your workspace and complete step 1.",
                FallbackChannel = ActionChannel.Sms
            }
        };

        var nWait1D = new LifecycleJourneyNode
        {
            Id = "saas_dly_wait1d",
            Name = "Wait 1 Day for Activation",
            Type = LifecycleNodeType.Delay,
            Description = "Gives the customer 24 hours to complete initial setup",
            PositionX = 600,
            PositionY = 150,
            NextNodeIds = new() { "saas_cnd_activated" },
            DelayConfig = new DelayNodeConfig
            {
                Value = 1,
                Unit = DelayUnit.Days
            }
        };

        var nCheckActivated = new LifecycleJourneyNode
        {
            Id = "saas_cnd_activated",
            Name = "Has Activated Workspace?",
            Type = LifecycleNodeType.Condition,
            Description = "Branches based on workspace activation status",
            PositionX = 850,
            PositionY = 150,
            ConditionConfig = new ConditionNodeConfig
            {
                Property = "has_activated",
                Operator = "IsTrue",
                Value = "true",
                TrueNodeId = "saas_act_celebrate",
                FalseNodeId = "saas_act_nudge"
            }
        };

        var nCelebrate = new LifecycleJourneyNode
        {
            Id = "saas_act_celebrate",
            Name = "Milestone Celebration & VIP Webinar Invite",
            Type = LifecycleNodeType.Action,
            Description = "Sends celebration WhatsApp with email failover",
            PositionX = 1100,
            PositionY = 80,
            NextNodeIds = new(),
            ActionConfig = new ActionNodeConfig
            {
                Channel = ActionChannel.WhatsApp,
                FallbackChannel = ActionChannel.Email,
                Subject = "You are all set! Exclusive VIP Webinar Invite",
                MessageBody = "Awesome job activating! Join our live weekly masterclass."
            }
        };

        var nNudge = new LifecycleJourneyNode
        {
            Id = "saas_act_nudge",
            Name = "Activation WhatsApp Nudge (Email Failover)",
            Type = LifecycleNodeType.Action,
            Description = "Friendly reminder message with fallback routing",
            PositionX = 1100,
            PositionY = 240,
            NextNodeIds = new(),
            ActionConfig = new ActionNodeConfig
            {
                Channel = ActionChannel.WhatsApp,
                FallbackChannel = ActionChannel.Email,
                Subject = "Need help setting up your SGrowing account?",
                MessageBody = "Hey! We noticed you haven't finished workspace activation. Let us know if you need help!"
            }
        };

        var saasJourney = new CustomerLifecycleJourney
        {
            Id = saasJourneyId,
            TenantId = defaultTenantId,
            Name = "SaaS Onboarding & Activation",
            Description = "Automated onboarding flow with activation condition checks, temporal delays, and cross-channel failover.",
            Status = JourneyLifecycleStatus.Active,
            Nodes = new() { nTrigger, nWelcome, nWait1D, nCheckActivated, nCelebrate, nNudge }
        };
        SyncEdgesAndNodes(saasJourney);
        _journeys[saasJourney.Id] = saasJourney;

        // 2. E-Commerce VIP Winback
        var ecomJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555552");
        var eTrigger = new LifecycleJourneyNode
        {
            Id = "ecom_trg_1",
            Name = "VIP Cart Abandoned / Inactive Segment",
            Type = LifecycleNodeType.Trigger,
            Description = "Triggered when VIP segment customer abandons checkout",
            PositionX = 100,
            PositionY = 150,
            NextNodeIds = new() { "ecom_dly_2h" },
            TriggerConfig = new TriggerNodeConfig
            {
                Type = TriggerType.Segment,
                SegmentName = "vip_customers"
            }
        };

        var eWait2H = new LifecycleJourneyNode
        {
            Id = "ecom_dly_2h",
            Name = "Wait 2 Hours",
            Type = LifecycleNodeType.Delay,
            Description = "Temporal buffer before dispatching cart recovery voucher",
            PositionX = 350,
            PositionY = 150,
            NextNodeIds = new() { "ecom_act_voucher" },
            DelayConfig = new DelayNodeConfig
            {
                Value = 2,
                Unit = DelayUnit.Hours
            }
        };

        var eVoucher = new LifecycleJourneyNode
        {
            Id = "ecom_act_voucher",
            Name = "WhatsApp Exclusive 15% VIP Discount Voucher",
            Type = LifecycleNodeType.Action,
            Description = "Dispatches WhatsApp voucher with SMS failover",
            PositionX = 600,
            PositionY = 150,
            NextNodeIds = new() { "ecom_dly_24h" },
            ActionConfig = new ActionNodeConfig
            {
                Channel = ActionChannel.WhatsApp,
                FallbackChannel = ActionChannel.Sms,
                Subject = "Exclusive 15% VIP Voucher Inside",
                MessageBody = "Here is your exclusive 15% VIP voucher: VIP15. Valid for the next 24 hours!"
            }
        };

        var eWait24H = new LifecycleJourneyNode
        {
            Id = "ecom_dly_24h",
            Name = "Wait 24 Hours for Purchase",
            Type = LifecycleNodeType.Delay,
            Description = "Grace period to check purchase completion",
            PositionX = 850,
            PositionY = 150,
            NextNodeIds = new() { "ecom_cnd_purchased" },
            DelayConfig = new DelayNodeConfig
            {
                Value = 24,
                Unit = DelayUnit.Hours
            }
        };

        var eCheckPurchased = new LifecycleJourneyNode
        {
            Id = "ecom_cnd_purchased",
            Name = "Has Completed Purchase?",
            Type = LifecycleNodeType.Condition,
            Description = "Evaluates has_purchased property",
            PositionX = 1100,
            PositionY = 150,
            ConditionConfig = new ConditionNodeConfig
            {
                Property = "has_purchased",
                Operator = "IsTrue",
                Value = "true",
                TrueNodeId = "ecom_act_loyalty",
                FalseNodeId = "ecom_act_final_email"
            }
        };

        var eLoyalty = new LifecycleJourneyNode
        {
            Id = "ecom_act_loyalty",
            Name = "Reward VIP Loyalty Points",
            Type = LifecycleNodeType.Action,
            Description = "Rewards VIP points after order verification",
            PositionX = 1350,
            PositionY = 80,
            NextNodeIds = new(),
            ActionConfig = new ActionNodeConfig
            {
                Channel = ActionChannel.WhatsApp,
                FallbackChannel = ActionChannel.Email,
                Subject = "500 VIP Loyalty Points Added!",
                MessageBody = "Thank you for your purchase! We have credited 500 points to your VIP tier."
            }
        };

        var eFinalEmail = new LifecycleJourneyNode
        {
            Id = "ecom_act_final_email",
            Name = "Final Notice Email (20% Flash Coupon)",
            Type = LifecycleNodeType.Action,
            Description = "Last-chance email push",
            PositionX = 1350,
            PositionY = 240,
            NextNodeIds = new(),
            ActionConfig = new ActionNodeConfig
            {
                Channel = ActionChannel.Email,
                FallbackChannel = ActionChannel.Push,
                Subject = "Last Chance: Your cart is expiring tonight",
                MessageBody = "Use code FLASH20 before midnight to complete your VIP order."
            }
        };

        var ecomJourney = new CustomerLifecycleJourney
        {
            Id = ecomJourneyId,
            TenantId = defaultTenantId,
            Name = "E-Commerce VIP Winback",
            Description = "High-touch recovery sequence combining WhatsApp vouchers, delayed checks, and loyalty point rewards.",
            Status = JourneyLifecycleStatus.Active,
            Nodes = new() { eTrigger, eWait2H, eVoucher, eWait24H, eCheckPurchased, eLoyalty, eFinalEmail }
        };
        SyncEdgesAndNodes(ecomJourney);
        _journeys[ecomJourney.Id] = ecomJourney;

        // 3. Enterprise Lead Nurture
        var entJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555553");
        var entTrigger = new LifecycleJourneyNode
        {
            Id = "ent_trg_1",
            Name = "High-Intent Demo Requested",
            Type = LifecycleNodeType.Trigger,
            Description = "Inbound lead requests enterprise product demonstration",
            PositionX = 100,
            PositionY = 150,
            NextNodeIds = new() { "ent_act_crm" },
            TriggerConfig = new TriggerNodeConfig
            {
                Type = TriggerType.Event,
                EventName = "demo_requested"
            }
        };

        var entCrm = new LifecycleJourneyNode
        {
            Id = "ent_act_crm",
            Name = "Create CRM SDR Priority Task",
            Type = LifecycleNodeType.Action,
            Description = "Creates instant follow-up task in CRM",
            PositionX = 350,
            PositionY = 150,
            NextNodeIds = new() { "ent_act_booking" },
            ActionConfig = new ActionNodeConfig
            {
                Channel = ActionChannel.CrmTask,
                CrmTaskTitle = "Priority Inbound Demo: Review lead and prepare deck",
                CrmAssignee = "Enterprise SDR Team"
            }
        };

        var entBooking = new LifecycleJourneyNode
        {
            Id = "ent_act_booking",
            Name = "Send Instant VIP Calendar Booking Link",
            Type = LifecycleNodeType.Action,
            Description = "Dispatches WhatsApp link with email fallback",
            PositionX = 600,
            PositionY = 150,
            NextNodeIds = new() { "ent_dly_3d" },
            ActionConfig = new ActionNodeConfig
            {
                Channel = ActionChannel.WhatsApp,
                FallbackChannel = ActionChannel.Email,
                Subject = "Book your priority Enterprise demo slot",
                MessageBody = "Thanks for your interest! Pick a convenient slot on our executive demo calendar."
            }
        };

        var entWait3D = new LifecycleJourneyNode
        {
            Id = "ent_dly_3d",
            Name = "Wait 3 Days",
            Type = LifecycleNodeType.Delay,
            Description = "3-day buffer to evaluate deal qualification",
            PositionX = 850,
            PositionY = 150,
            NextNodeIds = new() { "ent_cnd_ltv" },
            DelayConfig = new DelayNodeConfig
            {
                Value = 3,
                Unit = DelayUnit.Days
            }
        };

        var entCheckLtv = new LifecycleJourneyNode
        {
            Id = "ent_cnd_ltv",
            Name = "High LTV Enterprise Prospect (LTV > 500)",
            Type = LifecycleNodeType.Condition,
            Description = "Branches based on estimated account deal size",
            PositionX = 1100,
            PositionY = 150,
            ConditionConfig = new ConditionNodeConfig
            {
                Property = "ltv",
                Operator = "GreaterThan",
                Value = "500",
                TrueNodeId = "ent_act_whitepaper",
                FalseNodeId = "ent_act_video"
            }
        };

        var entWhitepaper = new LifecycleJourneyNode
        {
            Id = "ent_act_whitepaper",
            Name = "Executive Architecture Whitepaper & Case Study",
            Type = LifecycleNodeType.Action,
            Description = "Customized case study email from VP of Engineering",
            PositionX = 1350,
            PositionY = 80,
            NextNodeIds = new(),
            ActionConfig = new ActionNodeConfig
            {
                Channel = ActionChannel.Email,
                FallbackChannel = ActionChannel.Sms,
                Subject = "Enterprise Architecture & ROI Analysis Case Study",
                MessageBody = "Here is the architectural deep-dive into how peers achieved 4x ROI."
            }
        };

        var entVideo = new LifecycleJourneyNode
        {
            Id = "ent_act_video",
            Name = "Self-Service Video Walkthrough",
            Type = LifecycleNodeType.Action,
            Description = "On-demand product overview video link",
            PositionX = 1350,
            PositionY = 240,
            NextNodeIds = new(),
            ActionConfig = new ActionNodeConfig
            {
                Channel = ActionChannel.Email,
                Subject = "On-Demand 5-Minute Product Walkthrough",
                MessageBody = "Explore key features at your own pace with our 5-minute interactive video demo."
            }
        };

        var entJourney = new CustomerLifecycleJourney
        {
            Id = entJourneyId,
            TenantId = defaultTenantId,
            Name = "Enterprise Lead Nurture",
            Description = "Multi-tier enterprise lead routing integrating CRM task automation, conditional LTV segmentation, and content delivery.",
            Status = JourneyLifecycleStatus.Active,
            Nodes = new() { entTrigger, entCrm, entBooking, entWait3D, entCheckLtv, entWhitepaper, entVideo }
        };
        SyncEdgesAndNodes(entJourney);
        _journeys[entJourney.Id] = entJourney;
    }

    #endregion
}

#endregion
