using Microsoft.AspNetCore.SignalR;

namespace SGrowing.Api.Hubs;

/// <summary>
/// Strongly-typed client contract for real-time Growth OS telemetry,
/// including identity stitching, CAPI events, visual workflow telemetry, and ROAS budget optimization.
/// </summary>
public interface IGrowthOsTelemetryClient
{
    Task ReceiveIdentityStitched(string tenantId, string canonicalProfileId, bool wasMerged, string auditMessage);
    Task ReceiveCapiConversion(string tenantId, string platform, string eventId, decimal value, bool isDeduplicated);
    Task ReceiveWorkflowTelemetryStep(string tenantId, string workflowId, string nodeId, string nodeName, string status, int latencyMs);
    Task ReceiveRoasBudgetOptimized(string tenantId, decimal blendedRoas, int scaledCampaignsCount);
}

/// <summary>
/// SignalR Hub for real-time Growth OS telemetry streaming.
/// Clients join tenant-specific groups to receive filtered telemetry broadcasts.
/// </summary>
public class GrowthOsTelemetryHub : Hub<IGrowthOsTelemetryClient>
{
    public async Task JoinTenantGroup(string tenantId)
    {
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetTenantGroup(tenantId));
        }
    }

    public async Task LeaveTenantGroup(string tenantId)
    {
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetTenantGroup(tenantId));
        }
    }

    public static string GetTenantGroup(string tenantId) => $"tenant_{tenantId}";
}
