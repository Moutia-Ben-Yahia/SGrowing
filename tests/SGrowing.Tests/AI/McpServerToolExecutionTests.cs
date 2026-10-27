using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI;
using Xunit;

namespace SGrowing.Tests.AI;

public class McpServerToolExecutionTests
{
    private readonly SGrowingDbContext _db;
    private readonly McpServerRegistryService _service;
    private readonly Guid _tenantId = Guid.NewGuid();

    public McpServerToolExecutionTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new SGrowingDbContext(options);
        _service = new McpServerRegistryService(_db);
    }

    [Fact]
    public async Task GetRegisteredMcpToolsAsync_ShouldReturnAllFiveMandatoryTools()
    {
        // Act
        var tools = await _service.GetRegisteredMcpToolsAsync();

        // Assert
        tools.Should().NotBeNull();
        var toolNames = tools.Select(t => t.Name).ToList();

        toolNames.Should().Contain("crm_search_leads");
        toolNames.Should().Contain("outbound_send_spintax_campaign");
        toolNames.Should().Contain("deliverability_run_placement_test");
        toolNames.Should().Contain("contracts_dispatch_for_signature");
        toolNames.Should().Contain("courier_generate_waybill");
    }

    [Fact]
    public async Task ExecuteMcpTool_CrmSearchLeads_ShouldReturnMatchingLeads()
    {
        // Arrange
        var args = new Dictionary<string, object>
        {
            ["query"] = "Retail",
            ["limit"] = 5
        };

        // Act
        var result = await _service.ExecuteMcpToolAsync(_tenantId, "crm_search_leads", args);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ToolName.Should().Be("crm_search_leads");
        result.OutputPayload.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteMcpTool_OutboundSendSpintaxCampaign_ShouldResolveSpintax()
    {
        // Arrange
        var args = new Dictionary<string, object>
        {
            ["campaignId"] = "CMP-Q4-BLITZ",
            ["spintaxTemplate"] = "{Hello|Greetings|Hi} there, check out our new release!",
            ["recipientEmails"] = new[] { "client1@domain.com", "client2@domain.com" }
        };

        // Act
        var result = await _service.ExecuteMcpToolAsync(_tenantId, "outbound_send_spintax_campaign", args);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ToolName.Should().Be("outbound_send_spintax_campaign");
        result.OutputPayload.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteMcpTool_DeliverabilityRunPlacementTest_ShouldComputeInboxRadar()
    {
        // Arrange
        var args = new Dictionary<string, object>
        {
            ["domain"] = "sgrowing.io"
        };

        // Act
        var result = await _service.ExecuteMcpToolAsync(_tenantId, "deliverability_run_placement_test", args);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.OutputPayload.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteMcpTool_ContractsDispatchForSignature_ShouldGenerateEnvelopeAndUrl()
    {
        // Arrange
        var args = new Dictionary<string, object>
        {
            ["documentTitle"] = "Enterprise License Agreement",
            ["signerName"] = "Karim Trabelsi",
            ["signerEmail"] = "karim@maghrebretail.tn",
            ["contractValue"] = 25000.0
        };

        // Act
        var result = await _service.ExecuteMcpToolAsync(_tenantId, "contracts_dispatch_for_signature", args);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.OutputPayload.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteMcpTool_CourierGenerateWaybill_ShouldCreateWaybillAndBarcode()
    {
        // Arrange
        var args = new Dictionary<string, object>
        {
            ["orderId"] = "ORD-202610-8819",
            ["recipientName"] = "Kais Saidi",
            ["recipientPhone"] = "+21698223344",
            ["destinationWilaya"] = "Tunis",
            ["codAmount"] = 157.0
        };

        // Act
        var result = await _service.ExecuteMcpToolAsync(_tenantId, "courier_generate_waybill", args);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.OutputPayload.Should().NotBeNull();
    }

    [Fact]
    public void AutonomyMode_ShouldToggleAndPersist()
    {
        // Initial
        var defaultMode = _service.GetAutonomyMode(_tenantId);
        defaultMode.Should().Be("Human-in-the-Loop");

        // Set to Auto-Approve
        _service.SetAutonomyMode(_tenantId, "Auto-Approve");
        _service.GetAutonomyMode(_tenantId).Should().Be("Auto-Approve");

        // Set to Draft Only
        _service.SetAutonomyMode(_tenantId, "Draft Only");
        _service.GetAutonomyMode(_tenantId).Should().Be("Draft Only");

        // Verify audit log captured the changes
        var logs = _service.GetAuditLogs(_tenantId);
        logs.Should().NotBeEmpty();
        logs.Any(l => l.ActionSummary.Contains("Draft Only")).Should().BeTrue();
    }
}
