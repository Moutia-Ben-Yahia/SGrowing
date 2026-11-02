using FluentAssertions;
using SGrowing.Api.Modules.Automation.WorkflowTelemetry;
using Xunit;

namespace SGrowing.Tests.Automation;

public class WorkflowTelemetryTests
{
    private readonly WorkflowTelemetryEngine _engine = new();

    [Fact]
    public void ValidateDag_WithAcyclicGraph_PassesValidation()
    {
        var workflow = new WorkflowDefinition(
            WorkflowId: "wf_valid_01",
            Name: "Lead Nurture Flow",
            TriggerNodeId: "node_trigger",
            Nodes: new List<WorkflowNode>
            {
                new("node_trigger", "Form Submitted", WorkflowNodeType.Trigger),
                new("node_email_1", "Welcome Email", WorkflowNodeType.ActionEmail),
                new("node_delay", "Wait 2 Days", WorkflowNodeType.Delay, new() { { "DelayMinutes", "2880" } }),
                new("node_email_2", "Case Study Email", WorkflowNodeType.ActionEmail),
                new("node_end", "Done", WorkflowNodeType.Terminate)
            },
            Edges: new List<WorkflowEdge>
            {
                new("node_trigger", "node_email_1"),
                new("node_email_1", "node_delay"),
                new("node_delay", "node_email_2"),
                new("node_email_2", "node_end")
            }
        );

        var result = _engine.ValidateDag(workflow);

        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.HasCycles.Should().BeFalse();
        result.CyclePath.Should().BeEmpty();
        result.ValidationErrors.Should().BeEmpty();
        result.UnreachableNodeIds.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDag_WithCircularDependency_DetectsCycleAndReturnsError()
    {
        var workflow = new WorkflowDefinition(
            WorkflowId: "wf_cycle_02",
            Name: "Accidental Infinite Loop",
            TriggerNodeId: "n1",
            Nodes: new List<WorkflowNode>
            {
                new("n1", "Trigger", WorkflowNodeType.Trigger),
                new("n2", "Step A", WorkflowNodeType.ActionEmail),
                new("n3", "Step B", WorkflowNodeType.Delay),
                new("n4", "Step C", WorkflowNodeType.ActionWebhook)
            },
            Edges: new List<WorkflowEdge>
            {
                new("n1", "n2"),
                new("n2", "n3"),
                new("n3", "n4"),
                new("n4", "n2") // Cycle: n2 -> n3 -> n4 -> n2
            }
        );

        var result = _engine.ValidateDag(workflow);

        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.HasCycles.Should().BeTrue();
        result.CyclePath.Should().NotBeEmpty();
        result.ValidationErrors.Should().Contain(e => e.Contains("circular dependency"));
    }

    [Fact]
    public void SimulateWorkflow_WithBranchingCondition_FollowsCorrectTrueBranch()
    {
        var workflow = new WorkflowDefinition(
            WorkflowId: "wf_branch_03",
            Name: "Score-Based Router",
            TriggerNodeId: "start",
            Nodes: new List<WorkflowNode>
            {
                new("start", "Lead Created", WorkflowNodeType.Trigger),
                new("cond_score", "Score >= 80?", WorkflowNodeType.BranchCondition, new() { { "Field", "LeadScore" }, { "Threshold", "80" } }),
                new("vip_sales_alert", "Alert Account Exec", WorkflowNodeType.ActionWebhook, new() { { "Url", "https://crm.internal/alert-ae" } }),
                new("nurture_newsletter", "Add to Newsletter", WorkflowNodeType.ActionEmail, new() { { "TemplateId", "monthly_newsletter" } }),
                new("end_node", "Finish", WorkflowNodeType.Terminate)
            },
            Edges: new List<WorkflowEdge>
            {
                new("start", "cond_score"),
                new("cond_score", "vip_sales_alert", "true"),
                new("cond_score", "nurture_newsletter", "false"),
                new("vip_sales_alert", "end_node"),
                new("nurture_newsletter", "end_node")
            }
        );

        var highScorer = new SimulationContactContext(
            ContactId: "cont_vip_99",
            Email: "cto@megacorp.com",
            LeadScore: 92,
            Country: "US",
            HasPurchased: false,
            LifetimeValue: 0m
        );

        var request = new WorkflowSimulationRequest("tenant_auto_01", workflow, highScorer);
        var result = _engine.SimulateWorkflow(request);

        result.Should().NotBeNull();
        result.CompletedSuccessfully.Should().BeTrue();
        result.ActionsDispatched.Should().Contain(a => a.Contains("WebhookPost -> https://crm.internal/alert-ae"));
        result.ActionsDispatched.Should().NotContain(a => a.Contains("monthly_newsletter"));
        result.ExecutionPath.Should().Contain(s => s.NodeId == "vip_sales_alert");
    }

    [Fact]
    public void SimulateWorkflow_WithAiDecisionNode_RoutesToHighIntentPath()
    {
        var workflow = new WorkflowDefinition(
            WorkflowId: "wf_ai_decision_04",
            Name: "Autonomous Intent Allocator",
            TriggerNodeId: "node_trigger",
            Nodes: new List<WorkflowNode>
            {
                new("node_trigger", "E-commerce Visit", WorkflowNodeType.Trigger),
                new("node_ai", "Predict Buyer Intent", WorkflowNodeType.AiDecisionNode),
                new("node_fast_track", "Instant 20% Offer SMS", WorkflowNodeType.ActionSms, new() { { "Message", "Exclusive 20% offer" } }),
                new("node_standard", "Standard Drip Email", WorkflowNodeType.ActionEmail, new() { { "TemplateId", "standard_drip" } }),
                new("node_goal", "VIP Conversion Achieved", WorkflowNodeType.GoalAchieved)
            },
            Edges: new List<WorkflowEdge>
            {
                new("node_trigger", "node_ai"),
                new("node_ai", "node_fast_track", "high_intent"),
                new("node_ai", "node_standard", "nurture"),
                new("node_fast_track", "node_goal"),
                new("node_standard", "node_goal")
            }
        );

        var highIntentCustomer = new SimulationContactContext(
            ContactId: "cont_buyer_55",
            Email: "buyer@luxury.com",
            LeadScore: 85,
            Country: "UK",
            HasPurchased: true,
            LifetimeValue: 2500m
        );

        var request = new WorkflowSimulationRequest("tenant_auto_02", workflow, highIntentCustomer);
        var result = _engine.SimulateWorkflow(request);

        result.Should().NotBeNull();
        result.CompletedSuccessfully.Should().BeTrue();
        result.FinalStatus.Should().Be("GoalAchieved");
        result.ActionsDispatched.Should().Contain(a => a.Contains("SendSms -> cont_buyer_55: Exclusive 20% offer"));
        result.ActionsDispatched.Should().NotContain(a => a.Contains("standard_drip"));
    }

    [Fact]
    public void SimulateWorkflow_WithFilterBlocked_HaltsExecutionEarlyAndRecordsTelemetry()
    {
        var tenantId = "tenant_auto_03";
        var workflow = new WorkflowDefinition(
            WorkflowId: "wf_filtered_05",
            Name: "US Only Enterprise Campaign",
            TriggerNodeId: "t1",
            Nodes: new List<WorkflowNode>
            {
                new("t1", "Inbound Lead", WorkflowNodeType.Trigger),
                new("filter_us", "Require Country = US", WorkflowNodeType.Filter, new() { { "AllowedCountry", "US" } }),
                new("send_contract", "Dispatch Contract", WorkflowNodeType.ActionWebhook),
                new("end_node", "End", WorkflowNodeType.Terminate)
            },
            Edges: new List<WorkflowEdge>
            {
                new("t1", "filter_us"),
                new("filter_us", "send_contract"),
                new("send_contract", "end_node")
            }
        );

        var foreignLead = new SimulationContactContext(
            ContactId: "cont_eu_01",
            Email: "lead@berlin.de",
            LeadScore: 50,
            Country: "DE",
            HasPurchased: false,
            LifetimeValue: 0m
        );

        var request = new WorkflowSimulationRequest(tenantId, workflow, foreignLead);
        var result = _engine.SimulateWorkflow(request);

        result.CompletedSuccessfully.Should().BeTrue();
        result.FinalStatus.Should().Be("BlockedByFilter");
        result.ActionsDispatched.Should().BeEmpty();

        // Check telemetry record
        var telemetry = _engine.GetTelemetry(tenantId, result.ExecutionId);
        telemetry.Should().NotBeNull();
        telemetry!.Status.Should().Be("BlockedByFilter");
        telemetry.Steps.Should().HaveCount(2); // Trigger + Filter
    }
}
