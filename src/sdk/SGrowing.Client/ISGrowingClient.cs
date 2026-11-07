using SGrowing.Client.Models;

namespace SGrowing.Client;

/// <summary>
/// Official client interface for the SGrowing Enterprise Growth OS platform.
/// Provides strongly typed access to AI Missions, E-Commerce Connectors, Developer Webhooks, and SRE Telemetry.
/// </summary>
public interface ISGrowingClient
{
    // --- AI Autonomous Missions ---

    /// <summary>
    /// Plans a strategic growth mission by decomposing business objectives into an autonomous task DAG.
    /// </summary>
    Task<AgentMissionPlan> PlanMissionAsync(MissionObjectiveRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances DAG execution for a mission, optionally granting approval for a gated task node.
    /// </summary>
    Task<AgentMissionPlan> StepMissionAsync(Guid missionId, string? approveTaskId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the status, execution metadata, and task DAG of a specific agent mission.
    /// </summary>
    Task<AgentMissionPlan?> GetMissionAsync(Guid missionId, CancellationToken cancellationToken = default);

    // --- E-Commerce Connectors & Marketplace ---

    /// <summary>
    /// Normalizes and transforms an external platform order payload into canonical SGrowing format.
    /// </summary>
    Task<TransformPayloadResult> TransformOrderAsync(
        string connectorId,
        string rawPayload,
        Dictionary<string, string>? customMappings = null,
        CancellationToken cancellationToken = default);

    // --- Developer Webhooks ---

    /// <summary>
    /// Dispatches a synthetic test webhook event to registered endpoints for the configured tenant.
    /// </summary>
    Task<TestDispatchWebhookResponse> DispatchTestWebhookAsync(
        string eventType,
        object payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cryptographically validates an incoming webhook signature using timing-safe HMAC-SHA256 comparison.
    /// </summary>
    bool VerifyWebhookSignature(
        string rawBody,
        string secret,
        string signatureHeader,
        double toleranceSeconds = 300,
        string? timestampHeader = null);

    // --- SRE Health Radar & Telemetry ---

    /// <summary>
    /// Retrieves the platform SRE Health Radar status including SLO error budget and latency percentiles.
    /// </summary>
    Task<SreHealthRadarReport> GetSreHealthRadarAsync(
        int dlqDepth = 0,
        int activeMissions = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a synthetic metric to exercise the OpenTelemetry distributed telemetry pipeline.
    /// </summary>
    Task<bool> RecordSyntheticMetricAsync(
        RecordSyntheticMetricRequest request,
        CancellationToken cancellationToken = default);
}
