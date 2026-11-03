using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.AI;

public sealed class DynamicAgentToolRegistryTests
{
    private readonly SGrowingDbContext _db;
    private readonly DynamicAgentToolRegistry _registry;

    public DynamicAgentToolRegistryTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _registry = new DynamicAgentToolRegistry(_db, NullLogger<DynamicAgentToolRegistry>.Instance);
    }

    [Fact]
    public async Task RegisterToolAsync_NewTool_CreatesAndPersistsTool()
    {
        var tenantId = Guid.NewGuid();

        var tool = await _registry.RegisterToolAsync(
            tenantId,
            "slack_announcement_broadcast",
            "Broadcasts product release notifications to customer Slack channels",
            "{\"type\":\"object\",\"properties\":{\"channel\":{\"type\":\"string\"},\"message\":{\"type\":\"string\"}}}",
            "https://hooks.slack.com/services/T00/B00/X00",
            "POST");

        tool.Should().NotBeNull();
        tool.TenantId.Should().Be(tenantId);
        tool.Name.Should().Be("slack_announcement_broadcast");
        tool.Description.Should().Be("Broadcasts product release notifications to customer Slack channels");
        tool.HttpMethod.Should().Be("POST");
        tool.IsActive.Should().BeTrue();

        var persisted = await _db.DynamicAgentTools.FirstOrDefaultAsync(t => t.Id == tool.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("slack_announcement_broadcast");
    }

    [Fact]
    public async Task RegisterToolAsync_ExistingTool_UpdatesExistingRecordWithoutDuplication()
    {
        var tenantId = Guid.NewGuid();

        var tool1 = await _registry.RegisterToolAsync(
            tenantId,
            "hubspot_contact_sync",
            "Syncs contacts v1",
            "{\"version\":1}",
            "https://api.hubspot.com/v1/sync",
            "POST");

        var tool2 = await _registry.RegisterToolAsync(
            tenantId,
            "hubspot_contact_sync",
            "Syncs contacts v2 updated",
            "{\"version\":2}",
            "https://api.hubspot.com/v2/sync",
            "PUT");

        tool2.Id.Should().Be(tool1.Id);
        tool2.Description.Should().Be("Syncs contacts v2 updated");
        tool2.ParametersSchemaJson.Should().Be("{\"version\":2}");
        tool2.TargetEndpointUrl.Should().Be("https://api.hubspot.com/v2/sync");
        tool2.HttpMethod.Should().Be("PUT");

        var count = await _db.DynamicAgentTools.CountAsync(t => t.TenantId == tenantId && t.Name == "hubspot_contact_sync");
        count.Should().Be(1);
    }

    [Fact]
    public async Task GetActiveToolsAsync_FiltersInactiveAndOrdersByName()
    {
        var tenantId = Guid.NewGuid();

        var toolZ = DynamicAgentTool.Create(tenantId, "zebra_report_exporter", "Desc Z", "{}", "https://example.com/z", "GET");
        toolZ.IsActive = true;

        var toolA = DynamicAgentTool.Create(tenantId, "alpha_lead_enricher", "Desc A", "{}", "https://example.com/a", "POST");
        toolA.IsActive = true;

        var toolInactive = DynamicAgentTool.Create(tenantId, "beta_archived_connector", "Desc B", "{}", "https://example.com/b", "POST");
        toolInactive.IsActive = false;

        _db.DynamicAgentTools.AddRange(toolZ, toolA, toolInactive);
        await _db.SaveChangesAsync();

        var activeTools = await _registry.GetActiveToolsAsync(tenantId);

        activeTools.Should().HaveCount(2);
        activeTools[0].Name.Should().Be("alpha_lead_enricher");
        activeTools[1].Name.Should().Be("zebra_report_exporter");
        activeTools.Should().NotContain(t => t.Name == "beta_archived_connector");
    }

    [Fact]
    public async Task ExecuteToolAsync_ToolNotRegisteredOrInactive_ReturnsFailure()
    {
        var tenantId = Guid.NewGuid();

        // 1. Tool does not exist
        var missingResult = await _registry.ExecuteToolAsync(tenantId, "non_existent_tool", "{}");
        missingResult.Should().NotBeNull();
        missingResult.Success.Should().BeFalse();
        missingResult.ToolName.Should().Be("non_existent_tool");
        missingResult.Error.Should().Contain("is not registered or active for tenant");

        // 2. Tool exists but is inactive
        var inactiveTool = DynamicAgentTool.Create(tenantId, "legacy_erp_sync", "Desc", "{}", "https://example.com/erp", "POST");
        inactiveTool.IsActive = false;
        _db.DynamicAgentTools.Add(inactiveTool);
        await _db.SaveChangesAsync();

        var inactiveResult = await _registry.ExecuteToolAsync(tenantId, "legacy_erp_sync", "{}");
        inactiveResult.Should().NotBeNull();
        inactiveResult.Success.Should().BeFalse();
        inactiveResult.Error.Should().Contain("is not registered or active for tenant");
    }

    [Fact]
    public async Task ExecuteToolAsync_ActiveTool_SimulatesSuccessfulExecution()
    {
        var tenantId = Guid.NewGuid();

        await _registry.RegisterToolAsync(
            tenantId,
            "stripe_charge_create",
            "Creates a charge",
            "{}",
            "https://api.stripe.com/v1/charges",
            "POST");

        var result = await _registry.ExecuteToolAsync(
            tenantId,
            "stripe_charge_create",
            "{\"amount\":5000,\"currency\":\"eur\"}");

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ToolName.Should().Be("stripe_charge_create");
        result.Error.Should().BeNull();
        result.OutputJson.Should().Contain("\"status\":\"success\"");
        result.OutputJson.Should().Contain("\"tool\":\"stripe_charge_create\"");
        result.OutputJson.Should().Contain("\"amount\":5000");
    }
}
