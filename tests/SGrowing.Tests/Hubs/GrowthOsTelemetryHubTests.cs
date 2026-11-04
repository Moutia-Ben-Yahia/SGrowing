using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using SGrowing.Api.Hubs;
using Xunit;

namespace SGrowing.Tests.Hubs;

public class GrowthOsTelemetryHubTests
{
    private readonly Mock<IHubContext<GrowthOsTelemetryHub, IGrowthOsTelemetryClient>> _mockHubContext;
    private readonly Mock<IHubClients<IGrowthOsTelemetryClient>> _mockClients;
    private readonly Mock<IGrowthOsTelemetryClient> _mockClient;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly Mock<HubCallerContext> _mockCallerContext;
    private readonly Mock<IHubCallerClients<IGrowthOsTelemetryClient>> _mockCallerClients;
    private readonly GrowthOsHubBroadcaster _broadcaster;
    private readonly GrowthOsTelemetryHub _hub;
    private const string ConnectionId = "conn_telemetry_test_8819";

    public GrowthOsTelemetryHubTests()
    {
        _mockHubContext = new Mock<IHubContext<GrowthOsTelemetryHub, IGrowthOsTelemetryClient>>();
        _mockClients = new Mock<IHubClients<IGrowthOsTelemetryClient>>();
        _mockClient = new Mock<IGrowthOsTelemetryClient>();

        _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
        _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClient.Object);

        // Broadcaster directly instantiated with mocked IHubContext without BuildServiceProvider()
        _broadcaster = new GrowthOsHubBroadcaster(_mockHubContext.Object);

        // Hub setup for direct unit testing of client lifecycle methods
        _mockGroups = new Mock<IGroupManager>();
        _mockCallerContext = new Mock<HubCallerContext>();
        _mockCallerClients = new Mock<IHubCallerClients<IGrowthOsTelemetryClient>>();

        _mockCallerContext.Setup(c => c.ConnectionId).Returns(ConnectionId);

        _hub = new GrowthOsTelemetryHub
        {
            Clients = _mockCallerClients.Object,
            Groups = _mockGroups.Object,
            Context = _mockCallerContext.Object
        };
    }

    [Fact]
    public async Task BroadcastIdentityStitchedAsync_ShouldDispatchToTenantGroup()
    {
        const string tenantId = "tenant_alpha_101";
        const string canonicalProfileId = "canon_prof_uuid_0099";
        const bool wasMerged = true;
        const string auditMessage = "Matched existing master identity graph on normalized E.164 phone.";
        var expectedGroup = GrowthOsTelemetryHub.GetTenantGroup(tenantId);

        await _broadcaster.BroadcastIdentityStitchedAsync(tenantId, canonicalProfileId, wasMerged, auditMessage);

        _mockClients.Verify(c => c.Group(expectedGroup), Times.Once);
        _mockClient.Verify(c => c.ReceiveIdentityStitched(tenantId, canonicalProfileId, wasMerged, auditMessage), Times.Once);
    }

    [Fact]
    public async Task BroadcastCapiConversionAsync_ShouldFormatPayloadCorrectly()
    {
        const string tenantId = "tenant_ecommerce_202";
        const string platform = "Meta_CAPI";
        const string eventId = "capi_purchase_ev_99182";
        const decimal value = 349.50m;
        const bool isDeduplicated = true;
        var expectedGroup = GrowthOsTelemetryHub.GetTenantGroup(tenantId);

        await _broadcaster.BroadcastCapiConversionAsync(tenantId, platform, eventId, value, isDeduplicated);

        _mockClients.Verify(c => c.Group(expectedGroup), Times.Once);
        _mockClient.Verify(c => c.ReceiveCapiConversion(tenantId, platform, eventId, value, isDeduplicated), Times.Once);
    }

    [Fact]
    public async Task BroadcastWorkflowStepAsync_ShouldSendStepLatencyAndNodeStatus()
    {
        const string tenantId = "tenant_workflow_303";
        const string workflowId = "wf_lead_enrichment_dag";
        const string nodeId = "node_waterfall_step_02";
        const string nodeName = "Clearbit & Apollo Waterfall Enrichment";
        const string status = "Success";
        const int latencyMs = 385;
        var expectedGroup = GrowthOsTelemetryHub.GetTenantGroup(tenantId);

        await _broadcaster.BroadcastWorkflowStepAsync(tenantId, workflowId, nodeId, nodeName, status, latencyMs);

        _mockClients.Verify(c => c.Group(expectedGroup), Times.Once);
        _mockClient.Verify(c => c.ReceiveWorkflowTelemetryStep(tenantId, workflowId, nodeId, nodeName, status, latencyMs), Times.Once);
    }

    [Fact]
    public async Task BroadcastRoasOptimizationAsync_ShouldCalculateScaledCounts()
    {
        const string tenantId = "tenant_adspend_404";
        const decimal blendedRoas = 4.82m;
        const int scaledCampaignsCount = 14;
        var expectedGroup = GrowthOsTelemetryHub.GetTenantGroup(tenantId);

        await _broadcaster.BroadcastRoasOptimizationAsync(tenantId, blendedRoas, scaledCampaignsCount);

        _mockClients.Verify(c => c.Group(expectedGroup), Times.Once);
        _mockClient.Verify(c => c.ReceiveRoasBudgetOptimized(tenantId, blendedRoas, scaledCampaignsCount), Times.Once);
    }

    [Fact]
    public async Task JoinTenantGroup_ShouldAddConnectionToGroup()
    {
        const string tenantId = "tenant_corp_505";
        var expectedGroup = GrowthOsTelemetryHub.GetTenantGroup(tenantId);

        await _hub.JoinTenantGroup(tenantId);

        _mockGroups.Verify(g => g.AddToGroupAsync(ConnectionId, expectedGroup, default), Times.Once);
    }

    [Fact]
    public async Task LeaveTenantGroup_ShouldRemoveConnectionFromGroup()
    {
        const string tenantId = "tenant_corp_505";
        var expectedGroup = GrowthOsTelemetryHub.GetTenantGroup(tenantId);

        await _hub.LeaveTenantGroup(tenantId);

        _mockGroups.Verify(g => g.RemoveFromGroupAsync(ConnectionId, expectedGroup, default), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task JoinTenantGroup_NullOrWhitespaceTenantId_DoesNotAddToGroup(string? invalidTenantId)
    {
        await _hub.JoinTenantGroup(invalidTenantId!);

        _mockGroups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LeaveTenantGroup_NullOrWhitespaceTenantId_DoesNotRemoveFromGroup(string? invalidTenantId)
    {
        await _hub.LeaveTenantGroup(invalidTenantId!);

        _mockGroups.Verify(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }
}
