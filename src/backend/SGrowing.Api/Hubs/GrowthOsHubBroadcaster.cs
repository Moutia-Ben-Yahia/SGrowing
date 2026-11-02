using Microsoft.AspNetCore.SignalR;

namespace SGrowing.Api.Hubs;

/// <summary>
/// Broadcaster interface for dispatching real-time growth telemetry across tenant groups.
/// </summary>
public interface IGrowthOsHubBroadcaster
{
    Task BroadcastIdentityStitchedAsync(string tenantId, string canonicalProfileId, bool wasMerged, string auditMessage);
    Task BroadcastCapiConversionAsync(string tenantId, string platform, string eventId, decimal value, bool isDeduplicated);
    Task BroadcastWorkflowStepAsync(string tenantId, string workflowId, string nodeId, string nodeName, string status, int latencyMs);
    Task BroadcastRoasOptimizationAsync(string tenantId, decimal blendedRoas, int scaledCampaignsCount);
}

/// <summary>
/// Implementation of IGrowthOsHubBroadcaster utilizing SignalR HubContext.
/// </summary>
public class GrowthOsHubBroadcaster : IGrowthOsHubBroadcaster
{
    private readonly IHubContext<GrowthOsTelemetryHub, IGrowthOsTelemetryClient> _hubContext;

    public GrowthOsHubBroadcaster(IHubContext<GrowthOsTelemetryHub, IGrowthOsTelemetryClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task BroadcastIdentityStitchedAsync(string tenantId, string canonicalProfileId, bool wasMerged, string auditMessage)
    {
        var group = GrowthOsTelemetryHub.GetTenantGroup(tenantId);
        await _hubContext.Clients.Group(group)
            .ReceiveIdentityStitched(tenantId, canonicalProfileId, wasMerged, auditMessage);
    }

    public async Task BroadcastCapiConversionAsync(string tenantId, string platform, string eventId, decimal value, bool isDeduplicated)
    {
        var group = GrowthOsTelemetryHub.GetTenantGroup(tenantId);
        await _hubContext.Clients.Group(group)
            .ReceiveCapiConversion(tenantId, platform, eventId, value, isDeduplicated);
    }

    public async Task BroadcastWorkflowStepAsync(string tenantId, string workflowId, string nodeId, string nodeName, string status, int latencyMs)
    {
        var group = GrowthOsTelemetryHub.GetTenantGroup(tenantId);
        await _hubContext.Clients.Group(group)
            .ReceiveWorkflowTelemetryStep(tenantId, workflowId, nodeId, nodeName, status, latencyMs);
    }

    public async Task BroadcastRoasOptimizationAsync(string tenantId, decimal blendedRoas, int scaledCampaignsCount)
    {
        var group = GrowthOsTelemetryHub.GetTenantGroup(tenantId);
        await _hubContext.Clients.Group(group)
            .ReceiveRoasBudgetOptimized(tenantId, blendedRoas, scaledCampaignsCount);
    }
}
