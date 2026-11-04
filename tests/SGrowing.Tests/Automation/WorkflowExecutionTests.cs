using FluentAssertions;
using SGrowing.Api.Modules.Automation.ExecutionEngine;
using Xunit;

namespace SGrowing.Tests.Automation;

public class WorkflowExecutionTests
{
    private readonly WorkflowExecutionEngine _engine = new();

    [Fact]
    public void TriggerWorkflow_ExecutesStepsSequentiallyAndRecordsTrace()
    {
        // Arrange
        var wf = new WorkflowDefinition(
            WorkflowId: "wf_lead_nurture",
            TenantId: "tenant_wf_01",
            Name: "Lead Qualification & Alert",
            Description: "Qualifies incoming webhooks and updates CRM",
            IsActive: true,
            Steps: new List<WorkflowStepDefinition>
            {
                new("s1", "Webhook Ingest", WorkflowNodeKind.WebhookTrigger, new(), new() { "s2" }),
                new("s2", "Filter Lead Score", WorkflowNodeKind.FilterCondition, new() { ["minScore"] = "50" }, new() { "s3" }),
                new("s3", "Transform Payload", WorkflowNodeKind.TransformPayload, new(), new() { "s4" }),
                new("s4", "Update CRM", WorkflowNodeKind.CrmAction, new(), new())
            },
            CreatedAtUtc: DateTime.UtcNow
        );
        _engine.RegisterWorkflow(wf);

        var payload = new Dictionary<string, object>
        {
            ["leadEmail"] = "prospect@scale.com",
            ["leadScore"] = 75
        };

        // Act
        var execution = _engine.TriggerWorkflow(new TriggerWorkflowRequest("tenant_wf_01", "wf_lead_nurture", payload));

        // Assert
        execution.Should().NotBeNull();
        execution.Status.Should().Be(NodeExecutionStatus.Completed);
        execution.Steps.Should().HaveCount(4);
        execution.Steps.All(s => s.Status == NodeExecutionStatus.Completed).Should().BeTrue();
        execution.Steps.Last().OutputJson.Should().Contain("\"crm_updated\":true");
    }

    [Fact]
    public void TriggerWorkflow_HaltsOnFilterFailure_AndAllowsStepReplay()
    {
        // Arrange
        var wf = new WorkflowDefinition(
            WorkflowId: "wf_strict_filter",
            TenantId: "tenant_wf_02",
            Name: "VIP Route",
            Description: "Requires 90+ score",
            IsActive: true,
            Steps: new List<WorkflowStepDefinition>
            {
                new("step_trigger", "Trigger", WorkflowNodeKind.WebhookTrigger, new(), new() { "step_filter" }),
                new("step_filter", "Check VIP", WorkflowNodeKind.FilterCondition, new() { ["minScore"] = "90" }, new() { "step_action" }),
                new("step_action", "Dispatch", WorkflowNodeKind.HttpDispatch, new(), new())
            },
            CreatedAtUtc: DateTime.UtcNow
        );
        _engine.RegisterWorkflow(wf);

        var lowScorePayload = new Dictionary<string, object>
        {
            ["leadScore"] = 40
        };

        // Act 1: Trigger with low score
        var execution = _engine.TriggerWorkflow(new TriggerWorkflowRequest("tenant_wf_02", "wf_strict_filter", lowScorePayload));

        // Assert 1: Step 2 failed
        execution.Status.Should().Be(NodeExecutionStatus.Failed);
        execution.Steps.Should().HaveCount(2);
        execution.Steps[1].Status.Should().Be(NodeExecutionStatus.Failed);
        execution.Steps[1].ErrorMessage.Should().Contain("below threshold 90");

        // Act 2: Replay failed step
        var replayed = _engine.ReplayStep(new ReplayStepRequest("tenant_wf_02", execution.ExecutionId, "step_filter"));

        // Assert 2: Step replayed and marked completed with incremented attempt
        replayed.Steps[1].Status.Should().Be(NodeExecutionStatus.Completed);
        replayed.Steps[1].AttemptCount.Should().Be(2);
    }

    [Fact]
    public void TriggerWorkflow_WhenWorkflowNotFound_ThrowsKeyNotFoundException()
    {
        var act = () => _engine.TriggerWorkflow(new TriggerWorkflowRequest("tenant_missing", "wf_non_existent", new()));

        act.Should().Throw<KeyNotFoundException>()
            .WithMessage("*not found for tenant*");
    }

    [Fact]
    public void TriggerWorkflow_WithDelayTimerHttpDispatchAndSendEmail_ShouldExecuteAllNodes()
    {
        var wf = new WorkflowDefinition(
            WorkflowId: "wf_omnichannel_dispatch",
            TenantId: "tenant_dispatch_01",
            Name: "Omnichannel Alert",
            Description: "Sends webhook and email after delay",
            IsActive: true,
            Steps: new List<WorkflowStepDefinition>
            {
                new("s_delay", "Delay 5 min", WorkflowNodeKind.DelayTimer, new(), new() { "s_http" }),
                new("s_http", "Partner Webhook", WorkflowNodeKind.HttpDispatch, new() { ["url"] = "https://custom.webhook.site" }, new() { "s_email" }),
                new("s_email", "Email Confirmation", WorkflowNodeKind.SendEmail, new() { ["template"] = "vip_notification" }, new())
            },
            CreatedAtUtc: DateTime.UtcNow
        );
        _engine.RegisterWorkflow(wf);

        var execution = _engine.TriggerWorkflow(new TriggerWorkflowRequest("tenant_dispatch_01", "wf_omnichannel_dispatch", new()));

        execution.Status.Should().Be(NodeExecutionStatus.Completed);
        execution.Steps.Should().HaveCount(3);
        execution.Steps[0].OutputJson.Should().Contain("\"delay_completed\":true");
        execution.Steps[1].OutputJson.Should().Contain("\"https://custom.webhook.site\"");
        execution.Steps[2].OutputJson.Should().Contain("\"vip_notification\"");
    }

    [Fact]
    public void GetWorkflow_WhenNotRegistered_ReturnsNull()
    {
        var wf = _engine.GetWorkflow("tenant_x", "wf_unknown");
        wf.Should().BeNull();
    }

    [Fact]
    public void ListExecutions_ShouldFilterByTenantAndWorkflowId()
    {
        var tenantA = "tenant_list_a";
        var tenantB = "tenant_list_b";

        var wfA1 = new WorkflowDefinition("wf_a1", tenantA, "Name A1", "Desc", true, new(), DateTime.UtcNow);
        var wfA2 = new WorkflowDefinition("wf_a2", tenantA, "Name A2", "Desc", true, new(), DateTime.UtcNow);
        var wfB1 = new WorkflowDefinition("wf_b1", tenantB, "Name B1", "Desc", true, new(), DateTime.UtcNow);

        _engine.RegisterWorkflow(wfA1);
        _engine.RegisterWorkflow(wfA2);
        _engine.RegisterWorkflow(wfB1);

        _engine.TriggerWorkflow(new TriggerWorkflowRequest(tenantA, "wf_a1", new()));
        _engine.TriggerWorkflow(new TriggerWorkflowRequest(tenantA, "wf_a2", new()));
        _engine.TriggerWorkflow(new TriggerWorkflowRequest(tenantB, "wf_b1", new()));

        var executionsA = _engine.ListExecutions(tenantA);
        var executionsA1Only = _engine.ListExecutions(tenantA, "wf_a1");
        var executionsB = _engine.ListExecutions(tenantB);

        executionsA.Should().HaveCount(2);
        executionsA1Only.Should().ContainSingle();
        executionsA1Only[0].WorkflowId.Should().Be("wf_a1");
        executionsB.Should().ContainSingle();
    }

    [Fact]
    public void ReplayStep_WhenExecutionOrStepNotFound_ThrowsKeyNotFoundException()
    {
        // Non-existent execution
        var act1 = () => _engine.ReplayStep(new ReplayStepRequest("tenant_test", "exec_missing", "step_01"));
        act1.Should().Throw<KeyNotFoundException>().WithMessage("*Execution 'exec_missing' not found*");

        // Non-existent step in valid execution
        var wf = new WorkflowDefinition("wf_replay_err", "tenant_err", "Error Wf", "Desc", true, new(), DateTime.UtcNow);
        _engine.RegisterWorkflow(wf);
        var exec = _engine.TriggerWorkflow(new TriggerWorkflowRequest("tenant_err", "wf_replay_err", new()));

        var act2 = () => _engine.ReplayStep(new ReplayStepRequest("tenant_err", exec.ExecutionId, "step_ghost"));
        act2.Should().Throw<KeyNotFoundException>().WithMessage("*Step 'step_ghost' not found*");
    }

    [Fact]
    public void GetExecution_WhenExistsAndNonExistent_ReturnsExpected()
    {
        var tenant = "tenant_exec_get";
        var wf = new WorkflowDefinition("wf_get_test", tenant, "Get Test", "Desc", true, new(), DateTime.UtcNow);
        _engine.RegisterWorkflow(wf);

        var triggered = _engine.TriggerWorkflow(new TriggerWorkflowRequest(tenant, "wf_get_test", new()));

        var found = _engine.GetExecution(tenant, triggered.ExecutionId);
        found.Should().NotBeNull();
        found!.ExecutionId.Should().Be(triggered.ExecutionId);

        var missing = _engine.GetExecution(tenant, "exec_not_found");
        missing.Should().BeNull();
    }
}
