using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Journeys;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Journeys;

public class StatefulJourneyEngineTests
{
    private static SGrowingDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    // =========================================================================
    // 1. DAG Validation & Cycle Rejection (Kahn's Algorithm + DFS)
    // =========================================================================

    [Fact]
    public void DagValidation_AcyclicGraph_PassesValidation()
    {
        using var db = CreateInMemoryDb();
        var service = new StatefulJourneyEngineService(db);

        var nodes = new List<JourneyNode>
        {
            new TriggerNode("node-trigger", "Order Abandoned Trigger", "order_abandoned", nextNodeId: "node-delay"),
            new DelayNode("node-delay", "Wait 15m", delaySeconds: 900, nextNodeId: "node-wait"),
            new WaitUntilEventNode("node-wait", "Wait for Checkout", eventName: "checkout_completed", ttlSeconds: 86400,
                successNextNodeId: "node-success-email", timeoutNextNodeId: "node-timeout-whatsapp"),
            new ChannelActionNode("node-success-email", "Thank You Email", "Email", "Thank you for completing checkout!", nextNodeId: "node-exit"),
            new ChannelActionNode("node-timeout-whatsapp", "Abandoned Cart WhatsApp", "WhatsApp", "Complete your order with 10% off!", nextNodeId: "node-exit"),
            new ExitNode("node-exit", "Journey Exit")
        };

        var result = service.ValidateDag(nodes);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void DagValidation_DetectsCycles_AndRejectsGraph()
    {
        using var db = CreateInMemoryDb();
        var service = new StatefulJourneyEngineService(db);

        // A -> B -> C -> A cycle
        var nodes = new List<JourneyNode>
        {
            new TriggerNode("node-trigger", "Trigger", "lead_signup", nextNodeId: "node-a"),
            new ChannelActionNode("node-a", "Action A", "Email", "Step A", nextNodeId: "node-b"),
            new DelayNode("node-b", "Delay B", delaySeconds: 60, nextNodeId: "node-c"),
            new ChannelActionNode("node-c", "Action C", "SMS", "Step C", nextNodeId: "node-a"), // Cycle back to A!
            new ExitNode("node-exit", "Exit")
        };

        var result = service.ValidateDag(nodes);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Cycle detected", result.ErrorMessage);
        Assert.NotNull(result.CyclePath);
        Assert.True(result.CyclePath.Count > 0);
    }

    [Fact]
    public void DagValidation_MissingTargetNode_FailsValidation()
    {
        using var db = CreateInMemoryDb();
        var service = new StatefulJourneyEngineService(db);

        var nodes = new List<JourneyNode>
        {
            new TriggerNode("node-trigger", "Trigger", "lead_signup", nextNodeId: "nonexistent-node-id"),
            new ExitNode("node-exit", "Exit")
        };

        var result = service.ValidateDag(nodes);

        Assert.False(result.IsValid);
        Assert.Contains("nonexistent target node", result.ErrorMessage);
    }

    [Fact]
    public void DagValidation_MissingTriggerNode_FailsValidation()
    {
        using var db = CreateInMemoryDb();
        var service = new StatefulJourneyEngineService(db);

        var nodes = new List<JourneyNode>
        {
            new DelayNode("node-delay", "Delay", delaySeconds: 60, nextNodeId: "node-exit"),
            new ExitNode("node-exit", "Exit")
        };

        var result = service.ValidateDag(nodes);

        Assert.False(result.IsValid);
        Assert.Contains("Trigger node", result.ErrorMessage);
    }

    // =========================================================================
    // 2. Linear Progression through Delay and Channel Nodes
    // =========================================================================

    [Fact]
    public async Task LinearProgression_ExecutesDelayAndChannelNodes_ToCompletion()
    {
        using var db = CreateInMemoryDb();
        var service = new StatefulJourneyEngineService(db);
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var nodes = new List<JourneyNode>
        {
            new TriggerNode("t1", "Lead Onboarding", "lead_signup", nextNodeId: "d1"),
            new DelayNode("d1", "Immediate Delay", delaySeconds: 0, nextNodeId: "c1"),
            new ChannelActionNode("c1", "Welcome Email", "Email", "Hello {{first_name}}, welcome to {{company}}!", nextNodeId: "e1"),
            new ExitNode("e1", "Exit")
        };

        var defReq = new SaveJourneyDefinitionRequest(
            Id: null,
            Name: "Welcome Series",
            Description: "Immediate welcome series",
            TriggerEvent: "lead_signup",
            ConversionEvents: null,
            Nodes: nodes
        );

        var def = await service.SaveJourneyDefinitionAsync(tenantId, defReq);

        var initialAttributes = new Dictionary<string, object>
        {
            { "first_name", "Yassine" },
            { "company", "Atlas Logistics" }
        };

        var instance = await service.StartJourneyAsync(tenantId, def.Id, contactId, initialAttributes);

        Assert.Equal(JourneyInstanceStatus.Completed, instance.Status);
        Assert.NotNull(instance.CompletedAtUtc);

        // Check history execution log
        var history = instance.ExecutionHistory;
        Assert.Contains(history, h => h.NodeId == "t1" && h.Status == "Executed");
        Assert.Contains(history, h => h.NodeId == "d1" && h.Status == "Executed");
        Assert.Contains(history, h => h.NodeId == "c1" && h.Status == "Executed" && h.Details.Contains("Hello Yassine, welcome to Atlas Logistics!"));
        Assert.Contains(history, h => h.NodeId == "e1" && h.Status == "Completed");
    }

    // =========================================================================
    // 3. Wait-Until Event Matching (Success Branch)
    // =========================================================================

    [Fact]
    public async Task WaitUntilEvent_MatchesEventBeforeTtl_ExecutesSuccessBranch()
    {
        using var db = CreateInMemoryDb();
        var service = new StatefulJourneyEngineService(db);
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var nodes = new List<JourneyNode>
        {
            new TriggerNode("t1", "Cart Abandoned", "order_abandoned", nextNodeId: "w1"),
            new WaitUntilEventNode("w1", "Wait for Checkout", eventName: "checkout_completed", ttlSeconds: 7200,
                successNextNodeId: "success-channel", timeoutNextNodeId: "timeout-channel"),
            new ChannelActionNode("success-channel", "Order Confirmation Email", "Email", "Thank you for your purchase!", nextNodeId: "e1"),
            new ChannelActionNode("timeout-channel", "Recovery WhatsApp", "WhatsApp", "Forgot something?", nextNodeId: "e1"),
            new ExitNode("e1", "Exit")
        };

        var def = await service.SaveJourneyDefinitionAsync(tenantId, new SaveJourneyDefinitionRequest(
            Id: null,
            Name: "Cart Abandonment Flow",
            Description: "Recovers abandoned carts",
            TriggerEvent: "order_abandoned",
            ConversionEvents: new List<string> { "OrderPlaced" },
            Nodes: nodes
        ));

        // Start Journey: contact enters wait-until node
        var instance = await service.StartJourneyAsync(tenantId, def.Id, contactId);

        Assert.Equal(JourneyInstanceStatus.WaitingForEvent, instance.Status);
        Assert.Equal("checkout_completed", instance.WaitUntilEventName);
        Assert.NotNull(instance.WaitUntilExpiresAtUtc);
        Assert.True(instance.WaitUntilExpiresAtUtc > DateTime.UtcNow);

        // Ingest the matching event before TTL expiration
        var ingestResult = await service.IngestEventAsync(tenantId, contactId, "checkout_completed", new Dictionary<string, object>
        {
            { "order_id", "ORD-991" }
        });

        Assert.Equal(1, ingestResult.AdvancedCount);

        // Reload instance from db
        var updatedInstance = await db.JourneyInstances.FirstAsync(i => i.Id == instance.Id);
        Assert.Equal(JourneyInstanceStatus.Completed, updatedInstance.Status);

        var history = updatedInstance.ExecutionHistory;
        Assert.Contains(history, h => h.NodeId == "w1" && h.Status == "MatchedEvent");
        Assert.Contains(history, h => h.NodeId == "success-channel" && h.Status == "Executed");
        Assert.DoesNotContain(history, h => h.NodeId == "timeout-channel");
    }

    // =========================================================================
    // 4. Wait-Until Timeout Sweep (Timeout Branch)
    // =========================================================================

    [Fact]
    public async Task WaitUntilEvent_SweepsExpiredTtl_ExecutesTimeoutBranch()
    {
        using var db = CreateInMemoryDb();
        var service = new StatefulJourneyEngineService(db);
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var nodes = new List<JourneyNode>
        {
            new TriggerNode("t1", "Cart Abandoned", "order_abandoned", nextNodeId: "w1"),
            new WaitUntilEventNode("w1", "Wait for Checkout", eventName: "checkout_completed", ttlSeconds: 3600,
                successNextNodeId: "success-channel", timeoutNextNodeId: "timeout-channel"),
            new ChannelActionNode("success-channel", "Order Confirmation Email", "Email", "Thank you!", nextNodeId: "e1"),
            new ChannelActionNode("timeout-channel", "Abandoned Cart 10% Discount", "WhatsApp", "Here is a 10% discount to finish checkout!", nextNodeId: "e1"),
            new ExitNode("e1", "Exit")
        };

        var def = await service.SaveJourneyDefinitionAsync(tenantId, new SaveJourneyDefinitionRequest(
            Id: null,
            Name: "Cart Abandonment Flow",
            Description: "Recovers abandoned carts",
            TriggerEvent: "order_abandoned",
            ConversionEvents: null,
            Nodes: nodes
        ));

        var instance = await service.StartJourneyAsync(tenantId, def.Id, contactId);
        Assert.Equal(JourneyInstanceStatus.WaitingForEvent, instance.Status);

        // Simulate TTL expiration in the past
        instance.WaitUntilExpiresAtUtc = DateTime.UtcNow.AddHours(-2);
        await db.SaveChangesAsync();

        // Run timeout sweeper
        var sweptCount = await service.SweepExpiredTimeoutsAsync(tenantId);
        Assert.Equal(1, sweptCount);

        var updatedInstance = await db.JourneyInstances.FirstAsync(i => i.Id == instance.Id);
        Assert.Equal(JourneyInstanceStatus.Completed, updatedInstance.Status);

        var history = updatedInstance.ExecutionHistory;
        Assert.Contains(history, h => h.NodeId == "w1" && h.Status == "TimedOut");
        Assert.Contains(history, h => h.NodeId == "timeout-channel" && h.Status == "Executed" && h.Details.Contains("10% discount"));
        Assert.DoesNotContain(history, h => h.NodeId == "success-channel");
    }

    // =========================================================================
    // 5. Dynamic Unenrollment on Conversion
    // =========================================================================

    [Fact]
    public async Task DynamicUnenrollment_PurgesContactImmediately_OnConversionEvent()
    {
        using var db = CreateInMemoryDb();
        var service = new StatefulJourneyEngineService(db);
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var nodes = new List<JourneyNode>
        {
            new TriggerNode("t1", "Pricing Page Visit", "page_view_pricing", nextNodeId: "w1"),
            new WaitUntilEventNode("w1", "Wait for Contract", eventName: "contract_viewed", ttlSeconds: 86400,
                successNextNodeId: "c1", timeoutNextNodeId: "c2"),
            new ChannelActionNode("c1", "Sales Followup", "Email", "Discussing contract terms", nextNodeId: "e1"),
            new ChannelActionNode("c2", "Nudge", "SMS", "Need help with pricing?", nextNodeId: "e1"),
            new ExitNode("e1", "Exit")
        };

        // Notice: Journey defines "ContractSigned" and "OrderPlaced" as conversion unenrollment events
        var def = await service.SaveJourneyDefinitionAsync(tenantId, new SaveJourneyDefinitionRequest(
            Id: null,
            Name: "High Intent Nurture",
            Description: "Sales nurture flow",
            TriggerEvent: "page_view_pricing",
            ConversionEvents: new List<string> { "ContractSigned", "OrderPlaced" },
            Nodes: nodes
        ));

        // Contact enrolls and pauses in wait node
        var instance = await service.StartJourneyAsync(tenantId, def.Id, contactId);
        Assert.Equal(JourneyInstanceStatus.WaitingForEvent, instance.Status);

        // Customer signs contract independently -> conversion event occurs!
        var ingestResult = await service.IngestEventAsync(tenantId, contactId, "ContractSigned");

        Assert.Equal(1, ingestResult.UnenrolledCount);

        var updatedInstance = await db.JourneyInstances.FirstAsync(i => i.Id == instance.Id);
        Assert.Equal(JourneyInstanceStatus.Unenrolled, updatedInstance.Status);
        Assert.NotNull(updatedInstance.CompletedAtUtc);

        var history = updatedInstance.ExecutionHistory;
        Assert.Contains(history, h => h.Status == "Unenrolled" && h.Details.Contains("ContractSigned"));
    }

    // =========================================================================
    // 6. Condition Branching Evaluation (Ltv, Country, TagContains)
    // =========================================================================

    [Theory]
    [InlineData("Ltv > 500", 750, true)]
    [InlineData("Ltv > 500", 300, false)]
    [InlineData("Ltv >= 1000", 1000, true)]
    [InlineData("Ltv < 200", 150, true)]
    public void ConditionEvaluation_NumericLtv_EvaluatesCorrectly(string expression, decimal ltv, bool expected)
    {
        var attrs = new Dictionary<string, object>
        {
            { "Ltv", ltv }
        };

        var result = StatefulJourneyEngineService.EvaluateCondition(expression, attrs);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConditionEvaluation_CountryString_EvaluatesCorrectly()
    {
        var attrsMa = new Dictionary<string, object> { { "Country", "MA" } };
        var attrsTn = new Dictionary<string, object> { { "Country", "TN" } };

        Assert.True(StatefulJourneyEngineService.EvaluateCondition("Country == 'MA'", attrsMa));
        Assert.False(StatefulJourneyEngineService.EvaluateCondition("Country == 'MA'", attrsTn));
        Assert.True(StatefulJourneyEngineService.EvaluateCondition("Country != 'MA'", attrsTn));
    }

    [Fact]
    public void ConditionEvaluation_TagContains_EvaluatesCorrectly()
    {
        var attrsList = new Dictionary<string, object>
        {
            { "Tags", new List<string> { "Prospect", "VIP", "Enterprise" } }
        };
        var attrsStr = new Dictionary<string, object>
        {
            { "Tags", "VIP, Retail" }
        };
        var attrsNoVip = new Dictionary<string, object>
        {
            { "Tags", new List<string> { "Standard" } }
        };

        Assert.True(StatefulJourneyEngineService.EvaluateCondition("TagContains(\"VIP\")", attrsList));
        Assert.True(StatefulJourneyEngineService.EvaluateCondition("TagContains('VIP')", attrsStr));
        Assert.False(StatefulJourneyEngineService.EvaluateCondition("TagContains(\"VIP\")", attrsNoVip));
    }

    [Fact]
    public async Task ConditionBranching_RoutesVipAndStandardContacts_ToDifferentBranches()
    {
        using var db = CreateInMemoryDb();
        var service = new StatefulJourneyEngineService(db);
        var tenantId = Guid.NewGuid();

        var nodes = new List<JourneyNode>
        {
            new TriggerNode("t1", "Trigger", "signup", nextNodeId: "cond1"),
            new ConditionBranchNode("cond1", "LTV Check", "Ltv > 500", trueNextNodeId: "vip-action", falseNextNodeId: "standard-action"),
            new ChannelActionNode("vip-action", "VIP WhatsApp Concierge", "WhatsApp", "Welcome VIP!", nextNodeId: "e1"),
            new ChannelActionNode("standard-action", "Standard Email Welcome", "Email", "Welcome standard user!", nextNodeId: "e1"),
            new ExitNode("e1", "Exit")
        };

        var def = await service.SaveJourneyDefinitionAsync(tenantId, new SaveJourneyDefinitionRequest(
            Id: null,
            Name: "Segmented Welcome",
            Description: "Routes based on LTV",
            TriggerEvent: "signup",
            ConversionEvents: null,
            Nodes: nodes
        ));

        // Contact 1: VIP
        var vipContactId = Guid.NewGuid();
        var vipInst = await service.StartJourneyAsync(tenantId, def.Id, vipContactId, new Dictionary<string, object> { { "Ltv", 1200 } });
        Assert.Equal(JourneyInstanceStatus.Completed, vipInst.Status);
        Assert.Contains(vipInst.ExecutionHistory, h => h.NodeId == "vip-action" && h.Status == "Executed");
        Assert.DoesNotContain(vipInst.ExecutionHistory, h => h.NodeId == "standard-action");

        // Contact 2: Standard
        var standardContactId = Guid.NewGuid();
        var stdInst = await service.StartJourneyAsync(tenantId, def.Id, standardContactId, new Dictionary<string, object> { { "Ltv", 250 } });
        Assert.Equal(JourneyInstanceStatus.Completed, stdInst.Status);
        Assert.Contains(stdInst.ExecutionHistory, h => h.NodeId == "standard-action" && h.Status == "Executed");
        Assert.DoesNotContain(stdInst.ExecutionHistory, h => h.NodeId == "vip-action");
    }

    // =========================================================================
    // 7. Node Metrics & Drop-Off Funnel Computation
    // =========================================================================

    [Fact]
    public async Task JourneyMetrics_ComputesAccurateCountsAndDropOffRates()
    {
        using var db = CreateInMemoryDb();
        var service = new StatefulJourneyEngineService(db);
        var tenantId = Guid.NewGuid();

        var nodes = new List<JourneyNode>
        {
            new TriggerNode("t1", "Trigger", "order_abandoned", nextNodeId: "w1"),
            new WaitUntilEventNode("w1", "Wait for Checkout", "checkout_completed", ttlSeconds: 3600,
                successNextNodeId: "c1", timeoutNextNodeId: "c2"),
            new ChannelActionNode("c1", "Success Action", "Email", "Done", nextNodeId: "e1"),
            new ChannelActionNode("c2", "Timeout Action", "SMS", "Nudge", nextNodeId: "e1"),
            new ExitNode("e1", "Exit")
        };

        var def = await service.SaveJourneyDefinitionAsync(tenantId, new SaveJourneyDefinitionRequest(
            Id: null,
            Name: "Metrics Test Flow",
            Description: "Funnel metrics check",
            TriggerEvent: "order_abandoned",
            ConversionEvents: new List<string> { "OrderPlaced" },
            Nodes: nodes
        ));

        // Contact 1: Enrolls and completes via success branch
        var c1 = Guid.NewGuid();
        await service.StartJourneyAsync(tenantId, def.Id, c1);
        await service.IngestEventAsync(tenantId, c1, "checkout_completed");

        // Contact 2: Enrolls and unenrolled dynamically via conversion event
        var c2 = Guid.NewGuid();
        await service.StartJourneyAsync(tenantId, def.Id, c2);
        await service.IngestEventAsync(tenantId, c2, "OrderPlaced");

        // Contact 3: Enrolls and stays waiting
        var c3 = Guid.NewGuid();
        await service.StartJourneyAsync(tenantId, def.Id, c3);

        // Fetch Metrics
        var metrics = await service.GetJourneyMetricsAsync(tenantId, def.Id);

        Assert.Equal(3, metrics.TotalEnrolled);
        Assert.Equal(1, metrics.TotalCompleted);
        Assert.Equal(1, metrics.TotalUnenrolled);
        Assert.Equal(1, metrics.CurrentlyWaiting);
        Assert.Equal(0, metrics.CurrentlyActive);
        Assert.Equal(33.3, metrics.CompletionRate);
        Assert.Equal(33.3, metrics.DropOffRate);

        // Trigger Node should have 3 executions
        var triggerMetric = metrics.NodeMetrics.First(m => m.NodeId == "t1");
        Assert.Equal(3, triggerMetric.ExecutionsCount);

        // Wait Node should have 3 executions
        var waitMetric = metrics.NodeMetrics.First(m => m.NodeId == "w1");
        Assert.Equal(3, waitMetric.ExecutionsCount);
        Assert.Equal(1, waitMetric.CurrentContactsCount); // Contact 3 is waiting here
    }
}
