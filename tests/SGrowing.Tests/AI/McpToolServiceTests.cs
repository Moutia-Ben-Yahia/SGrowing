using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using SGrowing.McpServer.Services;
using Xunit;

namespace SGrowing.Tests.AI;

public class McpToolServiceTests
{
    private static (ServiceProvider ServiceProvider, SGrowingDbContext Db, McpToolService ToolService) CreateContext()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();

        services.AddDbContext<SGrowingDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var tenant = new TenantContext();
        tenant.SetTenant(Guid.NewGuid());
        services.AddSingleton<ITenantContext>(tenant);

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<SGrowingDbContext>();
        var toolService = new McpToolService(sp);

        return (sp, db, toolService);
    }

    [Fact]
    public void GetTools_ReturnsAllRegisteredTools()
    {
        var (_, _, toolService) = CreateContext();
        var tools = toolService.GetTools();

        tools.Should().NotBeEmpty();
        tools.Should().Contain(t => t.Name == "crm_search_leads");
        tools.Should().Contain(t => t.Name == "courier_generate_waybill");
        tools.Should().Contain(t => t.Name == "callcenter_acquire_order_lock");
        tools.Should().Contain(t => t.Name == "cod_calculate_net_margin");
    }

    [Fact]
    public async Task ExecuteToolAsync_CrmSearchLeads_WhenDbHasContacts_ReturnsRealLeads()
    {
        var (_, db, toolService) = CreateContext();
        var tenantId = Guid.NewGuid();

        var contact = Contact.Create(
            tenantId,
            "Hassen",
            "Ben Ali",
            "hassen@carthage.tn",
            "+21620112233",
            LifecycleStage.Customer
        );
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var args = new JsonObject
        {
            ["query"] = "Hassen",
            ["limit"] = 5,
            ["status"] = "Qualified"
        };

        var (result, isError) = await toolService.ExecuteToolAsync("crm_search_leads", args);

        isError.Should().BeFalse();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteToolAsync_CallCenterAcquireOrderLock_PersistsLockInDatabase()
    {
        var (_, db, toolService) = CreateContext();
        var orderId = Guid.NewGuid();

        var args = new JsonObject
        {
            ["orderId"] = orderId.ToString(),
            ["agentName"] = "Operator Yasmine",
            ["ttlMinutes"] = 5
        };

        var (result, isError) = await toolService.ExecuteToolAsync("callcenter_acquire_order_lock", args);

        isError.Should().BeFalse();
        var locksInDb = await db.CallCenterOrderLocks.Where(l => l.OrderId == orderId).ToListAsync();
        locksInDb.Should().HaveCount(1);
        locksInDb[0].LockedByUserName.Should().Be("Operator Yasmine");
        locksInDb[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteToolAsync_CourierGenerateWaybill_CreatesOutboxEvent()
    {
        var (_, db, toolService) = CreateContext();

        var args = new JsonObject
        {
            ["orderId"] = "ORD-2026-7788",
            ["recipientName"] = "Mohamed Trabelsi",
            ["recipientPhone"] = "+21620112233",
            ["destinationWilaya"] = "Sousse",
            ["codAmount"] = 210.0
        };

        var (result, isError) = await toolService.ExecuteToolAsync("courier_generate_waybill", args);

        isError.Should().BeFalse();
        var outboxEvents = await db.OutboxMessages.Where(m => m.EventType == "CourierWaybillGenerated").ToListAsync();
        outboxEvents.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteToolAsync_CodCalculateNetMargin_ComputesAccurateMargin()
    {
        var (_, _, toolService) = CreateContext();

        var args = new JsonObject
        {
            ["orderTotal"] = 200.0,
            ["productCost"] = 60.0,
            ["shippingFee"] = 10.0,
            ["callCenterFee"] = 5.0,
            ["returnRatePercent"] = 10.0,
            ["returnPenaltyFee"] = 20.0
        };

        var (result, isError) = await toolService.ExecuteToolAsync("cod_calculate_net_margin", args);

        isError.Should().BeFalse();
        result.ToString().Should().NotBeNull();
    }

    [Fact]
    public async Task HandleJsonRpcAsync_InitializeAndToolsList_ReturnsValidJsonRpc2()
    {
        var (_, _, toolService) = CreateContext();

        var initPayload = """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""";
        var initResponse = await toolService.HandleJsonRpcAsync(initPayload);
        initResponse.Should().Contain("SGrowing.McpServer");

        var listPayload = """{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""";
        var listResponse = await toolService.HandleJsonRpcAsync(listPayload);
        listResponse.Should().Contain("crm_search_leads");
    }
}
