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
}
