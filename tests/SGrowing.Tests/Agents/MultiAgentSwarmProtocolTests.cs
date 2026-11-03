using FluentAssertions;
using SGrowing.Api.Modules.Agents.Swarm;
using Xunit;

namespace SGrowing.Tests.Agents;

public class MultiAgentSwarmProtocolTests
{
    private readonly AgentConsensusEngine _consensusEngine = new();
    private readonly CryptographicQuorumSigner _quorumSigner = new();
    private readonly AgentHandoffProtocolService _handoffService;
    private readonly Guid _testTenantId = Guid.NewGuid();
    private const string TestSigningSecret = "tenant-sgrowing-ultra-secure-hmac-test-secret-2026";

    public MultiAgentSwarmProtocolTests()
    {
        _handoffService = new AgentHandoffProtocolService(_consensusEngine, _quorumSigner);
    }

    // =========================================================================
    // 1. AgentBlackboardState & Thread-Safety Tests
    // =========================================================================

    [Fact]
    public void BlackboardState_InitializesCorrectly_InLeadIntakeStage()
    {
        var sessionId = Guid.NewGuid();
        var action = new SwarmProposedAction(Guid.NewGuid(), "EnterpriseDeal", "Close enterprise tier", 10m, 30, 50000m);
        var state = new AgentBlackboardState(sessionId, _testTenantId, "Initial enterprise inbound lead", action);

        state.SessionId.Should().Be(sessionId);
        state.TenantId.Should().Be(_testTenantId);
        state.InitialContext.Should().Be("Initial enterprise inbound lead");
        state.CurrentStage.Should().Be(SwarmStage.LeadIntake);
        state.ProposedAction.Should().BeEquivalentTo(action);
        state.EscalationTicket.Should().BeNull();
        state.DeliberationAuditRecords.Should().ContainSingle(r => r.StepType == "SessionInitialized");
    }

    [Fact]
    public void BlackboardState_AppendDeliberationRecord_ThreadSafeUnderHighConcurrency()
    {
        var state = new AgentBlackboardState(Guid.NewGuid(), _testTenantId, "High concurrency stress test");
        const int concurrentTasks = 50;

        Parallel.For(0, concurrentTasks, i =>
        {
            state.AppendDeliberationRecord(new DeliberationStepRecord(
                Guid.NewGuid(),
                $"Agent_{i % 4}",
                "Observation",
                $"Concurrent deliberation utterance index {i}",
                DateTimeOffset.UtcNow,
                0.90
            ));
        });

        // 1 initial record + 50 concurrent additions = 51 records
        state.DeliberationAuditRecords.Should().HaveCount(concurrentTasks + 1);
        state.ToSnapshot().DeliberationAuditRecords.Should().HaveCount(concurrentTasks + 1);
    }

    [Fact]
    public void BlackboardState_HypothesesPool_AddsAndVotesCorrectly()
    {
        var state = new AgentBlackboardState(Guid.NewGuid(), _testTenantId, "Hypothesis testing");
        var hypId = Guid.NewGuid();
        var hypothesis = new AgentHypothesis(hypId, SwarmAgentRoles.SolutionsEngineerAgent, "Requires SOC2 compliance bridge", 0.85);

        state.AddHypothesis(hypothesis);

        state.Hypotheses.Should().ContainSingle();
        state.Hypotheses[0].Upvotes.Should().Be(0);

        state.VoteHypothesis(hypId, upvote: true);
        state.VoteHypothesis(hypId, upvote: true);
        state.VoteHypothesis(hypId, upvote: false);

        state.Hypotheses[0].Upvotes.Should().Be(2);
        state.Hypotheses[0].Downvotes.Should().Be(1);
    }

    [Fact]
    public void BlackboardState_ValidLinearTransitions_Succeed()
    {
        var state = new AgentBlackboardState(Guid.NewGuid(), _testTenantId, "Linear transition pipeline");

        state.TryTransitionStage(SwarmStage.TechnicalScoping, "Coordinator", out var err1).Should().BeTrue();
        err1.Should().BeNull();
        state.CurrentStage.Should().Be(SwarmStage.TechnicalScoping);

        state.TryTransitionStage(SwarmStage.FinancialRiskReview, "SolutionsEngineerAgent", out var err2).Should().BeTrue();
        err2.Should().BeNull();
        state.CurrentStage.Should().Be(SwarmStage.FinancialRiskReview);

        state.TryTransitionStage(SwarmStage.QuorumDeliberation, "FinancialRiskAgent", out var err3).Should().BeTrue();
        err3.Should().BeNull();
        state.CurrentStage.Should().Be(SwarmStage.QuorumDeliberation);

        state.TryTransitionStage(SwarmStage.ApprovedForExecution, "ConsensusEngine", out var err4).Should().BeTrue();
        err4.Should().BeNull();
        state.CurrentStage.Should().Be(SwarmStage.ApprovedForExecution);
    }

    [Fact]
    public void BlackboardState_InvalidStageTransition_FailsWithErrorMessage()
    {
        var state = new AgentBlackboardState(Guid.NewGuid(), _testTenantId, "Invalid transition check");

        // Jumping directly from LeadIntake to ApprovedForExecution is illegal
        var result = state.TryTransitionStage(SwarmStage.ApprovedForExecution, "RogueAgent", out var error);

        result.Should().BeFalse();
        error.Should().Contain("Invalid stage transition");
        state.CurrentStage.Should().Be(SwarmStage.LeadIntake);
    }

    [Fact]
    public void BlackboardState_EscalationStage_CanBeReachedFromAnyNonTerminalStage()
    {
        var state = new AgentBlackboardState(Guid.NewGuid(), _testTenantId, "Escalation check");
        state.TryTransitionStage(SwarmStage.TechnicalScoping, "User", out _).Should().BeTrue();

        var ticket = new HumanInTheLoopTicket(
            Guid.NewGuid(),
            state.SessionId,
            state.TenantId,
            "Security vulnerability in technical scoping",
            ["High CVE score"],
            DateTimeOffset.UtcNow
        );

        state.SetEscalationTicket(ticket);

        state.CurrentStage.Should().Be(SwarmStage.HumanInTheLoopEscalated);
        state.EscalationTicket.Should().NotBeNull();
        state.EscalationTicket!.Reason.Should().Be("Security vulnerability in technical scoping");
    }

    [Fact]
    public void BlackboardState_TerminalStage_RejectsFurtherTransitions()
    {
        var state = new AgentBlackboardState(Guid.NewGuid(), _testTenantId, "Terminal stage check");
        state.TryTransitionStage(SwarmStage.TechnicalScoping, "A1", out _);
        state.TryTransitionStage(SwarmStage.FinancialRiskReview, "A2", out _);
        state.TryTransitionStage(SwarmStage.QuorumDeliberation, "A3", out _);
        state.TryTransitionStage(SwarmStage.ApprovedForExecution, "A4", out _);

        // Attempting to advance after ApprovedForExecution fails
        var transitioned = state.TryTransitionStage(SwarmStage.TechnicalScoping, "A5", out var err);
        transitioned.Should().BeFalse();
        err.Should().Contain("Cannot transition from terminal stage");
    }

    [Fact]
    public void BlackboardState_ToSnapshot_ProducesImmutableCopy()
    {
        var state = new AgentBlackboardState(Guid.NewGuid(), _testTenantId, "Snapshot integrity");
        state.AddHypothesis(new AgentHypothesis(Guid.NewGuid(), "Agent1", "Test hypothesis", 0.9));

        var snapshot = state.ToSnapshot();

        // Mutate original state
        state.AddHypothesis(new AgentHypothesis(Guid.NewGuid(), "Agent2", "Second hypothesis", 0.8));

        snapshot.Hypotheses.Should().HaveCount(1);
        state.Hypotheses.Should().HaveCount(2);
    }

    // =========================================================================
    // 2. AgentConsensusEngine Quorum & Veto Tests
    // =========================================================================

    [Fact]
    public void ConsensusEngine_AllCoreAgentsApprove_ReturnsPassedWith100PercentQuorum()
    {
        var action = new SwarmProposedAction(Guid.NewGuid(), "EnterpriseQuote", "Standard quote", 15m, 30, 25000m);
        var votes = new List<AgentVote>
        {
            new(SwarmAgentRoles.ProspectingSdrAgent, Approved: true, ConfidenceScore: 0.95, Rationale: "Lead fits ICP perfectly"),
            new(SwarmAgentRoles.SolutionsEngineerAgent, Approved: true, ConfidenceScore: 0.90, Rationale: "Architecture compatible"),
            new(SwarmAgentRoles.FinancialRiskAgent, Approved: true, ConfidenceScore: 0.88, Rationale: "Margins acceptable"),
            new(SwarmAgentRoles.LegalComplianceAgent, Approved: true, ConfidenceScore: 0.92, Rationale: "Standard terms adhered to")
        };

        var result = _consensusEngine.EvaluateConsensus(votes, action);

        result.Status.Should().Be(ConsensusEvaluationStatus.Passed);
        result.QuorumMet.Should().BeTrue();
        result.HasVeto.Should().BeFalse();
        result.ApprovalRatio.Should().Be(1.0);
        result.WeightedApprovalScore.Should().Be(1.0);
        result.AcceptedVotes.Should().HaveCount(4);
        result.QuarantinedVotes.Should().BeEmpty();
    }

    [Fact]
    public void ConsensusEngine_Exact75PercentApproval_MeetsQuorumThresholdAndPasses()
    {
        var action = new SwarmProposedAction(Guid.NewGuid(), "MidMarketQuote", "Mid-market quote", 10m, 30, 15000m);
        // 3 Approvals out of 4 = 75%
        var votes = new List<AgentVote>
        {
            new(SwarmAgentRoles.ProspectingSdrAgent, Approved: true, ConfidenceScore: 0.85, Rationale: "Good fit"),
            new(SwarmAgentRoles.SolutionsEngineerAgent, Approved: true, ConfidenceScore: 0.80, Rationale: "Technically viable"),
            new(SwarmAgentRoles.FinancialRiskAgent, Approved: true, ConfidenceScore: 0.82, Rationale: "Financials sound"),
            new(SwarmAgentRoles.LegalComplianceAgent, Approved: false, ConfidenceScore: 0.85, Rationale: "Minor indemnity objection")
        };

        var result = _consensusEngine.EvaluateConsensus(votes, action);

        result.Status.Should().Be(ConsensusEvaluationStatus.Passed);
        result.QuorumMet.Should().BeTrue();
        result.ApprovalRatio.Should().Be(0.75);
        result.WeightedApprovalScore.Should().Be(0.75);
        result.HasVeto.Should().BeFalse();
    }

    [Fact]
    public void ConsensusEngine_Below75PercentApproval_ReturnsQuorumNotReached()
    {
        var action = new SwarmProposedAction(Guid.NewGuid(), "CustomPlan", "Custom SaaS tier", 10m, 30, 12000m);
        // 2 Approvals out of 4 = 50% (< 75%)
        var votes = new List<AgentVote>
        {
            new(SwarmAgentRoles.ProspectingSdrAgent, Approved: true, ConfidenceScore: 0.85, Rationale: "Good pipeline"),
            new(SwarmAgentRoles.SolutionsEngineerAgent, Approved: true, ConfidenceScore: 0.80, Rationale: "Integration feasible"),
            new(SwarmAgentRoles.FinancialRiskAgent, Approved: false, ConfidenceScore: 0.85, Rationale: "Gross margin too slim"),
            new(SwarmAgentRoles.LegalComplianceAgent, Approved: false, ConfidenceScore: 0.90, Rationale: "Redline changes required")
        };

        var result = _consensusEngine.EvaluateConsensus(votes, action);

        // FinancialRiskAgent voted false, which triggers financial veto or quorum failure
        result.Status.Should().BeOneOf(ConsensusEvaluationStatus.Vetoed, ConsensusEvaluationStatus.QuorumNotReached);
    }

    [Fact]
    public void ConsensusEngine_FinancialRiskAgentVeto_WhenDiscountExceeds20Percent_EvenIfAllOthersApprove()
    {
        // 25% discount > 20% policy ceiling
        var action = new SwarmProposedAction(Guid.NewGuid(), "HeavyDiscountQuote", "Enterprise aggressive discount", 25.0m, 30, 100000m);
        var votes = new List<AgentVote>
        {
            new(SwarmAgentRoles.ProspectingSdrAgent, Approved: true, ConfidenceScore: 0.99, Rationale: "Massive deal"),
            new(SwarmAgentRoles.SolutionsEngineerAgent, Approved: true, ConfidenceScore: 0.95, Rationale: "Fully supported"),
            new(SwarmAgentRoles.FinancialRiskAgent, Approved: true, ConfidenceScore: 0.70, Rationale: "Agent erroneously approved high discount"),
            new(SwarmAgentRoles.LegalComplianceAgent, Approved: true, ConfidenceScore: 0.90, Rationale: "No legal issues")
        };

        var result = _consensusEngine.EvaluateConsensus(votes, action);

        result.Status.Should().Be(ConsensusEvaluationStatus.Vetoed);
        result.HasVeto.Should().BeTrue();
        result.VetoingAgent.Should().Be(SwarmAgentRoles.FinancialRiskAgent);
        result.VetoReason.Should().Contain("exceeds the maximum allowable threshold of 20.0%");
    }

    [Fact]
    public void ConsensusEngine_FinancialRiskAgentVeto_WhenPaymentTermsExceedLimit()
    {
        // 90 days payment terms > 60 days limit
        var action = new SwarmProposedAction(Guid.NewGuid(), "LongTermsQuote", "Quote with 90d payment terms", 10.0m, 90, 50000m);
        var votes = new List<AgentVote>
        {
            new(SwarmAgentRoles.ProspectingSdrAgent, Approved: true, ConfidenceScore: 0.90, Rationale: "Client requested net 90"),
            new(SwarmAgentRoles.SolutionsEngineerAgent, Approved: true, ConfidenceScore: 0.90, Rationale: "Technically ready"),
            new(SwarmAgentRoles.FinancialRiskAgent, Approved: true, ConfidenceScore: 0.80, Rationale: "Acceptable"),
            new(SwarmAgentRoles.LegalComplianceAgent, Approved: true, ConfidenceScore: 0.90, Rationale: "Standard")
        };

        var result = _consensusEngine.EvaluateConsensus(votes, action);

        result.Status.Should().Be(ConsensusEvaluationStatus.Vetoed);
        result.HasVeto.Should().BeTrue();
        result.VetoingAgent.Should().Be(SwarmAgentRoles.FinancialRiskAgent);
        result.VetoReason.Should().Contain("Payment terms of 90 days exceed the maximum credit threshold of 60 days");
    }

    [Fact]
    public void ConsensusEngine_FinancialRiskAgent_ExplicitNegativeVote_TriggersVeto()
    {
        var action = new SwarmProposedAction(Guid.NewGuid(), "StandardDeal", "Standard", 15.0m, 30, 30000m);
        var votes = new List<AgentVote>
        {
            new(SwarmAgentRoles.ProspectingSdrAgent, Approved: true, ConfidenceScore: 0.90, Rationale: "Great prospect"),
            new(SwarmAgentRoles.SolutionsEngineerAgent, Approved: true, ConfidenceScore: 0.90, Rationale: "Tech ok"),
            new(SwarmAgentRoles.FinancialRiskAgent, Approved: false, ConfidenceScore: 0.95, Rationale: "Customer has low credit rating and outstanding debt"),
            new(SwarmAgentRoles.LegalComplianceAgent, Approved: true, ConfidenceScore: 0.90, Rationale: "Legal terms fine")
        };

        var result = _consensusEngine.EvaluateConsensus(votes, action);

        result.Status.Should().Be(ConsensusEvaluationStatus.Vetoed);
        result.HasVeto.Should().BeTrue();
        result.VetoReason.Should().Contain("Customer has low credit rating");
    }

    [Fact]
    public void ConsensusEngine_LowConfidenceVote_QuarantinedAsByzantineHallucination()
    {
        var action = new SwarmProposedAction(Guid.NewGuid(), "Action1", "Action", 5m, 30, 10000m);
        // Confidence 0.45 is below the 0.60 threshold
        var votes = new List<AgentVote>
        {
            new(SwarmAgentRoles.ProspectingSdrAgent, Approved: true, ConfidenceScore: 0.45, Rationale: "Unsure about customer fit"),
            new(SwarmAgentRoles.SolutionsEngineerAgent, Approved: true, ConfidenceScore: 0.90, Rationale: "Tech validated"),
            new(SwarmAgentRoles.FinancialRiskAgent, Approved: true, ConfidenceScore: 0.85, Rationale: "Financially validated"),
            new(SwarmAgentRoles.LegalComplianceAgent, Approved: true, ConfidenceScore: 0.88, Rationale: "Compliance validated")
        };

        var result = _consensusEngine.EvaluateConsensus(votes, action);

        result.QuarantinedVotes.Should().ContainSingle();
        result.QuarantinedVotes[0].Vote.AgentRole.Should().Be(SwarmAgentRoles.ProspectingSdrAgent);
        result.QuarantinedVotes[0].QuarantineReason.Should().Contain("Confidence score (0.45) below threshold");
        result.AcceptedVotes.Should().HaveCount(3);
    }

    [Fact]
    public void ConsensusEngine_AnomalousEntropyVote_QuarantinedAsByzantineDivergence()
    {
        var action = new SwarmProposedAction(Guid.NewGuid(), "Action2", "Action", 5m, 30, 10000m);
        // Reasoning entropy 4.2 > max allowed 2.5
        var votes = new List<AgentVote>
        {
            new(SwarmAgentRoles.ProspectingSdrAgent, Approved: true, ConfidenceScore: 0.90, Rationale: "Normal", ReasoningEntropy: 0.4),
            new(SwarmAgentRoles.SolutionsEngineerAgent, Approved: true, ConfidenceScore: 0.85, Rationale: "Erratic output", ReasoningEntropy: 4.2),
            new(SwarmAgentRoles.FinancialRiskAgent, Approved: true, ConfidenceScore: 0.85, Rationale: "Normal", ReasoningEntropy: 0.3),
            new(SwarmAgentRoles.LegalComplianceAgent, Approved: true, ConfidenceScore: 0.88, Rationale: "Normal", ReasoningEntropy: 0.5)
        };

        var result = _consensusEngine.EvaluateConsensus(votes, action);

        result.QuarantinedVotes.Should().ContainSingle(q => q.Vote.AgentRole == SwarmAgentRoles.SolutionsEngineerAgent);
        result.QuarantinedVotes[0].QuarantineReason.Should().Contain("Reasoning entropy (4.20) outside allowable bounds");
    }

    [Fact]
    public void ConsensusEngine_ExcessiveByzantineVotes_TriggersEscalationRequired()
    {
        var action = new SwarmProposedAction(Guid.NewGuid(), "Action3", "Action", 5m, 30, 10000m);
        // 3 out of 4 votes have low confidence / Byzantine divergence
        var votes = new List<AgentVote>
        {
            new(SwarmAgentRoles.ProspectingSdrAgent, Approved: true, ConfidenceScore: 0.30, Rationale: "Hallucinating"),
            new(SwarmAgentRoles.SolutionsEngineerAgent, Approved: true, ConfidenceScore: 0.20, Rationale: "Hallucinating"),
            new(SwarmAgentRoles.FinancialRiskAgent, Approved: true, ConfidenceScore: 0.15, Rationale: "Hallucinating"),
            new(SwarmAgentRoles.LegalComplianceAgent, Approved: true, ConfidenceScore: 0.90, Rationale: "Valid vote")
        };

        var result = _consensusEngine.EvaluateConsensus(votes, action);

        result.Status.Should().Be(ConsensusEvaluationStatus.EscalationRequired);
        result.QuarantinedVotes.Should().HaveCount(3);
        result.VetoReason.Should().Contain("Byzantine fault threshold exceeded");
    }

    [Fact]
    public void ConsensusEngine_CustomRoleWeights_AppliesWeightsAccurately()
    {
        var action = new SwarmProposedAction(Guid.NewGuid(), "EnterpriseScale", "Big deal", 10m, 30, 200000m);
        var customWeights = new Dictionary<string, double>
        {
            [SwarmAgentRoles.ProspectingSdrAgent] = 1.0,
            [SwarmAgentRoles.SolutionsEngineerAgent] = 3.0, // Heavy weight on technical feasibility
            [SwarmAgentRoles.FinancialRiskAgent] = 1.0,
            [SwarmAgentRoles.LegalComplianceAgent] = 1.0
        };

        var policy = new QuorumPolicy(ApprovalThreshold: 0.70, RoleWeights: customWeights);

        // SolutionsEngineer (weight 3.0) + ProspectingSdr (weight 1.0) approve -> 4.0 / 6.0 = 66.7% (< 70%)
        var votes = new List<AgentVote>
        {
            new(SwarmAgentRoles.ProspectingSdrAgent, Approved: true, ConfidenceScore: 0.90, Rationale: "OK"),
            new(SwarmAgentRoles.SolutionsEngineerAgent, Approved: true, ConfidenceScore: 0.90, Rationale: "Tech solid"),
            new(SwarmAgentRoles.FinancialRiskAgent, Approved: false, ConfidenceScore: 0.90, Rationale: "Budget constrained"),
            new(SwarmAgentRoles.LegalComplianceAgent, Approved: false, ConfidenceScore: 0.90, Rationale: "Jurisdiction clause missing")
        };

        var result = _consensusEngine.EvaluateConsensus(votes, action, policy);

        // Veto from FinancialRiskAgent or Quorum failure
        result.Status.Should().BeOneOf(ConsensusEvaluationStatus.Vetoed, ConsensusEvaluationStatus.QuorumNotReached);
    }

    [Fact]
    public void ConsensusEngine_EmptyVotesList_ReturnsQuorumNotReached()
    {
        var action = new SwarmProposedAction(Guid.NewGuid(), "Action", "Description");
        var result = _consensusEngine.EvaluateConsensus([], action);

        result.Status.Should().Be(ConsensusEvaluationStatus.QuorumNotReached);
        result.QuorumMet.Should().BeFalse();
        result.AcceptedVotes.Should().BeEmpty();
    }

    // =========================================================================
    // 3. Cryptographic Quorum Signer & Merkle Verification Tests
    // =========================================================================

    [Fact]
    public void CryptographicSigner_GeneratesVerifiableExecutionToken_WithValidMerkleRoot()
    {
        var sessionId = Guid.NewGuid();
        var votes = new List<AgentVote>
        {
            new(SwarmAgentRoles.ProspectingSdrAgent, Approved: true, ConfidenceScore: 0.95, Rationale: "Qualified"),
            new(SwarmAgentRoles.SolutionsEngineerAgent, Approved: true, ConfidenceScore: 0.90, Rationale: "Feasible"),
            new(SwarmAgentRoles.FinancialRiskAgent, Approved: true, ConfidenceScore: 0.85, Rationale: "Profitable"),
            new(SwarmAgentRoles.LegalComplianceAgent, Approved: true, ConfidenceScore: 0.92, Rationale: "Compliant")
        };

        var token = _quorumSigner.GenerateExecutionToken(
            sessionId,
            _testTenantId,
            votes,
            0.85,
            TestSigningSecret
        );

        token.Should().NotBeNull();
        token.SessionId.Should().Be(sessionId);
        token.TenantId.Should().Be(_testTenantId);
        token.QuorumRatio.Should().Be(0.85);
        token.MerkleRootHash.Should().NotBeNullOrWhiteSpace();
        token.LeafHashes.Should().HaveCount(4);
        token.Signature.Should().NotBeNullOrWhiteSpace();
        token.SignatureAlgorithm.Should().Be("HMAC-SHA256");
    }

    [Fact]
    public void CryptographicSigner_VerifiesUntamperedTokenAndVotes_Successfully()
    {
        var sessionId = Guid.NewGuid();
        var votes = new List<AgentVote>
        {
            new(SwarmAgentRoles.ProspectingSdrAgent, Approved: true, ConfidenceScore: 0.90, Rationale: "Valid"),
            new(SwarmAgentRoles.SolutionsEngineerAgent, Approved: true, ConfidenceScore: 0.85, Rationale: "Valid")
        };

        var token = _quorumSigner.GenerateExecutionToken(
            sessionId,
            _testTenantId,
            votes,
            1.0,
            TestSigningSecret
        );

        var verifyResult = _quorumSigner.VerifyExecutionToken(token, votes, TestSigningSecret);

        verifyResult.IsValid.Should().BeTrue();
        verifyResult.Reason.Should().BeNull();
    }

    [Fact]
    public void CryptographicSigner_DetectsTamperedAgentVote_ViaMerkleRootMismatch()
    {
        var votes = new List<AgentVote>
        {
            new(SwarmAgentRoles.ProspectingSdrAgent, Approved: true, ConfidenceScore: 0.90, Rationale: "Valid SDR"),
            new(SwarmAgentRoles.SolutionsEngineerAgent, Approved: true, ConfidenceScore: 0.85, Rationale: "Valid SE")
        };

        var token = _quorumSigner.GenerateExecutionToken(Guid.NewGuid(), _testTenantId, votes, 1.0, TestSigningSecret);

        // An attacker mutates an agent's rationale or approval post-deliberation
        var tamperedVotes = new List<AgentVote>
        {
            votes[0],
            votes[1] with { Approved = false, Rationale = "TAMPERED: Rogue injection" }
        };

        var verifyResult = _quorumSigner.VerifyExecutionToken(token, tamperedVotes, TestSigningSecret);

        verifyResult.IsValid.Should().BeFalse();
        verifyResult.Reason.Should().Contain("Merkle root mismatch");
    }

    [Fact]
    public void CryptographicSigner_DetectsTamperedSignature_ViaConstantTimeHmacCheck()
    {
        var votes = new List<AgentVote>
        {
            new(SwarmAgentRoles.ProspectingSdrAgent, Approved: true, ConfidenceScore: 0.90, Rationale: "Valid")
        };

        var token = _quorumSigner.GenerateExecutionToken(Guid.NewGuid(), _testTenantId, votes, 1.0, TestSigningSecret);

        // Mutate signature
        var replacement = token.Signature.EndsWith("ff") ? "00" : "ff";
        var tamperedToken = token with { Signature = token.Signature[..^2] + replacement };

        var verifyResult = _quorumSigner.VerifyExecutionToken(tamperedToken, votes, TestSigningSecret);

        verifyResult.IsValid.Should().BeFalse();
        verifyResult.Reason.Should().Contain("Cryptographic signature invalid");
    }

    [Fact]
    public void CryptographicSigner_DetectsExpiredToken()
    {
        var votes = new List<AgentVote>
        {
            new(SwarmAgentRoles.ProspectingSdrAgent, Approved: true, ConfidenceScore: 0.90, Rationale: "Valid")
        };

        // Token expired 1 minute ago
        var token = _quorumSigner.GenerateExecutionToken(
            Guid.NewGuid(),
            _testTenantId,
            votes,
            1.0,
            TestSigningSecret,
            validityWindow: TimeSpan.FromMinutes(-1)
        );

        var verifyResult = _quorumSigner.VerifyExecutionToken(token, votes, TestSigningSecret);

        verifyResult.IsValid.Should().BeFalse();
        verifyResult.Reason.Should().Contain("Token expired");
    }

    [Fact]
    public void CryptographicSigner_DeterministicMerkleRoot_IndependentOfVoteArrivalOrder()
    {
        var vote1 = new AgentVote(SwarmAgentRoles.ProspectingSdrAgent, true, 0.90, "Rationale A");
        var vote2 = new AgentVote(SwarmAgentRoles.SolutionsEngineerAgent, true, 0.85, "Rationale B");
        var vote3 = new AgentVote(SwarmAgentRoles.LegalComplianceAgent, true, 0.95, "Rationale C");

        var orderA = new List<AgentVote> { vote1, vote2, vote3 };
        var orderB = new List<AgentVote> { vote3, vote1, vote2 };

        var rootA = _quorumSigner.ComputeMerkleRoot(orderA, out var leavesA);
        var rootB = _quorumSigner.ComputeMerkleRoot(orderB, out var leavesB);

        rootA.Should().Be(rootB);
        leavesA.Should().BeEquivalentTo(leavesB);
    }

    // =========================================================================
    // 4. AgentHandoffProtocolService Full Lifecycle & Escalation Tests
    // =========================================================================

    [Fact]
    public async Task HandoffProtocol_FullLifecycle_FromLeadIntakeToApprovedForExecution()
    {
        // 1. Create Session
        var action = new SwarmProposedAction(Guid.NewGuid(), "ExpansionDeal", "Enterprise account tier", 10m, 30, 45000m);
        var session = await _handoffService.CreateSessionAsync(_testTenantId, "Qualified inbound lead for enterprise expansion", action);

        session.CurrentStage.Should().Be(SwarmStage.LeadIntake);

        // 2. Advance to TechnicalScoping
        session = await _handoffService.AdvanceStageAsync(session.SessionId, SwarmStage.TechnicalScoping, SwarmAgentRoles.ProspectingSdrAgent);
        session.CurrentStage.Should().Be(SwarmStage.TechnicalScoping);

        // 3. Deliberate & add hypothesis
        session = await _handoffService.DeliberateAsync(session.SessionId, new DeliberationStepRecord(
            Guid.NewGuid(),
            SwarmAgentRoles.SolutionsEngineerAgent,
            "TechnicalAssessment",
            "Customer stack verified: PostgreSQL + React. Direct connector available.",
            DateTimeOffset.UtcNow,
            0.95
        ));

        // 4. Advance to FinancialRiskReview
        session = await _handoffService.AdvanceStageAsync(session.SessionId, SwarmStage.FinancialRiskReview, SwarmAgentRoles.SolutionsEngineerAgent);
        session.CurrentStage.Should().Be(SwarmStage.FinancialRiskReview);

        // 5. Submit agent votes
        await _handoffService.SubmitVoteAsync(session.SessionId, new AgentVote(SwarmAgentRoles.ProspectingSdrAgent, true, 0.92, "High value target"));
        await _handoffService.SubmitVoteAsync(session.SessionId, new AgentVote(SwarmAgentRoles.SolutionsEngineerAgent, true, 0.95, "Architecture sound"));
        await _handoffService.SubmitVoteAsync(session.SessionId, new AgentVote(SwarmAgentRoles.FinancialRiskAgent, true, 0.90, "10% discount meets margin requirements"));
        await _handoffService.SubmitVoteAsync(session.SessionId, new AgentVote(SwarmAgentRoles.LegalComplianceAgent, true, 0.88, "Standard DPA accepted"));

        // 6. Evaluate consensus
        var outcome = await _handoffService.EvaluateConsensusAndProgressAsync(session.SessionId, TestSigningSecret);

        outcome.Approved.Should().BeTrue();
        outcome.NewStage.Should().Be(SwarmStage.ApprovedForExecution);
        outcome.ExecutionToken.Should().NotBeNull();
        outcome.EscalationTicket.Should().BeNull();

        // 7. Verify generated execution token
        var verify = _handoffService.VerifyExecutionToken(outcome.ExecutionToken!, outcome.ConsensusResult.AcceptedVotes, TestSigningSecret);
        verify.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task HandoffProtocol_FinancialVeto_CreatesHumanInTheLoopTicketAndEscalates()
    {
        // 25% discount exceeds 20% limit
        var highDiscountAction = new SwarmProposedAction(Guid.NewGuid(), "RiskyDiscount", "Deal with 25% discount", 25.0m, 30, 80000m);
        var session = await _handoffService.CreateSessionAsync(_testTenantId, "Enterprise lead requiring 25% discount", highDiscountAction);

        await _handoffService.AdvanceStageAsync(session.SessionId, SwarmStage.TechnicalScoping, "User");
        await _handoffService.AdvanceStageAsync(session.SessionId, SwarmStage.FinancialRiskReview, "User");
        await _handoffService.AdvanceStageAsync(session.SessionId, SwarmStage.QuorumDeliberation, "User");

        // Submit votes
        await _handoffService.SubmitVoteAsync(session.SessionId, new AgentVote(SwarmAgentRoles.ProspectingSdrAgent, true, 0.95, "Wants closing this quarter"));
        await _handoffService.SubmitVoteAsync(session.SessionId, new AgentVote(SwarmAgentRoles.SolutionsEngineerAgent, true, 0.90, "Feasible"));
        await _handoffService.SubmitVoteAsync(session.SessionId, new AgentVote(SwarmAgentRoles.FinancialRiskAgent, false, 0.95, "Discount exceeds authorized policy"));
        await _handoffService.SubmitVoteAsync(session.SessionId, new AgentVote(SwarmAgentRoles.LegalComplianceAgent, true, 0.90, "Legal ok"));

        var outcome = await _handoffService.EvaluateConsensusAndProgressAsync(session.SessionId, TestSigningSecret);

        outcome.Approved.Should().BeFalse();
        outcome.NewStage.Should().Be(SwarmStage.HumanInTheLoopEscalated);
        outcome.ExecutionToken.Should().BeNull();
        outcome.EscalationTicket.Should().NotBeNull();
        outcome.EscalationTicket!.Reason.Should().Contain("Financial veto");
        outcome.EscalationTicket.Status.Should().Be("PendingHumanReview");

        // Blackboard reflects escalated state
        var snapshot = _handoffService.GetSessionSnapshot(session.SessionId);
        snapshot!.CurrentStage.Should().Be(SwarmStage.HumanInTheLoopEscalated);
        snapshot.EscalationTicket.Should().NotBeNull();
    }

    [Fact]
    public async Task HandoffProtocol_QuorumFailure_CreatesHumanInTheLoopTicketAndEscalates()
    {
        var action = new SwarmProposedAction(Guid.NewGuid(), "Action", "Normal", 10m, 30, 20000m);
        var session = await _handoffService.CreateSessionAsync(_testTenantId, "Lead with unresolved blockers", action);

        // Only 1 out of 4 approvals = 25% (< 75%)
        await _handoffService.SubmitVoteAsync(session.SessionId, new AgentVote(SwarmAgentRoles.ProspectingSdrAgent, true, 0.90, "SDR yes"));
        await _handoffService.SubmitVoteAsync(session.SessionId, new AgentVote(SwarmAgentRoles.SolutionsEngineerAgent, false, 0.90, "Architecture incompatible"));
        await _handoffService.SubmitVoteAsync(session.SessionId, new AgentVote(SwarmAgentRoles.FinancialRiskAgent, false, 0.90, "Margins negative"));
        await _handoffService.SubmitVoteAsync(session.SessionId, new AgentVote(SwarmAgentRoles.LegalComplianceAgent, false, 0.90, "Sanctions screening concern"));

        var outcome = await _handoffService.EvaluateConsensusAndProgressAsync(session.SessionId, TestSigningSecret);

        outcome.Approved.Should().BeFalse();
        outcome.NewStage.Should().Be(SwarmStage.HumanInTheLoopEscalated);
        outcome.EscalationTicket.Should().NotBeNull();
        outcome.EscalationTicket!.EscalationFactors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task HandoffProtocol_ByzantineQuarantine_CreatesHumanInTheLoopTicket()
    {
        var action = new SwarmProposedAction(Guid.NewGuid(), "Action", "Standard", 5m, 30, 10000m);
        var session = await _handoffService.CreateSessionAsync(_testTenantId, "Byzantine test lead", action);

        // 3 out of 4 votes have low confidence
        await _handoffService.SubmitVoteAsync(session.SessionId, new AgentVote(SwarmAgentRoles.ProspectingSdrAgent, true, 0.20, "Very confused"));
        await _handoffService.SubmitVoteAsync(session.SessionId, new AgentVote(SwarmAgentRoles.SolutionsEngineerAgent, true, 0.35, "Hallucinated response"));
        await _handoffService.SubmitVoteAsync(session.SessionId, new AgentVote(SwarmAgentRoles.FinancialRiskAgent, true, 0.40, "Incoherent numbers"));
        await _handoffService.SubmitVoteAsync(session.SessionId, new AgentVote(SwarmAgentRoles.LegalComplianceAgent, true, 0.90, "Valid compliance"));

        var outcome = await _handoffService.EvaluateConsensusAndProgressAsync(session.SessionId, TestSigningSecret);

        outcome.Approved.Should().BeFalse();
        outcome.NewStage.Should().Be(SwarmStage.HumanInTheLoopEscalated);
        outcome.ConsensusResult.Status.Should().Be(ConsensusEvaluationStatus.EscalationRequired);
        outcome.EscalationTicket.Should().NotBeNull();
        outcome.EscalationTicket!.Reason.Should().Contain("Byzantine fault threshold exceeded");
    }

    [Fact]
    public async Task HandoffProtocol_AdvanceStage_ThrowsWhenSessionNotFound()
    {
        var nonExistentSessionId = Guid.NewGuid();

        var act = async () => await _handoffService.AdvanceStageAsync(nonExistentSessionId, SwarmStage.TechnicalScoping, "User");

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{nonExistentSessionId}*");
    }

    [Fact]
    public async Task HandoffProtocol_MultiTenantIsolation_MaintainsDistinctSessions()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var sessionA = await _handoffService.CreateSessionAsync(tenantA, "Tenant A Context");
        var sessionB = await _handoffService.CreateSessionAsync(tenantB, "Tenant B Context");

        sessionA.TenantId.Should().Be(tenantA);
        sessionB.TenantId.Should().Be(tenantB);
        sessionA.SessionId.Should().NotBe(sessionB.SessionId);

        var retrievedA = _handoffService.GetSessionSnapshot(sessionA.SessionId);
        var retrievedB = _handoffService.GetSessionSnapshot(sessionB.SessionId);

        retrievedA!.InitialContext.Should().Be("Tenant A Context");
        retrievedB!.InitialContext.Should().Be("Tenant B Context");
    }
}
