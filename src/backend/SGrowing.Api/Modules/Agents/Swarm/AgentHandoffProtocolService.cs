using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Agents.Swarm;

/// <summary>
/// Structured outcome of a handoff consensus evaluation.
/// </summary>
public record HandoffDeliberationOutcome(
    Guid SessionId,
    Guid TenantId,
    SwarmStage NewStage,
    bool Approved,
    ConsensusEvaluationResult ConsensusResult,
    ConsensusExecutionToken? ExecutionToken,
    HumanInTheLoopTicket? EscalationTicket,
    string Summary
);

/// <summary>
/// Service contract governing the multi-agent handoff protocol lifecycle.
/// </summary>
public interface IAgentHandoffProtocolService
{
    Task<BlackboardSnapshot> CreateSessionAsync(
        Guid tenantId,
        string initialContext,
        SwarmProposedAction? initialAction = null,
        CancellationToken ct = default
    );

    BlackboardSnapshot? GetSessionSnapshot(Guid sessionId);

    Task<BlackboardSnapshot> AdvanceStageAsync(
        Guid sessionId,
        SwarmStage targetStage,
        string initiatedBy,
        CancellationToken ct = default
    );

    Task<BlackboardSnapshot> DeliberateAsync(
        Guid sessionId,
        DeliberationStepRecord step,
        CancellationToken ct = default
    );

    Task<BlackboardSnapshot> AddHypothesisAsync(
        Guid sessionId,
        AgentHypothesis hypothesis,
        CancellationToken ct = default
    );

    Task<BlackboardSnapshot> SubmitVoteAsync(
        Guid sessionId,
        AgentVote vote,
        CancellationToken ct = default
    );

    Task<HandoffDeliberationOutcome> EvaluateConsensusAndProgressAsync(
        Guid sessionId,
        string? tenantSecret = null,
        QuorumPolicy? policy = null,
        CancellationToken ct = default
    );

    TokenVerificationResult VerifyExecutionToken(
        ConsensusExecutionToken token,
        IReadOnlyList<AgentVote> votes,
        string tenantSecret
    );
}

/// <summary>
/// Governs structured stage transitions and quorum execution across specialized swarm agents.
/// Pipeline: LeadIntake -> TechnicalScoping -> FinancialRiskReview -> QuorumDeliberation -> ApprovedForExecution | HumanInTheLoopEscalated.
/// </summary>
public sealed class AgentHandoffProtocolService(
    IAgentConsensusEngine consensusEngine,
    ICryptographicQuorumSigner quorumSigner
) : IAgentHandoffProtocolService
{
    private readonly ConcurrentDictionary<Guid, AgentBlackboardState> _sessions = new();

    public Task<BlackboardSnapshot> CreateSessionAsync(
        Guid tenantId,
        string initialContext,
        SwarmProposedAction? initialAction = null,
        CancellationToken ct = default)
    {
        var sessionId = Guid.NewGuid();
        var state = new AgentBlackboardState(sessionId, tenantId, initialContext, initialAction);
        _sessions[sessionId] = state;
        return Task.FromResult(state.ToSnapshot());
    }

    public BlackboardSnapshot? GetSessionSnapshot(Guid sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var state) ? state.ToSnapshot() : null;
    }

    public Task<BlackboardSnapshot> AdvanceStageAsync(
        Guid sessionId,
        SwarmStage targetStage,
        string initiatedBy,
        CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
        {
            throw new KeyNotFoundException($"Swarm session {sessionId} not found.");
        }

        if (!state.TryTransitionStage(targetStage, initiatedBy, out var error))
        {
            throw new InvalidOperationException(error ?? $"Failed to transition to {targetStage}.");
        }

        return Task.FromResult(state.ToSnapshot());
    }

    public Task<BlackboardSnapshot> DeliberateAsync(
        Guid sessionId,
        DeliberationStepRecord step,
        CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
        {
            throw new KeyNotFoundException($"Swarm session {sessionId} not found.");
        }

        state.AppendDeliberationRecord(step);
        return Task.FromResult(state.ToSnapshot());
    }

    public Task<BlackboardSnapshot> AddHypothesisAsync(
        Guid sessionId,
        AgentHypothesis hypothesis,
        CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
        {
            throw new KeyNotFoundException($"Swarm session {sessionId} not found.");
        }

        state.AddHypothesis(hypothesis);
        return Task.FromResult(state.ToSnapshot());
    }

    public Task<BlackboardSnapshot> SubmitVoteAsync(
        Guid sessionId,
        AgentVote vote,
        CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
        {
            throw new KeyNotFoundException($"Swarm session {sessionId} not found.");
        }

        state.AddOrUpdateVote(vote);
        return Task.FromResult(state.ToSnapshot());
    }

    public Task<HandoffDeliberationOutcome> EvaluateConsensusAndProgressAsync(
        Guid sessionId,
        string? tenantSecret = null,
        QuorumPolicy? policy = null,
        CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
        {
            throw new KeyNotFoundException($"Swarm session {sessionId} not found.");
        }

        // Auto-advance to QuorumDeliberation if currently in earlier active stage
        if (state.CurrentStage is SwarmStage.LeadIntake or SwarmStage.TechnicalScoping or SwarmStage.FinancialRiskReview)
        {
            state.TryTransitionStage(SwarmStage.QuorumDeliberation, "ProtocolCoordinator", out _);
        }

        var votes = state.Votes;
        var proposedAction = state.ProposedAction;
        var consensusResult = consensusEngine.EvaluateConsensus(votes, proposedAction, policy);

        var effectiveSecret = !string.IsNullOrWhiteSpace(tenantSecret)
            ? tenantSecret
            : $"sgrowing-tenant-{state.TenantId:N}-quorum-secret-masp-2026";

        if (consensusResult.Status == ConsensusEvaluationStatus.Passed)
        {
            var token = quorumSigner.GenerateExecutionToken(
                state.SessionId,
                state.TenantId,
                consensusResult.AcceptedVotes,
                consensusResult.WeightedApprovalScore,
                effectiveSecret
            );

            state.TryTransitionStage(SwarmStage.ApprovedForExecution, "ConsensusEngine", out _);

            var outcome = new HandoffDeliberationOutcome(
                SessionId: state.SessionId,
                TenantId: state.TenantId,
                NewStage: SwarmStage.ApprovedForExecution,
                Approved: true,
                ConsensusResult: consensusResult,
                ExecutionToken: token,
                EscalationTicket: null,
                Summary: $"Consensus passed with {consensusResult.WeightedApprovalScore:P1} weighted approval. Execution token {token.TokenId} signed."
            );

            return Task.FromResult(outcome);
        }

        // Handle Veto, QuorumNotReached, or EscalationRequired -> Trigger Human-In-The-Loop
        var escalationFactors = new List<string>();

        if (consensusResult.Status == ConsensusEvaluationStatus.Vetoed)
        {
            escalationFactors.Add(consensusResult.VetoReason ?? "Single-agent veto triggered.");
        }
        else if (consensusResult.Status == ConsensusEvaluationStatus.QuorumNotReached)
        {
            escalationFactors.Add($"Quorum threshold of {(policy?.ApprovalThreshold ?? 0.75):P1} not met. Achieved: {consensusResult.WeightedApprovalScore:P1}.");
        }
        else if (consensusResult.Status == ConsensusEvaluationStatus.EscalationRequired)
        {
            escalationFactors.Add($"Swarm consensus compromised by Byzantine/untrusted agent votes ({consensusResult.QuarantinedVotes.Count} quarantined).");
            foreach (var q in consensusResult.QuarantinedVotes)
            {
                escalationFactors.Add(q.QuarantineReason);
            }
        }

        var ticket = new HumanInTheLoopTicket(
            TicketId: Guid.NewGuid(),
            SessionId: state.SessionId,
            TenantId: state.TenantId,
            Reason: consensusResult.VetoReason ?? $"Swarm consensus failed with status: {consensusResult.Status}.",
            EscalationFactors: escalationFactors,
            CreatedAt: DateTimeOffset.UtcNow
        );

        state.SetEscalationTicket(ticket);

        var failedOutcome = new HandoffDeliberationOutcome(
            SessionId: state.SessionId,
            TenantId: state.TenantId,
            NewStage: SwarmStage.HumanInTheLoopEscalated,
            Approved: false,
            ConsensusResult: consensusResult,
            ExecutionToken: null,
            EscalationTicket: ticket,
            Summary: $"Deliberation escalated to Human-In-The-Loop review. Reason: {ticket.Reason}"
        );

        return Task.FromResult(failedOutcome);
    }

    public TokenVerificationResult VerifyExecutionToken(
        ConsensusExecutionToken token,
        IReadOnlyList<AgentVote> votes,
        string tenantSecret)
    {
        return quorumSigner.VerifyExecutionToken(token, votes, tenantSecret);
    }
}
