using System.Threading;

namespace SGrowing.Api.Modules.Agents.Swarm;

/// <summary>
/// Execution stages of the Autonomous Multi-Agent Consensus Swarm &amp; Handoff Protocol (MASP).
/// </summary>
public enum SwarmStage
{
    LeadIntake,
    TechnicalScoping,
    FinancialRiskReview,
    QuorumDeliberation,
    ApprovedForExecution,
    HumanInTheLoopEscalated
}

/// <summary>
/// Canonical agent role constants participating in the multi-agent consensus swarm.
/// </summary>
public static class SwarmAgentRoles
{
    public const string ProspectingSdrAgent = "ProspectingSdrAgent";
    public const string SolutionsEngineerAgent = "SolutionsEngineerAgent";
    public const string FinancialRiskAgent = "FinancialRiskAgent";
    public const string LegalComplianceAgent = "LegalComplianceAgent";

    public static readonly IReadOnlyList<string> CoreQuorumRoles =
    [
        ProspectingSdrAgent,
        SolutionsEngineerAgent,
        FinancialRiskAgent,
        LegalComplianceAgent
    ];
}

/// <summary>
/// Immutable audit log entry representing a deliberate step or utterance by an agent in the blackboard.
/// </summary>
public record DeliberationStepRecord(
    Guid StepId,
    string AgentRole,
    string StepType,
    string Content,
    DateTimeOffset Timestamp,
    double? ConfidenceScore = null,
    IReadOnlyDictionary<string, string>? Metadata = null
);

/// <summary>
/// A proposed hypothesis in the blackboard problem-solving pool.
/// </summary>
public record AgentHypothesis(
    Guid HypothesisId,
    string ProposedByAgent,
    string Statement,
    double Confidence,
    int Upvotes = 0,
    int Downvotes = 0,
    DateTimeOffset CreatedAt = default
);

/// <summary>
/// Commercial or technical proposal submitted to the swarm for quorum voting and execution.
/// </summary>
public record SwarmProposedAction(
    Guid ActionId,
    string ActionType,
    string Description,
    decimal ProposedDiscountPercentage = 0m,
    int PaymentTermsDays = 30,
    decimal DealValue = 0m,
    IReadOnlyDictionary<string, string>? Parameters = null
);

/// <summary>
/// An individual vote submitted by a specialized agent during QuorumDeliberation.
/// </summary>
public record AgentVote(
    string AgentRole,
    bool Approved,
    double ConfidenceScore,
    string Rationale,
    double ReasoningEntropy = 0.5,
    DateTimeOffset VotedAt = default,
    IReadOnlyDictionary<string, string>? Metadata = null
);

/// <summary>
/// Ticket created when consensus cannot be safely reached and human intervention is mandated.
/// </summary>
public record HumanInTheLoopTicket(
    Guid TicketId,
    Guid SessionId,
    Guid TenantId,
    string Reason,
    IReadOnlyList<string> EscalationFactors,
    DateTimeOffset CreatedAt,
    string Status = "PendingHumanReview"
);

/// <summary>
/// Immutable snapshot representation of the blackboard state for point-in-time serialization.
/// </summary>
public record BlackboardSnapshot(
    Guid SessionId,
    Guid TenantId,
    string InitialContext,
    SwarmStage CurrentStage,
    SwarmProposedAction? ProposedAction,
    IReadOnlyList<AgentHypothesis> Hypotheses,
    IReadOnlyList<DeliberationStepRecord> DeliberationAuditRecords,
    IReadOnlyList<AgentVote> Votes,
    HumanInTheLoopTicket? EscalationTicket,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

/// <summary>
/// Shared blackboard state representation for multi-agent deliberative problem solving.
/// Provides thread-safe mutations, atomic stage transitions, and immutable audit logging.
/// </summary>
public sealed class AgentBlackboardState
{
    private readonly Lock _sync = new();
    private readonly List<AgentHypothesis> _hypotheses = [];
    private readonly List<DeliberationStepRecord> _deliberationRecords = [];
    private readonly Dictionary<string, AgentVote> _votesByRole = new(StringComparer.OrdinalIgnoreCase);

    private SwarmStage _currentStage;
    private SwarmProposedAction? _proposedAction;
    private HumanInTheLoopTicket? _escalationTicket;
    private DateTimeOffset _updatedAt;

    public Guid SessionId { get; }
    public Guid TenantId { get; }
    public string InitialContext { get; }
    public DateTimeOffset CreatedAt { get; }

    public AgentBlackboardState(
        Guid sessionId,
        Guid tenantId,
        string initialContext,
        SwarmProposedAction? initialAction = null)
    {
        SessionId = sessionId == Guid.Empty ? Guid.NewGuid() : sessionId;
        TenantId = tenantId;
        InitialContext = initialContext ?? string.Empty;
        _proposedAction = initialAction;
        _currentStage = SwarmStage.LeadIntake;
        CreatedAt = DateTimeOffset.UtcNow;
        _updatedAt = CreatedAt;

        _deliberationRecords.Add(new DeliberationStepRecord(
            Guid.NewGuid(),
            "System",
            "SessionInitialized",
            $"Blackboard session {SessionId} initialized in stage {_currentStage}.",
            CreatedAt
        ));
    }

    public SwarmStage CurrentStage
    {
        get
        {
            lock (_sync) return _currentStage;
        }
    }

    public SwarmProposedAction? ProposedAction
    {
        get
        {
            lock (_sync) return _proposedAction;
        }
    }

    public HumanInTheLoopTicket? EscalationTicket
    {
        get
        {
            lock (_sync) return _escalationTicket;
        }
    }

    public DateTimeOffset UpdatedAt
    {
        get
        {
            lock (_sync) return _updatedAt;
        }
    }

    public IReadOnlyList<AgentHypothesis> Hypotheses
    {
        get
        {
            lock (_sync) return [.. _hypotheses];
        }
    }

    public IReadOnlyList<DeliberationStepRecord> DeliberationAuditRecords
    {
        get
        {
            lock (_sync) return [.. _deliberationRecords];
        }
    }

    public IReadOnlyList<AgentVote> Votes
    {
        get
        {
            lock (_sync) return [.. _votesByRole.Values];
        }
    }

    /// <summary>
    /// Attempts an atomic stage transition according to the multi-agent handoff state machine.
    /// </summary>
    public bool TryTransitionStage(SwarmStage targetStage, string initiatedBy, out string? errorMessage)
    {
        lock (_sync)
        {
            if (_currentStage == targetStage)
            {
                errorMessage = null;
                return true;
            }

            // Escalation can happen from any non-terminal stage
            if (targetStage == SwarmStage.HumanInTheLoopEscalated)
            {
                var previous = _currentStage;
                _currentStage = targetStage;
                _updatedAt = DateTimeOffset.UtcNow;
                _deliberationRecords.Add(new DeliberationStepRecord(
                    Guid.NewGuid(),
                    initiatedBy,
                    "StageEscalated",
                    $"Session escalated to HumanInTheLoopEscalated from {previous}.",
                    _updatedAt
                ));
                errorMessage = null;
                return true;
            }

            // Terminal check
            if (_currentStage is SwarmStage.ApprovedForExecution or SwarmStage.HumanInTheLoopEscalated)
            {
                errorMessage = $"Cannot transition from terminal stage {_currentStage} to {targetStage}.";
                return false;
            }

            // Standard linear pipeline progression
            var isValidTransition = (_currentStage, targetStage) switch
            {
                (SwarmStage.LeadIntake, SwarmStage.TechnicalScoping) => true,
                (SwarmStage.TechnicalScoping, SwarmStage.FinancialRiskReview) => true,
                (SwarmStage.FinancialRiskReview, SwarmStage.QuorumDeliberation) => true,
                (SwarmStage.QuorumDeliberation, SwarmStage.ApprovedForExecution) => true,
                _ => false
            };

            if (!isValidTransition)
            {
                errorMessage = $"Invalid stage transition from {_currentStage} to {targetStage}.";
                return false;
            }

            var priorStage = _currentStage;
            _currentStage = targetStage;
            _updatedAt = DateTimeOffset.UtcNow;
            _deliberationRecords.Add(new DeliberationStepRecord(
                Guid.NewGuid(),
                initiatedBy,
                "StageTransition",
                $"Stage advanced from {priorStage} to {targetStage}.",
                _updatedAt
            ));

            errorMessage = null;
            return true;
        }
    }

    /// <summary>
    /// Thread-safe update of the proposed action under deliberation.
    /// </summary>
    public void SetProposedAction(SwarmProposedAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_sync)
        {
            _proposedAction = action;
            _updatedAt = DateTimeOffset.UtcNow;
            _deliberationRecords.Add(new DeliberationStepRecord(
                Guid.NewGuid(),
                "Coordinator",
                "ProposedActionUpdated",
                $"Proposed action updated to '{action.ActionType}' (Discount: {action.ProposedDiscountPercentage}%, Terms: {action.PaymentTermsDays}d).",
                _updatedAt
            ));
        }
    }

    /// <summary>
    /// Appends a new hypothesis into the blackboard deliberative pool.
    /// </summary>
    public void AddHypothesis(AgentHypothesis hypothesis)
    {
        ArgumentNullException.ThrowIfNull(hypothesis);
        lock (_sync)
        {
            var item = hypothesis.CreatedAt == default
                ? hypothesis with { CreatedAt = DateTimeOffset.UtcNow }
                : hypothesis;
            _hypotheses.Add(item);
            _updatedAt = DateTimeOffset.UtcNow;
            _deliberationRecords.Add(new DeliberationStepRecord(
                Guid.NewGuid(),
                hypothesis.ProposedByAgent,
                "HypothesisAdded",
                $"Hypothesis proposed: \"{hypothesis.Statement}\" (Confidence: {hypothesis.Confidence:P0})",
                _updatedAt,
                hypothesis.Confidence
            ));
        }
    }

    /// <summary>
    /// Upvotes or downvotes an existing hypothesis.
    /// </summary>
    public bool VoteHypothesis(Guid hypothesisId, bool upvote)
    {
        lock (_sync)
        {
            var index = _hypotheses.FindIndex(h => h.HypothesisId == hypothesisId);
            if (index < 0) return false;

            var existing = _hypotheses[index];
            _hypotheses[index] = upvote
                ? existing with { Upvotes = existing.Upvotes + 1 }
                : existing with { Downvotes = existing.Downvotes + 1 };

            _updatedAt = DateTimeOffset.UtcNow;
            return true;
        }
    }

    /// <summary>
    /// Appends an immutable deliberation record to the audit trail.
    /// </summary>
    public void AppendDeliberationRecord(DeliberationStepRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_sync)
        {
            _deliberationRecords.Add(record);
            _updatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Submits or updates an agent vote in QuorumDeliberation.
    /// </summary>
    public void AddOrUpdateVote(AgentVote vote)
    {
        ArgumentNullException.ThrowIfNull(vote);
        if (string.IsNullOrWhiteSpace(vote.AgentRole))
        {
            throw new ArgumentException("AgentRole cannot be empty.", nameof(vote));
        }

        lock (_sync)
        {
            var normalizedVote = vote.VotedAt == default
                ? vote with { VotedAt = DateTimeOffset.UtcNow }
                : vote;

            _votesByRole[vote.AgentRole] = normalizedVote;
            _updatedAt = DateTimeOffset.UtcNow;

            _deliberationRecords.Add(new DeliberationStepRecord(
                Guid.NewGuid(),
                vote.AgentRole,
                "VoteSubmitted",
                $"Vote: {(vote.Approved ? "APPROVED" : "REJECTED")} | Confidence: {vote.ConfidenceScore:F2} | Entropy: {vote.ReasoningEntropy:F2} | Rationale: {vote.Rationale}",
                _updatedAt,
                vote.ConfidenceScore
            ));
        }
    }

    /// <summary>
    /// Registers a human-in-the-loop escalation ticket and transitions stage to HumanInTheLoopEscalated.
    /// </summary>
    public void SetEscalationTicket(HumanInTheLoopTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        lock (_sync)
        {
            _escalationTicket = ticket;
            _currentStage = SwarmStage.HumanInTheLoopEscalated;
            _updatedAt = DateTimeOffset.UtcNow;

            _deliberationRecords.Add(new DeliberationStepRecord(
                Guid.NewGuid(),
                "EscalationEngine",
                "EscalationTicketCreated",
                $"HITL Ticket {ticket.TicketId} logged. Reason: {ticket.Reason}",
                _updatedAt
            ));
        }
    }

    /// <summary>
    /// Returns an immutable snapshot copy of current blackboard state.
    /// </summary>
    public BlackboardSnapshot ToSnapshot()
    {
        lock (_sync)
        {
            return new BlackboardSnapshot(
                SessionId,
                TenantId,
                InitialContext,
                _currentStage,
                _proposedAction,
                [.. _hypotheses],
                [.. _deliberationRecords],
                [.. _votesByRole.Values],
                _escalationTicket,
                CreatedAt,
                _updatedAt
            );
        }
    }
}
