using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.Analytics;
using SGrowing.Api.Modules.Automation;
using SGrowing.Api.Modules.Tenancy;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.E2E;

public class MasterEnterpriseAnalyticsAndAITests
{
    private readonly SGrowingDbContext _db;
    private readonly PromptFirewallService _firewall;
    private readonly AiModelRouterService _router;
    private readonly DynamicAgentToolRegistry _tools;
    private readonly TenantUsageMeteringService _metering;
    private readonly MultiTouchAttributionService _attribution;
    private readonly QuietHoursGuardService _quietHours;
    private readonly AdvancedWorkflowSchedulerService _scheduler;

    public MasterEnterpriseAnalyticsAndAITests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase("MasterLeadershipDb_" + Guid.NewGuid())
            .Options;

        _db = new SGrowingDbContext(options);
        _firewall = new PromptFirewallService();
        _router = new AiModelRouterService();
        _tools = new DynamicAgentToolRegistry(_db);
        _metering = new TenantUsageMeteringService(_db);
        _attribution = new MultiTouchAttributionService(_db);
        _quietHours = new QuietHoursGuardService(_db);
        _scheduler = new AdvancedWorkflowSchedulerService(_db);
    }

    [Fact]
    public async Task MasterLeadershipE2E_AttributionSaaSMeteringAiGatewayAndDlq_RunsHarmoniously()
    {
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        // 1. AI Firewall Input Inspection
        var customerInput = "I am interested in SGrowing Enterprise for our 50-person sales team.";
        var firewallResult = _firewall.InspectAndSanitize(customerInput);
        Assert.True(firewallResult.IsSafe);

        // 2. AI Model Router Selects Model
        var routingDecision = _router.SelectModelForTask(AiTaskComplexity.DeepReasoning, "Enterprise Sales Consultation");
        Assert.Equal("claude-3-5-sonnet-20241022", routingDecision.SelectedModel);

        // 3. Dynamic Tool Invocation
        var tool = await _tools.RegisterToolAsync(
            tenantId, "crm_enrichment_lookup", "Enriches B2B domain", "{}", "https://api.internal/enrich", "POST");
        var toolExec = await _tools.ExecuteToolAsync(tenantId, "crm_enrichment_lookup", "{\"domain\":\"enterprise.com\"}");
        Assert.True(toolExec.Success);

        // 4. Usage Metering & Plan Entitlement
        await _metering.RecordUsageAsync(tenantId, UsageMetricType.AiTokens, 2500, "Enterprise Reasoning Turn");
        var entitlement = await _metering.CheckEntitlementAsync(tenantId, UsageMetricType.AiTokens, 1000);
        Assert.True(entitlement.Allowed);

        // 5. Multi-Touch Attribution Journey
        var deal = Deal.Create(tenantId, contactId, "Global SaaS Deal", 50000m, DealStage.Won);
        await _db.Deals.AddAsync(deal);

        await _attribution.RecordTouchpointAsync(tenantId, contactId, "GoogleAds", "Enterprise-Search", TouchpointStage.FirstDiscovery, deal.Id, cost: 800m);
        await _attribution.RecordTouchpointAsync(tenantId, contactId, "LinkedIn", "SDR-Inbound", TouchpointStage.LeadCreated, deal.Id, cost: 400m);
        await _attribution.RecordTouchpointAsync(tenantId, contactId, "WhatsApp", "AI-Closing-Turn", TouchpointStage.OpportunityCreated, deal.Id, cost: 100m);

        var attributionBreakdown = await _attribution.CalculateAttributionAsync(tenantId, deal.Id, AttributionModelType.WShaped);
        Assert.Equal(3, attributionBreakdown.Count);
        Assert.Equal(20000m, attributionBreakdown[0].AttributedRevenue); // 40% First Discovery
        Assert.Equal(20000m, attributionBreakdown[1].AttributedRevenue); // 40% Lead Created
        Assert.Equal(10000m, attributionBreakdown[2].AttributedRevenue); // 20% Opp Created

        var kpis = await _attribution.CalculateExecutiveKpisAsync(tenantId);
        Assert.Equal(50000m, kpis.TotalRevenue);
        Assert.Equal(1300m, kpis.TotalAdSpend);
        Assert.True(kpis.ReturnOnAdSpend > 30m); // > 38x ROAS

        // 6. Timezone Quiet Hours Dispatch Check
        var dispatchCheck = await _quietHours.EvaluateDispatchTimeAsync(tenantId, DateTime.UtcNow);
        Assert.True(dispatchCheck.CanSendNow);

        // 7. Dead-Letter Queue & Automated Replay
        var dlq = await _scheduler.EnqueueDeadLetterAsync(
            tenantId, Guid.NewGuid(), Guid.NewGuid(), "node_notify", "Action:Push", "{}", "Temporary network reset");
        var replay = await _scheduler.ReplayDeadLetterStepAsync(tenantId, dlq.Id);
        Assert.True(replay.Success);
    }
}
