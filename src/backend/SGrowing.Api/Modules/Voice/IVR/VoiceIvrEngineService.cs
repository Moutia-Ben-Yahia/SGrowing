using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Voice.IVR;

#region Domain Enums & Models

public enum IvrNodeType
{
    PlayAudioPrompt,
    CollectDtmfDigits,
    SpeechRecognition,
    BranchCondition,
    RouteToQueue,
    VoicemailDrop,
    Hangup
}

public enum IvrSessionStatus
{
    Ringing,
    ActiveInMenu,
    AwaitingInput,
    RoutingToAgent,
    VoicemailRecording,
    Completed,
    FailedTimeout
}

public record IvrNode(
    Guid NodeId,
    string NodeName,
    IvrNodeType NodeType,
    string? PromptTextTts,
    string? AudioPromptUrl,
    List<string>? AllowedDtmfDigits,
    int MaxDigits = 1,
    int TimeoutSeconds = 6,
    int MaxRetries = 3,
    Dictionary<string, Guid>? Transitions = null, // key = digit ("1", "2") or intent ("sales", "billing") -> target NodeId
    Guid? DefaultNextNodeId = null,
    string? TargetQueue = null
);

public record IvrOperatingSchedule(
    int OpenHour = 8,
    int CloseHour = 18,
    List<DayOfWeek>? WorkDays = null,
    bool EnableHolidayOverrides = true
);

public record IvrFlow(
    Guid FlowId,
    Guid TenantId,
    string Name,
    string Description,
    Guid StartNodeId,
    List<IvrNode> Nodes,
    IvrOperatingSchedule OperatingSchedule,
    Guid? OffHoursFallbackNodeId,
    bool IsActive,
    DateTime CreatedAtUtc
);

public record IvrStepAudit(
    Guid NodeId,
    string NodeName,
    IvrNodeType NodeType,
    string? ReceivedInput,
    string ActionResult,
    DateTime TimestampUtc
);

public record IvrActiveSession(
    Guid SessionId,
    Guid TenantId,
    Guid FlowId,
    string CallerPhoneNumber,
    Guid CurrentNodeId,
    string CurrentNodeName,
    IvrNodeType CurrentNodeType,
    string? PromptToPlay,
    IvrSessionStatus Status,
    int CurrentRetries,
    string? RoutedQueue,
    List<IvrStepAudit> AuditTrail,
    DateTime StartedAtUtc,
    DateTime LastActivityAtUtc
);

public record SubmitSessionInputRequest(
    Guid SessionId,
    string? DtmfInput = null,
    string? SpeechTranscript = null,
    double SpeechConfidence = 1.0
);

#endregion

public interface IVoiceIvrEngineService
{
    Task<IvrFlow> SaveFlowAsync(IvrFlow flow, CancellationToken ct = default);
    Task<IvrFlow> GetFlowAsync(Guid tenantId, Guid flowId, CancellationToken ct = default);
    Task<List<IvrFlow>> ListFlowsAsync(Guid tenantId, CancellationToken ct = default);

    Task<IvrActiveSession> InitiateSessionAsync(
        Guid tenantId,
        Guid flowId,
        string callerPhoneNumber,
        DateTime? overrideCallTimeUtc = null,
        CancellationToken ct = default);

    Task<IvrActiveSession> ProcessInputAsync(
        SubmitSessionInputRequest request,
        CancellationToken ct = default);

    Task<IvrActiveSession> GetSessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<List<IvrActiveSession>> GetActiveSessionsAsync(Guid tenantId, CancellationToken ct = default);
}

public class VoiceIvrEngineService : IVoiceIvrEngineService
{
    private readonly ConcurrentDictionary<Guid, IvrFlow> _flows = new();
    private readonly ConcurrentDictionary<Guid, IvrActiveSession> _sessions = new();

    public Task<IvrFlow> SaveFlowAsync(IvrFlow flow, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(flow);

        if (string.IsNullOrWhiteSpace(flow.Name))
        {
            throw new ArgumentException("IVR Flow name cannot be empty.", nameof(flow));
        }

        if (flow.Nodes == null || flow.Nodes.Count == 0)
        {
            throw new ArgumentException("IVR Flow must contain at least one node.", nameof(flow));
        }

        var nodeMap = flow.Nodes.ToDictionary(n => n.NodeId);
        if (!nodeMap.ContainsKey(flow.StartNodeId))
        {
            throw new InvalidOperationException($"StartNodeId '{flow.StartNodeId}' was not found in the nodes list.");
        }

        if (flow.OffHoursFallbackNodeId.HasValue && !nodeMap.ContainsKey(flow.OffHoursFallbackNodeId.Value))
        {
            throw new InvalidOperationException($"OffHoursFallbackNodeId '{flow.OffHoursFallbackNodeId}' was not found in the nodes list.");
        }

        // Validate transitions reference valid nodes
        foreach (var node in flow.Nodes)
        {
            if (node.Transitions != null)
            {
                foreach (var (key, targetId) in node.Transitions)
                {
                    if (!nodeMap.ContainsKey(targetId))
                    {
                        throw new InvalidOperationException($"Node '{node.NodeName}' references invalid target transition NodeId '{targetId}'.");
                    }
                }
            }

            if (node.DefaultNextNodeId.HasValue && !nodeMap.ContainsKey(node.DefaultNextNodeId.Value))
            {
                throw new InvalidOperationException($"Node '{node.NodeName}' references invalid DefaultNextNodeId '{node.DefaultNextNodeId.Value}'.");
            }
        }

        _flows[flow.FlowId] = flow;
        return Task.FromResult(flow);
    }

    public Task<IvrFlow> GetFlowAsync(Guid tenantId, Guid flowId, CancellationToken ct = default)
    {
        if (!_flows.TryGetValue(flowId, out var flow) || flow.TenantId != tenantId)
        {
            throw new KeyNotFoundException($"IVR flow '{flowId}' not found for tenant '{tenantId}'.");
        }
        return Task.FromResult(flow);
    }

    public Task<List<IvrFlow>> ListFlowsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = _flows.Values.Where(f => f.TenantId == tenantId).ToList();
        return Task.FromResult(list);
    }

    public Task<IvrActiveSession> InitiateSessionAsync(
        Guid tenantId,
        Guid flowId,
        string callerPhoneNumber,
        DateTime? overrideCallTimeUtc = null,
        CancellationToken ct = default)
    {
        if (!_flows.TryGetValue(flowId, out var flow) || flow.TenantId != tenantId)
        {
            throw new KeyNotFoundException($"IVR flow '{flowId}' not found.");
        }

        if (!flow.IsActive)
        {
            throw new InvalidOperationException("This IVR flow is currently inactive.");
        }

        var callTime = overrideCallTimeUtc ?? DateTime.UtcNow;
        var nodeMap = flow.Nodes.ToDictionary(n => n.NodeId);

        // Evaluate Operating Hours
        bool isWithinHours = true;
        var schedule = flow.OperatingSchedule;
        if (schedule != null)
        {
            var workDays = schedule.WorkDays ?? new List<DayOfWeek>
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday
            };

            if (!workDays.Contains(callTime.DayOfWeek) || callTime.Hour < schedule.OpenHour || callTime.Hour >= schedule.CloseHour)
            {
                isWithinHours = false;
            }
        }

        Guid startingNodeId = (!isWithinHours && flow.OffHoursFallbackNodeId.HasValue)
            ? flow.OffHoursFallbackNodeId.Value
            : flow.StartNodeId;

        var startNode = nodeMap[startingNodeId];
        var sessionId = Guid.NewGuid();

        var initialAudit = new IvrStepAudit(
            startNode.NodeId,
            startNode.NodeName,
            startNode.NodeType,
            null,
            isWithinHours ? "SessionInitiatedInHours" : "SessionInitiatedOffHoursFallback",
            DateTime.UtcNow
        );

        var status = startNode.NodeType switch
        {
            IvrNodeType.CollectDtmfDigits or IvrNodeType.SpeechRecognition => IvrSessionStatus.AwaitingInput,
            IvrNodeType.RouteToQueue => IvrSessionStatus.RoutingToAgent,
            IvrNodeType.VoicemailDrop => IvrSessionStatus.VoicemailRecording,
            IvrNodeType.Hangup => IvrSessionStatus.Completed,
            _ => IvrSessionStatus.ActiveInMenu
        };

        var session = new IvrActiveSession(
            sessionId,
            tenantId,
            flowId,
            callerPhoneNumber?.Trim() ?? "Unknown",
            startNode.NodeId,
            startNode.NodeName,
            startNode.NodeType,
            startNode.PromptTextTts ?? startNode.AudioPromptUrl,
            status,
            0,
            startNode.TargetQueue,
            new List<IvrStepAudit> { initialAudit },
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        _sessions[sessionId] = session;
        return Task.FromResult(session);
    }

    public Task<IvrActiveSession> ProcessInputAsync(
        SubmitSessionInputRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_sessions.TryGetValue(request.SessionId, out var session))
        {
            throw new KeyNotFoundException($"Active IVR session '{request.SessionId}' not found.");
        }

        if (session.Status == IvrSessionStatus.Completed || session.Status == IvrSessionStatus.FailedTimeout)
        {
            throw new InvalidOperationException($"Cannot process input for session in terminal status '{session.Status}'.");
        }

        var flow = _flows[session.FlowId];
        var nodeMap = flow.Nodes.ToDictionary(n => n.NodeId);
        var currentNode = nodeMap[session.CurrentNodeId];

        string rawInput = (request.DtmfInput ?? request.SpeechTranscript ?? string.Empty).Trim();
        var now = DateTime.UtcNow;

        // Check if input matches transitions
        Guid? nextNodeId = null;
        bool isMatch = false;

        if (currentNode.Transitions != null && currentNode.Transitions.Count > 0)
        {
            if (currentNode.Transitions.TryGetValue(rawInput, out var targetId))
            {
                nextNodeId = targetId;
                isMatch = true;
            }
            else
            {
                // Fallback check case-insensitive speech intent
                var intentMatch = currentNode.Transitions.FirstOrDefault(kv =>
                    string.Equals(kv.Key, rawInput, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(intentMatch.Key))
                {
                    nextNodeId = intentMatch.Value;
                    isMatch = true;
                }
            }
        }

        // If no transition match, check DefaultNextNodeId when node is not input-conditional
        if (!isMatch && currentNode.NodeType != IvrNodeType.CollectDtmfDigits && currentNode.NodeType != IvrNodeType.SpeechRecognition)
        {
            nextNodeId = currentNode.DefaultNextNodeId;
            isMatch = true;
        }

        // Handle Invalid / Failed Input
        if (!isMatch)
        {
            int retries = session.CurrentRetries + 1;
            if (retries >= currentNode.MaxRetries)
            {
                // Max retries exceeded -> escalate to default next or fail
                if (currentNode.DefaultNextNodeId.HasValue)
                {
                    nextNodeId = currentNode.DefaultNextNodeId.Value;
                }
                else
                {
                    var failAudit = new IvrStepAudit(
                        currentNode.NodeId,
                        currentNode.NodeName,
                        currentNode.NodeType,
                        rawInput,
                        "MaxRetriesExceeded_Disconnected",
                        now
                    );
                    session.AuditTrail.Add(failAudit);

                    var terminatedSession = session with
                    {
                        Status = IvrSessionStatus.FailedTimeout,
                        CurrentRetries = retries,
                        LastActivityAtUtc = now
                    };
                    _sessions[session.SessionId] = terminatedSession;
                    return Task.FromResult(terminatedSession);
                }
            }
            else
            {
                // Prompt retry
                var retryAudit = new IvrStepAudit(
                    currentNode.NodeId,
                    currentNode.NodeName,
                    currentNode.NodeType,
                    rawInput,
                    $"InvalidInput_Retry_{retries}",
                    now
                );
                session.AuditTrail.Add(retryAudit);

                var retrySession = session with
                {
                    CurrentRetries = retries,
                    LastActivityAtUtc = now
                };
                _sessions[session.SessionId] = retrySession;
                return Task.FromResult(retrySession);
            }
        }

        // Transition to Next Node
        if (!nextNodeId.HasValue || !nodeMap.TryGetValue(nextNodeId.Value, out var nextNode))
        {
            throw new InvalidOperationException($"Transition resolved to non-existent target node ID.");
        }

        var transitionAudit = new IvrStepAudit(
            currentNode.NodeId,
            currentNode.NodeName,
            currentNode.NodeType,
            rawInput,
            $"TransitionedTo_{nextNode.NodeName}",
            now
        );
        session.AuditTrail.Add(transitionAudit);

        var newStatus = nextNode.NodeType switch
        {
            IvrNodeType.CollectDtmfDigits or IvrNodeType.SpeechRecognition => IvrSessionStatus.AwaitingInput,
            IvrNodeType.RouteToQueue => IvrSessionStatus.RoutingToAgent,
            IvrNodeType.VoicemailDrop => IvrSessionStatus.VoicemailRecording,
            IvrNodeType.Hangup => IvrSessionStatus.Completed,
            _ => IvrSessionStatus.ActiveInMenu
        };

        var updatedSession = session with
        {
            CurrentNodeId = nextNode.NodeId,
            CurrentNodeName = nextNode.NodeName,
            CurrentNodeType = nextNode.NodeType,
            PromptToPlay = nextNode.PromptTextTts ?? nextNode.AudioPromptUrl,
            Status = newStatus,
            CurrentRetries = 0,
            RoutedQueue = nextNode.TargetQueue ?? session.RoutedQueue,
            LastActivityAtUtc = now
        };

        _sessions[session.SessionId] = updatedSession;
        return Task.FromResult(updatedSession);
    }

    public Task<IvrActiveSession> GetSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Session '{sessionId}' not found.");
        }
        return Task.FromResult(session);
    }

    public Task<List<IvrActiveSession>> GetActiveSessionsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = _sessions.Values
            .Where(s => s.TenantId == tenantId && s.Status != IvrSessionStatus.Completed && s.Status != IvrSessionStatus.FailedTimeout)
            .OrderByDescending(s => s.StartedAtUtc)
            .ToList();

        return Task.FromResult(list);
    }
}
