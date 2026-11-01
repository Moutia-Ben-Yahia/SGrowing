using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Agents.Swarm;

public record CreateSwarmSessionRequest(
    string InitialContext,
    SwarmProposedAction? ProposedAction = null,
    Guid? TenantId = null
);

public record DeliberateStepRequest(
    string AgentRole,
    string StepType,
    string Content,
    double? ConfidenceScore = null,
    IReadOnlyDictionary<string, string>? Metadata = null
);

public record SubmitAgentVoteRequest(
    string AgentRole,
    bool Approved,
    double ConfidenceScore,
    string Rationale,
    double ReasoningEntropy = 0.5,
    IReadOnlyDictionary<string, string>? Metadata = null
);

public record AdvanceStageRequest(
    SwarmStage TargetStage,
    string InitiatedBy = "User"
);

public record VerifyExecutionTokenRequest(
    ConsensusExecutionToken Token,
    IReadOnlyList<AgentVote> Votes,
    string TenantSecret
);

public static class MultiAgentSwarmEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? bodyTenantId, Guid? queryTenantId, Guid? headerTenantId)
    {
        if (bodyTenantId.HasValue && bodyTenantId.Value != Guid.Empty) return bodyTenantId.Value;
        if (queryTenantId.HasValue && queryTenantId.Value != Guid.Empty) return queryTenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    /// <summary>
    /// Registers multi-agent swarm consensus and handoff services into DI.
    /// </summary>
    public static IServiceCollection AddMultiAgentSwarmServices(this IServiceCollection services)
    {
        services.AddSingleton<IAgentConsensusEngine, AgentConsensusEngine>();
        services.AddSingleton<ICryptographicQuorumSigner, CryptographicQuorumSigner>();
        services.AddSingleton<IAgentHandoffProtocolService, AgentHandoffProtocolService>();
        return services;
    }

    /// <summary>
    /// Maps REST endpoints for the Autonomous Multi-Agent Consensus Swarm &amp; Handoff Protocol (MASP).
    /// </summary>
    public static IEndpointRouteBuilder MapMultiAgentSwarmEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/agents/swarm")
            .WithTags("Autonomous Multi-Agent Consensus Swarm & Handoff Protocol (MASP)");

        // 1. Create Swarm Session
        group.MapPost("/session/create", async (
            [FromBody] CreateSwarmSessionRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? queryTenantId,
            IAgentHandoffProtocolService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.InitialContext))
            {
                return Results.BadRequest(new { error = "InitialContext cannot be empty." });
            }

            var effectiveTenantId = ResolveTenantId(req.TenantId, queryTenantId, headerTenantId);
            var snapshot = await service.CreateSessionAsync(effectiveTenantId, req.InitialContext, req.ProposedAction, ct);
            return Results.Created($"/api/v1/agents/swarm/session/{snapshot.SessionId}", snapshot);
        })
        .WithName("CreateSwarmSession")
        .WithSummary("Initializes a new multi-agent deliberative blackboard session.");

        // 2. Get Swarm Session Snapshot
        group.MapGet("/session/{sessionId:guid}", (
            Guid sessionId,
            IAgentHandoffProtocolService service) =>
        {
            var snapshot = service.GetSessionSnapshot(sessionId);
            return snapshot is not null
                ? Results.Ok(snapshot)
                : Results.NotFound(new { error = $"Session {sessionId} not found." });
        })
        .WithName("GetSwarmSession")
        .WithSummary("Retrieves current blackboard state snapshot for a session.");

        // 3. Post Deliberation Step
        group.MapPost("/session/{sessionId:guid}/deliberate", async (
            Guid sessionId,
            [FromBody] DeliberateStepRequest req,
            IAgentHandoffProtocolService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.AgentRole) || string.IsNullOrWhiteSpace(req.Content))
            {
                return Results.BadRequest(new { error = "AgentRole and Content cannot be empty." });
            }

            try
            {
                var step = new DeliberationStepRecord(
                    Guid.NewGuid(),
                    req.AgentRole,
                    req.StepType ?? "Argument",
                    req.Content,
                    DateTimeOffset.UtcNow,
                    req.ConfidenceScore,
                    req.Metadata
                );

                var snapshot = await service.DeliberateAsync(sessionId, step, ct);
                return Results.Ok(snapshot);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("DeliberateSwarmStep")
        .WithSummary("Appends an immutable deliberation record into the session blackboard.");

        // 4. Submit Agent Vote
        group.MapPost("/session/{sessionId:guid}/vote", async (
            Guid sessionId,
            [FromBody] SubmitAgentVoteRequest req,
            IAgentHandoffProtocolService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.AgentRole) || string.IsNullOrWhiteSpace(req.Rationale))
            {
                return Results.BadRequest(new { error = "AgentRole and Rationale cannot be empty." });
            }

            try
            {
                var vote = new AgentVote(
                    AgentRole: req.AgentRole,
                    Approved: req.Approved,
                    ConfidenceScore: req.ConfidenceScore,
                    Rationale: req.Rationale,
                    ReasoningEntropy: req.ReasoningEntropy,
                    VotedAt: DateTimeOffset.UtcNow,
                    Metadata: req.Metadata
                );

                var snapshot = await service.SubmitVoteAsync(sessionId, vote, ct);
                return Results.Ok(snapshot);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SubmitSwarmVote")
        .WithSummary("Submits an agent vote towards quorum deliberation.");

        // 5. Evaluate Consensus and Progress
        group.MapGet("/session/{sessionId:guid}/consensus", async (
            Guid sessionId,
            [FromQuery] string? tenantSecret,
            [FromQuery] double? approvalThreshold,
            IAgentHandoffProtocolService service,
            CancellationToken ct) =>
        {
            try
            {
                QuorumPolicy? policy = approvalThreshold.HasValue
                    ? new QuorumPolicy(ApprovalThreshold: approvalThreshold.Value)
                    : null;

                var outcome = await service.EvaluateConsensusAndProgressAsync(sessionId, tenantSecret, policy, ct);
                return Results.Ok(outcome);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("EvaluateSwarmConsensus")
        .WithSummary("Evaluates quorum votes, enforces Byzantine checks and vetoes, and generates signed execution token or HITL escalation.");

        // 6. Verify Execution Token
        group.MapPost("/token/verify", (
            [FromBody] VerifyExecutionTokenRequest req,
            IAgentHandoffProtocolService service) =>
        {
            if (req.Token is null)
            {
                return Results.BadRequest(new { error = "Execution token cannot be null." });
            }

            var result = service.VerifyExecutionToken(req.Token, req.Votes, req.TenantSecret);
            return Results.Ok(result);
        })
        .WithName("VerifySwarmExecutionToken")
        .WithSummary("Cryptographically verifies an execution token against Merkle leaf votes and tenant signing key.");

        return app;
    }
}
