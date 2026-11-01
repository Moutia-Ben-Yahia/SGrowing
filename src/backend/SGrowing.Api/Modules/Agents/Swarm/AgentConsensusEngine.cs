namespace SGrowing.Api.Modules.Agents.Swarm;

/// <summary>
/// Status of multi-agent deliberation and quorum evaluation.
/// </summary>
public enum ConsensusEvaluationStatus
{
    Passed,
    Vetoed,
    QuorumNotReached,
    EscalationRequired
}

/// <summary>
/// Record of an agent vote quarantined due to Byzantine or hallucination criteria.
/// </summary>
public record QuarantinedVoteRecord(
    AgentVote Vote,
    string QuarantineReason,
    DateTimeOffset QuarantinedAt
);

/// <summary>
/// Policy settings governing quorum thresholds, Byzantine filters, and veto rules.
/// </summary>
public record QuorumPolicy(
    double ApprovalThreshold = 0.75,
    double MinConfidenceThreshold = 0.60,
    double MaxReasoningEntropy = 2.5,
    decimal MaxAllowedDiscountPercentage = 20.0m,
    int MaxAllowedPaymentTermsDays = 60,
    IReadOnlyDictionary<string, double>? RoleWeights = null
);

/// <summary>
/// Result of multi-agent consensus deliberation evaluation.
/// </summary>
public record ConsensusEvaluationResult(
    ConsensusEvaluationStatus Status,
    double ApprovalRatio,
    double WeightedApprovalScore,
    bool QuorumMet,
    bool HasVeto,
    string? VetoingAgent,
    string? VetoReason,
    IReadOnlyList<AgentVote> AcceptedVotes,
    IReadOnlyList<QuarantinedVoteRecord> QuarantinedVotes,
    IReadOnlyList<string> DeliberationLogs,
    DateTimeOffset EvaluatedAt
);

/// <summary>
/// Service contract for multi-agent voting and quorum evaluation.
/// </summary>
public interface IAgentConsensusEngine
{
    ConsensusEvaluationResult EvaluateConsensus(
        IReadOnlyList<AgentVote> votes,
        SwarmProposedAction? proposedAction,
        QuorumPolicy? policy = null
    );
}

/// <summary>
/// Multi-agent voting and quorum coordinator.
/// Evaluates agent votes, enforces Byzantine fault tolerance, executes financial vetoes,
/// and computes weighted consensus scores.
/// </summary>
public sealed class AgentConsensusEngine : IAgentConsensusEngine
{
    private static readonly Dictionary<string, double> DefaultWeights = new(StringComparer.OrdinalIgnoreCase)
    {
        [SwarmAgentRoles.ProspectingSdrAgent] = 1.0,
        [SwarmAgentRoles.SolutionsEngineerAgent] = 1.0,
        [SwarmAgentRoles.FinancialRiskAgent] = 1.0,
        [SwarmAgentRoles.LegalComplianceAgent] = 1.0
    };

    public ConsensusEvaluationResult EvaluateConsensus(
        IReadOnlyList<AgentVote> votes,
        SwarmProposedAction? proposedAction,
        QuorumPolicy? policy = null)
    {
        policy ??= new QuorumPolicy();
        var logs = new List<string>();
        var now = DateTimeOffset.UtcNow;

        logs.Add($"Consensus evaluation initiated at {now:O} with {votes?.Count ?? 0} submitted vote(s).");

        if (votes == null || votes.Count == 0)
        {
            logs.Add("No votes submitted. Quorum cannot be achieved.");
            return new ConsensusEvaluationResult(
                Status: ConsensusEvaluationStatus.QuorumNotReached,
                ApprovalRatio: 0.0,
                WeightedApprovalScore: 0.0,
                QuorumMet: false,
                HasVeto: false,
                VetoingAgent: null,
                VetoReason: null,
                AcceptedVotes: [],
                QuarantinedVotes: [],
                DeliberationLogs: logs,
                EvaluatedAt: now
            );
        }

        var acceptedVotes = new List<AgentVote>();
        var quarantinedVotes = new List<QuarantinedVoteRecord>();

        // 1. Byzantine & Hallucination Filter
        foreach (var vote in votes)
        {
            if (vote.ConfidenceScore < policy.MinConfidenceThreshold)
            {
                var reason = $"Vote from '{vote.AgentRole}' quarantined: Confidence score ({vote.ConfidenceScore:F2}) below threshold ({policy.MinConfidenceThreshold:F2}). Suspected hallucination.";
                logs.Add(reason);
                quarantinedVotes.Add(new QuarantinedVoteRecord(vote, reason, now));
                continue;
            }

            if (vote.ReasoningEntropy > policy.MaxReasoningEntropy || vote.ReasoningEntropy < 0.0)
            {
                var reason = $"Vote from '{vote.AgentRole}' quarantined: Reasoning entropy ({vote.ReasoningEntropy:F2}) outside allowable bounds [0.0, {policy.MaxReasoningEntropy:F2}]. Suspected divergence.";
                logs.Add(reason);
                quarantinedVotes.Add(new QuarantinedVoteRecord(vote, reason, now));
                continue;
            }

            acceptedVotes.Add(vote);
            logs.Add($"Accepted vote from '{vote.AgentRole}': Approved={vote.Approved}, Confidence={vote.ConfidenceScore:F2}.");
        }

        // Check if swarm is compromised by excessive Byzantine votes
        if (quarantinedVotes.Count > 0 && quarantinedVotes.Count >= acceptedVotes.Count)
        {
            logs.Add($"Consensus compromised: {quarantinedVotes.Count} votes quarantined vs {acceptedVotes.Count} accepted votes. Triggering EscalationRequired.");
            return new ConsensusEvaluationResult(
                Status: ConsensusEvaluationStatus.EscalationRequired,
                ApprovalRatio: 0.0,
                WeightedApprovalScore: 0.0,
                QuorumMet: false,
                HasVeto: false,
                VetoingAgent: null,
                VetoReason: $"Swarm Byzantine fault threshold exceeded ({quarantinedVotes.Count} quarantined votes).",
                AcceptedVotes: acceptedVotes,
                QuarantinedVotes: quarantinedVotes,
                DeliberationLogs: logs,
                EvaluatedAt: now
            );
        }

        // 2. Financial Veto Enforcement
        // FinancialRiskAgent has single-veto power if:
        // - Proposed discount > 20%
        // - Proposed payment terms exceed credit threshold (e.g. 60 days)
        // - FinancialRiskAgent explicitly votes Approved == false
        bool hasVeto = false;
        string? vetoingAgent = null;
        string? vetoReason = null;

        var financialVote = acceptedVotes.FirstOrDefault(v =>
            string.Equals(v.AgentRole, SwarmAgentRoles.FinancialRiskAgent, StringComparison.OrdinalIgnoreCase));

        if (proposedAction != null)
        {
            if (proposedAction.ProposedDiscountPercentage > policy.MaxAllowedDiscountPercentage)
            {
                hasVeto = true;
                vetoingAgent = SwarmAgentRoles.FinancialRiskAgent;
                vetoReason = $"Financial veto triggered: Proposed discount of {proposedAction.ProposedDiscountPercentage}% exceeds the maximum allowable threshold of {policy.MaxAllowedDiscountPercentage}%.";
                logs.Add(vetoReason);
            }
            else if (proposedAction.PaymentTermsDays > policy.MaxAllowedPaymentTermsDays)
            {
                hasVeto = true;
                vetoingAgent = SwarmAgentRoles.FinancialRiskAgent;
                vetoReason = $"Financial veto triggered: Payment terms of {proposedAction.PaymentTermsDays} days exceed the maximum credit threshold of {policy.MaxAllowedPaymentTermsDays} days.";
                logs.Add(vetoReason);
            }
        }

        if (!hasVeto && financialVote != null && !financialVote.Approved)
        {
            hasVeto = true;
            vetoingAgent = SwarmAgentRoles.FinancialRiskAgent;
            vetoReason = $"FinancialRiskAgent explicit veto: {financialVote.Rationale}";
            logs.Add(vetoReason);
        }

        if (hasVeto)
        {
            return new ConsensusEvaluationResult(
                Status: ConsensusEvaluationStatus.Vetoed,
                ApprovalRatio: 0.0,
                WeightedApprovalScore: 0.0,
                QuorumMet: false,
                HasVeto: true,
                VetoingAgent: vetoingAgent,
                VetoReason: vetoReason,
                AcceptedVotes: acceptedVotes,
                QuarantinedVotes: quarantinedVotes,
                DeliberationLogs: logs,
                EvaluatedAt: now
            );
        }

        // 3. Quorum Weighted Calculation
        if (acceptedVotes.Count == 0)
        {
            logs.Add("No valid accepted votes remaining after Byzantine filtering. QuorumNotReached.");
            return new ConsensusEvaluationResult(
                Status: ConsensusEvaluationStatus.QuorumNotReached,
                ApprovalRatio: 0.0,
                WeightedApprovalScore: 0.0,
                QuorumMet: false,
                HasVeto: false,
                VetoingAgent: null,
                VetoReason: null,
                AcceptedVotes: acceptedVotes,
                QuarantinedVotes: quarantinedVotes,
                DeliberationLogs: logs,
                EvaluatedAt: now
            );
        }

        var weights = policy.RoleWeights ?? DefaultWeights;
        double totalWeight = 0.0;
        double approvedWeight = 0.0;
        int approvedCount = 0;

        foreach (var vote in acceptedVotes)
        {
            double weight = weights.TryGetValue(vote.AgentRole, out var w) ? w : 1.0;
            totalWeight += weight;

            if (vote.Approved)
            {
                approvedWeight += weight;
                approvedCount++;
            }
        }

        double approvalRatio = acceptedVotes.Count > 0 ? (double)approvedCount / acceptedVotes.Count : 0.0;
        double weightedScore = totalWeight > 0 ? approvedWeight / totalWeight : 0.0;

        bool quorumMet = weightedScore >= policy.ApprovalThreshold;
        logs.Add($"Weighted score: {weightedScore:P1} (Threshold: {policy.ApprovalThreshold:P1}) | Raw approval ratio: {approvalRatio:P1} ({approvedCount}/{acceptedVotes.Count}). QuorumMet={quorumMet}.");

        var status = quorumMet ? ConsensusEvaluationStatus.Passed : ConsensusEvaluationStatus.QuorumNotReached;

        return new ConsensusEvaluationResult(
            Status: status,
            ApprovalRatio: approvalRatio,
            WeightedApprovalScore: weightedScore,
            QuorumMet: quorumMet,
            HasVeto: false,
            VetoingAgent: null,
            VetoReason: null,
            AcceptedVotes: acceptedVotes,
            QuarantinedVotes: quarantinedVotes,
            DeliberationLogs: logs,
            EvaluatedAt: now
        );
    }
}
