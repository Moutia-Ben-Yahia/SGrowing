using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.Automation;
using Xunit;

namespace SGrowing.Tests.Automation;

public class DurableWorkflowDagAndHitlTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly InMemoryWorkflowEventStore _eventStore = new();
    private readonly InMemoryWorkflowExecutionStore _executionStore = new();
    private readonly InMemoryWorkflowApprovalTaskStore _taskStore = new();
    private readonly DefaultWorkflowActionExecutor _actionExecutor = new();
    private readonly DefaultAiPromptTransformer _aiTransformer = new();
    private readonly LocalTimezonePacingService _pacingService = new();

    private readonly Mock<IHubContext<OmniHub, IOmniClient>> _mockHubContext;
    private readonly Mock<IHubClients<IOmniClient>> _mockClients;
    private readonly Mock<IOmniClient> _mockClientProxy;

    public DurableWorkflowDagAndHitlTests()
    {
        _mockHubContext = new Mock<IHubContext<OmniHub, IOmniClient>>();
        _mockClients = new Mock<IHubClients<IOmniClient>>();
        _mockClientProxy = new Mock<IOmniClient>();

        _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
        _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);
    }

    private (DurableWorkflowExecutionEngine Engine, HumanInTheLoopApprovalService HitlService, ServiceProvider Provider) CreateEngineAndHitl(
        IWorkflowActionExecutor? customActionExecutor = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowEventStore>(_eventStore);
        services.AddSingleton<IWorkflowExecutionStore>(_executionStore);
        services.AddSingleton<IWorkflowApprovalTaskStore>(_taskStore);
        services.AddSingleton<IWorkflowActionExecutor>(customActionExecutor ?? _actionExecutor);
        services.AddSingleton<IAiPromptTransformer>(_aiTransformer);
        services.AddSingleton<ILocalTimezonePacingService>(_pacingService);
        services.AddSingleton(_mockHubContext.Object);

        services.AddScoped<IDurableWorkflowExecutionEngine, DurableWorkflowExecutionEngine>();
        services.AddScoped<IHumanInTheLoopApprovalService, HumanInTheLoopApprovalService>();

        var provider = services.BuildServiceProvider();
        var engine = (DurableWorkflowExecutionEngine)provider.GetRequiredService<IDurableWorkflowExecutionEngine>();
        var hitl = (HumanInTheLoopApprovalService)provider.GetRequiredService<IHumanInTheLoopApprovalService>();

        return (engine, hitl, provider);
    }

    #region 1. Durable Workflow Execution Engine & Node Types

    [Fact]
    public async Task DurableWorkflow_LinearFlow_ExecutesAllNodesThroughCompletion()
    {
        // Arrange: Trigger -> AiPrompt -> Action
        var (engine, _, _) = CreateEngineAndHitl();

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Linear Inbound Pipeline",
            Nodes:
            [
                new("t1", "Inbound WhatsApp Ingestion", DurableWorkflowNodeType.TriggerNode, Config: new() { ["leadName"] = "Atlas Logistics" }),
                new("ai1", "AI Intent Classification", DurableWorkflowNodeType.AiPromptNode, Config: new() { ["promptTemplate"] = "Classify: {leadName}", ["taskType"] = "classify_intent", ["outputVariable"] = "intent" }),
                new("a1", "CRM Deal Provisioning", DurableWorkflowNodeType.ActionNode, Subtype: "create_deal")
            ],
            Edges:
            [
                new("t1", "ai1"),
                new("ai1", "a1")
            ]
        );

        // Act
        var result = await engine.StartExecutionAsync(_tenantId, dag, new() { ["leadName"] = "Atlas Logistics" });

        // Assert
        result.Status.Should().Be(WorkflowExecutionState.Completed);
        result.ExecutedStepsCount.Should().Be(3);
        result.ReplayedStepsCount.Should().Be(0);
        result.Context.Should().ContainKey("leadName").WhoseValue.Should().Be("Atlas Logistics");
        result.Context.Should().ContainKey("intent");
        result.Context.Should().ContainKey("crmRecordId");
        result.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task DurableWorkflow_TriggerNode_IngestsPayloadAndInitializesContext()
    {
        var (engine, _, _) = CreateEngineAndHitl();

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Trigger Only Workflow",
            Nodes: [new("trigger-node", "Inbound Event", DurableWorkflowNodeType.TriggerNode, Config: new() { ["defaultChannel"] = "WhatsApp" })],
            Edges: []
        );

        var payload = new Dictionary<string, object?>
        {
            ["senderPhone"] = "+21698123456",
            ["senderName"] = "Nidhal SARL"
        };

        var result = await engine.StartExecutionAsync(_tenantId, dag, payload);

        result.Status.Should().Be(WorkflowExecutionState.Completed);
        result.Context["senderPhone"].Should().Be("+21698123456");
        result.Context["senderName"].Should().Be("Nidhal SARL");
        result.Context["defaultChannel"].Should().Be("WhatsApp");
    }

    [Fact]
    public async Task DurableWorkflow_AiPromptNode_TransformsPromptUsingContextVariables()
    {
        var (engine, _, _) = CreateEngineAndHitl();

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "AI Transformation Pipeline",
            Nodes:
            [
                new("t", "Trigger", DurableWorkflowNodeType.TriggerNode),
                new("ai", "Summarizer", DurableWorkflowNodeType.AiPromptNode, Config: new()
                {
                    ["promptTemplate"] = "Client {companyName} requested quotation of {quoteAmount} EUR",
                    ["taskType"] = "summarize",
                    ["outputVariable"] = "executiveSummary"
                })
            ],
            Edges: [new("t", "ai")]
        );

        var initial = new Dictionary<string, object?>
        {
            ["companyName"] = "Stripe EU",
            ["quoteAmount"] = 12500m
        };

        var result = await engine.StartExecutionAsync(_tenantId, dag, initial);

        result.Status.Should().Be(WorkflowExecutionState.Completed);
        result.Context.Should().ContainKey("executiveSummary");
        var summary = result.Context["executiveSummary"]?.ToString();
        summary.Should().Contain("Stripe EU");
        summary.Should().Contain("12500");
    }

    [Fact]
    public async Task DurableWorkflow_AiPromptNode_ClassifiesIntentBasedOnKeywords()
    {
        var (engine, _, _) = CreateEngineAndHitl();

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Intent Classification",
            Nodes:
            [
                new("t", "Trigger", DurableWorkflowNodeType.TriggerNode),
                new("ai", "Classifier", DurableWorkflowNodeType.AiPromptNode, Config: new()
                {
                    ["promptTemplate"] = "Inquiry: {text}",
                    ["taskType"] = "classify_intent",
                    ["outputVariable"] = "leadTier"
                })
            ],
            Edges: [new("t", "ai")]
        );

        var enterpriseResult = await engine.StartExecutionAsync(_tenantId, dag, new() { ["text"] = "Enterprise expansion inquiry with high priority" });
        enterpriseResult.Context["leadTier"].Should().Be("enterprise_deal_intent");

        var generalResult = await engine.StartExecutionAsync(_tenantId, dag, new() { ["text"] = "Question regarding standard login page" });
        generalResult.Context["leadTier"].Should().Be("general_inquiry_intent");
    }

    [Fact]
    public async Task DurableWorkflow_AiPromptNode_EvaluatesSentiment()
    {
        var (engine, _, _) = CreateEngineAndHitl();

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Sentiment Pipeline",
            Nodes:
            [
                new("t", "Trigger", DurableWorkflowNodeType.TriggerNode),
                new("ai", "Sentiment", DurableWorkflowNodeType.AiPromptNode, Config: new()
                {
                    ["promptTemplate"] = "Feedback: {msg}",
                    ["taskType"] = "sentiment",
                    ["outputVariable"] = "detectedSentiment"
                })
            ],
            Edges: [new("t", "ai")]
        );

        var positiveRes = await engine.StartExecutionAsync(_tenantId, dag, new() { ["msg"] = "This product delivers great results with good support!" });
        positiveRes.Context["detectedSentiment"].Should().Be("positive");

        var urgentRes = await engine.StartExecutionAsync(_tenantId, dag, new() { ["msg"] = "Server is throwing urgent errors, bad incident!" });
        urgentRes.Context["detectedSentiment"].Should().Be("urgent_negative");
    }

    [Fact]
    public async Task DurableWorkflow_ConditionNode_NumericComparison_FollowsTrueBranch()
    {
        var (engine, _, _) = CreateEngineAndHitl();

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Conditional Router",
            Nodes:
            [
                new("t", "Trigger", DurableWorkflowNodeType.TriggerNode),
                new("cond", "Check Amount Threshold", DurableWorkflowNodeType.ConditionNode, Config: new()
                {
                    ["variable"] = "dealAmount",
                    ["operator"] = ">=",
                    ["value"] = 5000m
                }),
                new("action_true", "Enterprise VIP Notification", DurableWorkflowNodeType.ActionNode, Subtype: "notify_channel"),
                new("action_false", "Standard Lead Routing", DurableWorkflowNodeType.ActionNode, Subtype: "notify_channel")
            ],
            Edges:
            [
                new("t", "cond"),
                new("cond", "action_true", ConditionBranch: "true"),
                new("cond", "action_false", ConditionBranch: "false")
            ]
        );

        var result = await engine.StartExecutionAsync(_tenantId, dag, new() { ["dealAmount"] = 7500m });

        result.Status.Should().Be(WorkflowExecutionState.Completed);
        result.Context.Should().ContainKey("cond_evaluation").WhoseValue.Should().Be(true);
        result.Context.Should().ContainKey("nodeId").WhoseValue.Should().Be("action_true");
    }

    [Fact]
    public async Task DurableWorkflow_ConditionNode_NumericComparison_FollowsFalseBranch()
    {
        var (engine, _, _) = CreateEngineAndHitl();

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Conditional Router",
            Nodes:
            [
                new("t", "Trigger", DurableWorkflowNodeType.TriggerNode),
                new("cond", "Check Amount Threshold", DurableWorkflowNodeType.ConditionNode, Config: new()
                {
                    ["variable"] = "dealAmount",
                    ["operator"] = ">=",
                    ["value"] = 5000m
                }),
                new("action_true", "Enterprise VIP Notification", DurableWorkflowNodeType.ActionNode, Subtype: "notify_channel"),
                new("action_false", "Standard Lead Routing", DurableWorkflowNodeType.ActionNode, Subtype: "notify_channel")
            ],
            Edges:
            [
                new("t", "cond"),
                new("cond", "action_true", ConditionBranch: "true"),
                new("cond", "action_false", ConditionBranch: "false")
            ]
        );

        var result = await engine.StartExecutionAsync(_tenantId, dag, new() { ["dealAmount"] = 1200m });

        result.Status.Should().Be(WorkflowExecutionState.Completed);
        result.Context.Should().ContainKey("cond_evaluation").WhoseValue.Should().Be(false);
        result.Context.Should().ContainKey("nodeId").WhoseValue.Should().Be("action_false");
    }

    [Fact]
    public async Task DurableWorkflow_ConditionNode_StringComparison_FollowsCorrectBranch()
    {
        var (engine, _, _) = CreateEngineAndHitl();

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Channel Router",
            Nodes:
            [
                new("t", "Trigger", DurableWorkflowNodeType.TriggerNode),
                new("cond", "Match Channel", DurableWorkflowNodeType.ConditionNode, Config: new()
                {
                    ["variable"] = "channel",
                    ["operator"] = "==",
                    ["value"] = "WhatsApp"
                }),
                new("wa_action", "WhatsApp Dispatch", DurableWorkflowNodeType.ActionNode, Subtype: "dispatch_whatsapp"),
                new("email_action", "Email Dispatch", DurableWorkflowNodeType.ActionNode, Subtype: "send_email")
            ],
            Edges:
            [
                new("t", "cond"),
                new("cond", "wa_action", ConditionBranch: "true"),
                new("cond", "email_action", ConditionBranch: "false")
            ]
        );

        var result = await engine.StartExecutionAsync(_tenantId, dag, new() { ["channel"] = "WhatsApp" });

        result.Status.Should().Be(WorkflowExecutionState.Completed);
        result.Context.Should().ContainKey("nodeId").WhoseValue.Should().Be("wa_action");
        result.Context["deliveryStatus"].Should().Be("Dispatched");
    }

    [Fact]
    public async Task DurableWorkflow_ActionNode_ExecutesSideEffectAndCapturesOutput()
    {
        var (engine, _, _) = CreateEngineAndHitl();

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Invoice Generator Action",
            Nodes:
            [
                new("t", "Trigger", DurableWorkflowNodeType.TriggerNode),
                new("inv", "Issue Tax Invoice", DurableWorkflowNodeType.ActionNode, Subtype: "generate_invoice")
            ],
            Edges: [new("t", "inv")]
        );

        var result = await engine.StartExecutionAsync(_tenantId, dag, new() { ["customer"] = "Global Tech" });

        result.Status.Should().Be(WorkflowExecutionState.Completed);
        result.Context.Should().ContainKey("invoiceNumber");
        result.Context["invoiceStatus"].Should().Be("Issued");
    }

    [Fact]
    public async Task DurableWorkflow_EventHistory_MaintainsOrderedSequence()
    {
        var (engine, _, _) = CreateEngineAndHitl();

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Audit Trace Workflow",
            Nodes:
            [
                new("t", "Trigger", DurableWorkflowNodeType.TriggerNode),
                new("a", "Action", DurableWorkflowNodeType.ActionNode, Subtype: "send_email")
            ],
            Edges: [new("t", "a")]
        );

        var exec = await engine.StartExecutionAsync(_tenantId, dag);
        var history = await engine.GetExecutionHistoryAsync(_tenantId, exec.ExecutionId);

        history.Should().NotBeEmpty();
        history.Count.Should().BeGreaterThanOrEqualTo(3);

        // Sequence: ExecutionStarted -> NodeCompleted (t) -> NodeCompleted (a) -> ExecutionCompleted
        history[0].EventType.Should().Be("ExecutionStarted");
        history.Last().EventType.Should().Be("ExecutionCompleted");

        // Verify timestamps are non-decreasing
        for (var i = 1; i < history.Count; i++)
        {
            history[i].TimestampUtc.Should().BeOnOrAfter(history[i - 1].TimestampUtc);
        }
    }

    #endregion

    #region 2. Human-In-The-Loop Suspension & Temporal State Replay

    [Fact]
    public async Task DurableWorkflow_HumanApprovalNode_SuspendsExecution_WithPendingApprovalTaskId()
    {
        var (engine, _, _) = CreateEngineAndHitl();

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "High Risk Approval Pipeline",
            Nodes:
            [
                new("t", "Trigger", DurableWorkflowNodeType.TriggerNode),
                new("gate", "CFO Approval Required", DurableWorkflowNodeType.HumanApprovalNode, Config: new()
                {
                    ["description"] = "Approval needed for transaction of {dealAmount} USD",
                    ["financialRiskAmount"] = 15000m
                }),
                new("action", "Disburse Funds", DurableWorkflowNodeType.ActionNode, Subtype: "send_email")
            ],
            Edges:
            [
                new("t", "gate"),
                new("gate", "action")
            ]
        );

        var result = await engine.StartExecutionAsync(_tenantId, dag, new() { ["dealAmount"] = 15000m });

        // Must suspend at the gate
        result.Status.Should().Be(WorkflowExecutionState.SuspendedAtHumanGate);
        result.CurrentNodeId.Should().Be("gate");
        result.PendingApprovalTaskId.Should().NotBeNull();
        result.CompletedAtUtc.Should().BeNull();

        // The subsequent action must NOT have executed yet
        result.Context.Should().NotContainKey("deliveryStatus");
    }

    [Fact]
    public async Task DurableWorkflow_HumanApprovalNode_RecordsExecutionSuspendedEvent()
    {
        var (engine, _, _) = CreateEngineAndHitl();

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Suspension Event Test",
            Nodes:
            [
                new("t", "Trigger", DurableWorkflowNodeType.TriggerNode),
                new("hitl", "Gate", DurableWorkflowNodeType.HumanApprovalNode, Config: new() { ["description"] = "Gate test" }),
                new("a", "Action", DurableWorkflowNodeType.ActionNode)
            ],
            Edges: [new("t", "hitl"), new("hitl", "a")]
        );

        var result = await engine.StartExecutionAsync(_tenantId, dag);
        var history = await engine.GetExecutionHistoryAsync(_tenantId, result.ExecutionId);

        var suspendedEvent = history.FirstOrDefault(e => e.EventType == "ExecutionSuspended");
        suspendedEvent.Should().NotBeNull();
        suspendedEvent!.NodeId.Should().Be("hitl");
        result.PendingApprovalTaskId.Should().NotBeNull();
        suspendedEvent.OutputPayloadJson.Should().Contain(result.PendingApprovalTaskId!.Value.ToString());
    }

    [Fact]
    public async Task DurableWorkflow_HumanApprovalNode_InterpolatesVariablesInDescription()
    {
        var (engine, hitlService, _) = CreateEngineAndHitl();

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Interpolation Test",
            Nodes:
            [
                new("t", "Trigger", DurableWorkflowNodeType.TriggerNode),
                new("gate", "Manager Gate", DurableWorkflowNodeType.HumanApprovalNode, Config: new()
                {
                    ["description"] = "Authorize refund for order #{orderId} to customer {customerName}"
                })
            ],
            Edges: [new("t", "gate")]
        );

        var initial = new Dictionary<string, object?>
        {
            ["orderId"] = "ORD-9871",
            ["customerName"] = "Sophie Germain"
        };

        var result = await engine.StartExecutionAsync(_tenantId, dag, initial);

        var task = await hitlService.GetTaskByIdAsync(_tenantId, result.PendingApprovalTaskId!.Value);
        task.Should().NotBeNull();
        task!.Description.Should().Be("Authorize refund for order #ORD-9871 to customer Sophie Germain");
    }

    [Fact]
    public async Task DurableWorkflow_ResumeApprovedTask_ReplaysStateAndCompletesWorkflow()
    {
        var (engine, hitlService, _) = CreateEngineAndHitl();

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Approve and Resume",
            Nodes:
            [
                new("t", "Inbound Deal", DurableWorkflowNodeType.TriggerNode),
                new("gate", "Director Approval", DurableWorkflowNodeType.HumanApprovalNode, Config: new() { ["description"] = "Sign-off" }),
                new("action", "Dispatch Confirmation", DurableWorkflowNodeType.ActionNode, Subtype: "send_email")
            ],
            Edges:
            [
                new("t", "gate"),
                new("gate", "action")
            ]
        );

        var suspended = await engine.StartExecutionAsync(_tenantId, dag, new() { ["dealTitle"] = "Acme Corp Expansion" });
        suspended.Status.Should().Be(WorkflowExecutionState.SuspendedAtHumanGate);

        // Approve task through HITL service
        var approvalResult = await hitlService.ApproveTaskAsync(
            _tenantId,
            suspended.PendingApprovalTaskId!.Value,
            approverEmail: "director@sgrowing.com",
            notes: "Approved under Q4 corporate expansion budget"
        );

        approvalResult.Success.Should().BeTrue();
        approvalResult.Execution.Should().NotBeNull();
        approvalResult.Execution!.Status.Should().Be(WorkflowExecutionState.Completed);
        approvalResult.Execution.PendingApprovalTaskId.Should().BeNull();
        approvalResult.Execution.Context["approvalDecision"].Should().Be("Approved");
        approvalResult.Execution.Context["approvedBy"].Should().Be("director@sgrowing.com");
        approvalResult.Execution.Context["approvalNotes"].Should().Be("Approved under Q4 corporate expansion budget");
        approvalResult.Execution.Context["deliveryStatus"].Should().Be("Dispatched");
    }

    [Fact]
    public async Task DurableWorkflow_ResumeApprovedTask_DoesNotReExecuteCompletedSideEffects()
    {
        // Temporal state replay test: Ensure past completed Action nodes are replayed from history
        // and their side-effects are NEVER invoked a second time!
        var action1InvocationCount = 0;
        var action2InvocationCount = 0;

        var mockActionExecutor = new Mock<IWorkflowActionExecutor>();
        mockActionExecutor
            .Setup(a => a.ExecuteActionAsync("action1", It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                action1InvocationCount++;
                return new Dictionary<string, object?> { ["action1_result"] = "executed_once" };
            });

        mockActionExecutor
            .Setup(a => a.ExecuteActionAsync("action2", It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                action2InvocationCount++;
                return new Dictionary<string, object?> { ["action2_result"] = "executed_after_gate" };
            });

        var (engine, hitlService, _) = CreateEngineAndHitl(mockActionExecutor.Object);

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Temporal Checkpoint Verification DAG",
            Nodes:
            [
                new("t", "Trigger", DurableWorkflowNodeType.TriggerNode),
                new("action1", "Pre-Gate Side-Effect", DurableWorkflowNodeType.ActionNode, Subtype: "send_email"),
                new("gate", "Human Gate", DurableWorkflowNodeType.HumanApprovalNode, Config: new() { ["description"] = "Verify gate" }),
                new("action2", "Post-Gate Side-Effect", DurableWorkflowNodeType.ActionNode, Subtype: "send_email")
            ],
            Edges:
            [
                new("t", "action1"),
                new("action1", "gate"),
                new("gate", "action2")
            ]
        );

        // 1. First execution up to gate
        var suspended = await engine.StartExecutionAsync(_tenantId, dag);
        suspended.Status.Should().Be(WorkflowExecutionState.SuspendedAtHumanGate);
        action1InvocationCount.Should().Be(1, "action1 should have executed once before suspension");
        action2InvocationCount.Should().Be(0, "action2 must not have executed yet");

        // 2. Resume execution from human approval
        var resumeResult = await hitlService.ApproveTaskAsync(
            _tenantId,
            suspended.PendingApprovalTaskId!.Value,
            "cfo@sgrowing.com",
            "Proceed"
        );

        resumeResult.Execution!.Status.Should().Be(WorkflowExecutionState.Completed);

        // Verify side-effect execution counts:
        // action1 must STILL be 1 (recovered from checkpoint event without repeating side-effect!)
        action1InvocationCount.Should().Be(1, "action1 side-effect MUST NOT be re-executed upon checkpoint recovery!");
        action2InvocationCount.Should().Be(1, "action2 must now be executed exactly once");
        resumeResult.Execution.ReplayedStepsCount.Should().BeGreaterThan(0, "engine must record replayed steps from history");
    }

    [Fact]
    public async Task DurableWorkflow_RejectTask_TerminatesWorkflowCleanly()
    {
        var (engine, hitlService, _) = CreateEngineAndHitl();

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Rejection Pipeline",
            Nodes:
            [
                new("t", "Trigger", DurableWorkflowNodeType.TriggerNode),
                new("gate", "Risk Gate", DurableWorkflowNodeType.HumanApprovalNode, Config: new() { ["description"] = "Validate compliance" }),
                new("action", "Disburse Wire", DurableWorkflowNodeType.ActionNode, Subtype: "send_email")
            ],
            Edges:
            [
                new("t", "gate"),
                new("gate", "action")
            ]
        );

        var suspended = await engine.StartExecutionAsync(_tenantId, dag);

        // Reject task
        var rejectResult = await hitlService.RejectTaskAsync(
            _tenantId,
            suspended.PendingApprovalTaskId!.Value,
            approverEmail: "compliance@sgrowing.com",
            reason: "Suspected sanctions compliance violation"
        );

        rejectResult.Success.Should().BeTrue();
        rejectResult.Execution!.Status.Should().Be(WorkflowExecutionState.Terminated);
        rejectResult.Execution.FailureReason.Should().Be("Suspected sanctions compliance violation");
        rejectResult.Execution.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task DurableWorkflow_RejectTask_PreventsSubsequentActionNodesFromExecuting()
    {
        var actionExecuted = false;
        var mockAction = new Mock<IWorkflowActionExecutor>();
        mockAction
            .Setup(a => a.ExecuteActionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .Callback(() => actionExecuted = true)
            .ReturnsAsync([]);

        var (engine, hitlService, _) = CreateEngineAndHitl(mockAction.Object);

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Rejection Blocks Actions",
            Nodes:
            [
                new("t", "Trigger", DurableWorkflowNodeType.TriggerNode),
                new("gate", "Approval Gate", DurableWorkflowNodeType.HumanApprovalNode),
                new("action", "Dangerous Wire", DurableWorkflowNodeType.ActionNode)
            ],
            Edges: [new("t", "gate"), new("gate", "action")]
        );

        var suspended = await engine.StartExecutionAsync(_tenantId, dag);

        await hitlService.RejectTaskAsync(_tenantId, suspended.PendingApprovalTaskId!.Value, "fraud@sgrowing.com", "High fraud risk");

        actionExecuted.Should().BeFalse("subsequent action nodes must never execute after workflow termination!");
    }

    [Fact]
    public async Task DurableWorkflow_CheckpointRecovery_ReconstitutesStateFromEventHistory()
    {
        var (engine, _, _) = CreateEngineAndHitl();

        var execId = Guid.NewGuid();
        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "History Reconstitution",
            Nodes:
            [
                new("t", "Trigger", DurableWorkflowNodeType.TriggerNode),
                new("action", "Create CRM Deal", DurableWorkflowNodeType.ActionNode, Subtype: "create_deal")
            ],
            Edges: [new("t", "action")]
        );

        // Execute once
        var exec1 = await engine.StartExecutionAsync(_tenantId, dag, new() { ["initialParam"] = "value123" }, executionId: execId);
        exec1.Status.Should().Be(WorkflowExecutionState.Completed);

        // Now replay execution from history
        var replayed = await engine.ReplayExecutionAsync(_tenantId, execId);
        replayed.Status.Should().Be(WorkflowExecutionState.Completed);
        replayed.ReplayedStepsCount.Should().BeGreaterThan(0);
        replayed.Context["initialParam"].Should().Be("value123");
    }

    [Fact]
    public async Task DurableWorkflow_ResumeNonSuspendedExecution_ThrowsInvalidOperationException()
    {
        var (engine, _, _) = CreateEngineAndHitl();

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Quick Finish",
            Nodes: [new("t", "Trigger", DurableWorkflowNodeType.TriggerNode)],
            Edges: []
        );

        var completed = await engine.StartExecutionAsync(_tenantId, dag);

        var act = async () => await engine.ResumeExecutionAsync(_tenantId, completed.ExecutionId, isApproved: true, "test@sgrowing.com");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot resume execution in state 'Completed'*");
    }

    [Fact]
    public async Task DurableWorkflow_TenantIsolation_MismatchThrowsUnauthorizedAccessException()
    {
        var (engine, _, _) = CreateEngineAndHitl();

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Tenant Isolation",
            Nodes:
            [
                new("t", "Trigger", DurableWorkflowNodeType.TriggerNode),
                new("gate", "Gate", DurableWorkflowNodeType.HumanApprovalNode)
            ],
            Edges: [new("t", "gate")]
        );

        var exec = await engine.StartExecutionAsync(_tenantId, dag);

        var alienTenantId = Guid.NewGuid();
        var act = async () => await engine.ResumeExecutionAsync(alienTenantId, exec.ExecutionId, isApproved: true, "hacker@evil.com");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task DurableWorkflow_ExecutionFailure_CapturesErrorAndSetsFailedState()
    {
        var mockAction = new Mock<IWorkflowActionExecutor>();
        mockAction
            .Setup(a => a.ExecuteActionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Remote webhook endpoint connection refused"));

        var (engine, _, _) = CreateEngineAndHitl(mockAction.Object);

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Failing Workflow",
            Nodes:
            [
                new("t", "Trigger", DurableWorkflowNodeType.TriggerNode),
                new("failing_node", "Webhook Dispatch", DurableWorkflowNodeType.ActionNode)
            ],
            Edges: [new("t", "failing_node")]
        );

        var failed = await engine.StartExecutionAsync(_tenantId, dag);

        failed.Status.Should().Be(WorkflowExecutionState.Failed);
        failed.FailureReason.Should().Contain("Remote webhook endpoint connection refused");
        failed.CompletedAtUtc.Should().NotBeNull();

        var history = await engine.GetExecutionHistoryAsync(_tenantId, failed.ExecutionId);
        history.Should().Contain(e => e.EventType == "ExecutionFailed");
    }

    #endregion

    #region 3. Human-In-The-Loop Approval Service

    [Fact]
    public async Task HitlService_CreateApprovalTask_BroadcastsSignalROmniHubNotification()
    {
        var (_, hitlService, _) = CreateEngineAndHitl();
        var execId = Guid.NewGuid();

        var task = await hitlService.CreateApprovalTaskAsync(
            _tenantId,
            execId,
            nodeId: "cfo_gate",
            description: "High value B2B transfer approval",
            financialRiskAmount: 25000m
        );

        task.Should().NotBeNull();
        task.Status.Should().Be("Pending");
        task.FinancialRiskAmount.Should().Be(25000m);

        // Verify SignalR broadcast to tenant group
        _mockClients.Verify(c => c.Group(OmniHub.GetTenantGroup(_tenantId)), Times.Once);
        _mockClientProxy.Verify(c => c.OnApprovalTaskCreated(It.Is<WorkflowApprovalTaskDto>(dto =>
            dto.Id == task.Id &&
            dto.TenantId == _tenantId &&
            dto.ExecutionId == execId &&
            dto.FinancialRiskAmount == 25000m
        )), Times.Once);
    }

    [Fact]
    public async Task HitlService_ApproveTask_UpdatesStatusAndDecidedAtUtc()
    {
        var (engine, hitlService, _) = CreateEngineAndHitl();

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Approval Test",
            Nodes: [new("t", "Trigger", DurableWorkflowNodeType.TriggerNode), new("g", "Gate", DurableWorkflowNodeType.HumanApprovalNode)],
            Edges: [new("t", "g")]
        );

        var exec = await engine.StartExecutionAsync(_tenantId, dag);

        var result = await hitlService.ApproveTaskAsync(_tenantId, exec.PendingApprovalTaskId!.Value, "lead@sgrowing.com", "Looks good");

        result.Success.Should().BeTrue();
        result.Task.Status.Should().Be("Approved");
        result.Task.ApproverEmail.Should().Be("lead@sgrowing.com");
        result.Task.DecisionNotes.Should().Be("Looks good");
        result.Task.DecidedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task HitlService_RejectTask_UpdatesStatusAndReason()
    {
        var (engine, hitlService, _) = CreateEngineAndHitl();

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Rejection Test",
            Nodes: [new("t", "Trigger", DurableWorkflowNodeType.TriggerNode), new("g", "Gate", DurableWorkflowNodeType.HumanApprovalNode)],
            Edges: [new("t", "g")]
        );

        var exec = await engine.StartExecutionAsync(_tenantId, dag);

        var result = await hitlService.RejectTaskAsync(_tenantId, exec.PendingApprovalTaskId!.Value, "audit@sgrowing.com", "Insufficient documentation");

        result.Success.Should().BeTrue();
        result.Task.Status.Should().Be("Rejected");
        result.Task.ApproverEmail.Should().Be("audit@sgrowing.com");
        result.Task.DecisionNotes.Should().Be("Insufficient documentation");
        result.Task.DecidedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task HitlService_NonExistentTask_ThrowsKeyNotFoundException()
    {
        var (_, hitlService, _) = CreateEngineAndHitl();

        var act = async () => await hitlService.ApproveTaskAsync(_tenantId, Guid.NewGuid(), "user@sgrowing.com");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task HitlService_AlreadyDecidedTask_ThrowsInvalidOperationException()
    {
        var (engine, hitlService, _) = CreateEngineAndHitl();

        var dag = new DurableWorkflowDag(
            WorkflowId: Guid.NewGuid(),
            Name: "Double Approval Test",
            Nodes: [new("t", "Trigger", DurableWorkflowNodeType.TriggerNode), new("g", "Gate", DurableWorkflowNodeType.HumanApprovalNode)],
            Edges: [new("t", "g")]
        );

        var exec = await engine.StartExecutionAsync(_tenantId, dag);
        var taskId = exec.PendingApprovalTaskId!.Value;

        // First approval succeeds
        await hitlService.ApproveTaskAsync(_tenantId, taskId, "user1@sgrowing.com");

        // Second approval attempt on already approved task fails
        var secondAct = async () => await hitlService.ApproveTaskAsync(_tenantId, taskId, "user2@sgrowing.com");
        await secondAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already 'Approved'*");
    }

    #endregion

    #region 4. Spatially-Aware Timezone Pacing & Quiet Hours

    [Theory]
    [InlineData("+12125551234", "America/New_York")]
    [InlineData("+213770123456", "Africa/Algiers")]
    [InlineData("+21698123456", "Africa/Tunis")]
    [InlineData("+971501234567", "Asia/Dubai")]
    [InlineData("+447911123456", "Europe/London")]
    [InlineData("+33612345678", "Europe/Paris")]
    [InlineData("+966501234567", "Asia/Riyadh")]
    public void LocalTimezonePacing_InfersFromInternationalPhonePrefixes(string phone, string expectedTz)
    {
        var inferred = _pacingService.InferTimezoneFromPhoneNumber(phone);
        inferred.Should().Be(expectedTz);
    }

    [Fact]
    public void LocalTimezonePacing_InfersFromCoordinates_AlgiersAndDubaiAndLondon()
    {
        // Algiers coordinates: 36.75, 3.05
        var algiers = _pacingService.InferTimezoneFromCoordinates(36.75, 3.05);
        algiers.Should().Be("Africa/Algiers");

        // Dubai coordinates: 25.20, 55.27
        var dubai = _pacingService.InferTimezoneFromCoordinates(25.20, 55.27);
        dubai.Should().Be("Asia/Dubai");

        // London coordinates: 51.50, -0.12
        var london = _pacingService.InferTimezoneFromCoordinates(51.50, -0.12);
        london.Should().Be("Europe/London");
    }

    [Fact]
    public void LocalTimezonePacing_DetectsQuietHours_MidnightWrap()
    {
        var quietStart = new TimeSpan(21, 0, 0); // 21:00 (9 PM)
        var quietEnd = new TimeSpan(8, 30, 0);   // 08:30 (8:30 AM)

        // 23:30 local -> In quiet hours
        var lateNight = new DateTime(2026, 11, 4, 23, 30, 0);
        _pacingService.IsInQuietHours(lateNight, quietStart, quietEnd).Should().BeTrue();

        // 05:00 local -> In quiet hours
        var earlyMorning = new DateTime(2026, 11, 5, 5, 0, 0);
        _pacingService.IsInQuietHours(earlyMorning, quietStart, quietEnd).Should().BeTrue();

        // 14:00 local -> NOT in quiet hours (Active daylight window)
        var afternoon = new DateTime(2026, 11, 5, 14, 0, 0);
        _pacingService.IsInQuietHours(afternoon, quietStart, quietEnd).Should().BeFalse();
    }

    [Fact]
    public void LocalTimezonePacing_DetectsMenaWeekend_FridayAndSaturday()
    {
        // Friday in Dubai (2026-11-06)
        var friday = new DateTime(2026, 11, 6, 14, 0, 0);
        _pacingService.IsWeekend(friday, "Asia/Dubai").Should().BeTrue("Friday is weekend in MENA");

        // Saturday in Dubai (2026-11-07)
        var saturday = new DateTime(2026, 11, 7, 14, 0, 0);
        _pacingService.IsWeekend(saturday, "Asia/Dubai").Should().BeTrue("Saturday is weekend in MENA");

        // Sunday in Dubai (2026-11-08)
        var sunday = new DateTime(2026, 11, 8, 14, 0, 0);
        _pacingService.IsWeekend(sunday, "Asia/Dubai").Should().BeFalse("Sunday is a regular business working day in MENA");
    }

    [Fact]
    public void LocalTimezonePacing_DetectsStandardWeekend_SaturdayAndSunday()
    {
        // Friday in London (2026-11-06)
        var friday = new DateTime(2026, 11, 6, 14, 0, 0);
        _pacingService.IsWeekend(friday, "Europe/London").Should().BeFalse("Friday is a business working day in UK/Western markets");

        // Saturday in London (2026-11-07)
        var saturday = new DateTime(2026, 11, 7, 14, 0, 0);
        _pacingService.IsWeekend(saturday, "Europe/London").Should().BeTrue("Saturday is weekend");

        // Sunday in London (2026-11-08)
        var sunday = new DateTime(2026, 11, 8, 14, 0, 0);
        _pacingService.IsWeekend(sunday, "Europe/London").Should().BeTrue("Sunday is weekend");
    }

    [Fact]
    public void LocalTimezonePacing_CalculatesNextSafeDispatchUtc_WhenAlreadySafe_ReturnsSameUtc()
    {
        // Wednesday at 14:00 UTC for London (UTC+0). Not quiet, not weekend.
        var wednesdayDaytime = new DateTime(2026, 11, 4, 14, 0, 0, DateTimeKind.Utc);

        var nextSafe = _pacingService.CalculateNextSafeDispatchUtc(
            wednesdayDaytime,
            "Europe/London",
            new TimeSpan(21, 0, 0),
            new TimeSpan(8, 30, 0),
            excludeWeekends: true
        );

        nextSafe.Should().Be(wednesdayDaytime, "safe daylight hours should be immediately actionable");
    }

    [Fact]
    public void LocalTimezonePacing_CalculatesNextSafeDispatchUtc_DuringEveningQuietHours_AdvancesToNextMorning()
    {
        // Wednesday at 22:30 UTC in London -> in quiet hours (21:00 - 08:30)
        var wednesdayNight = new DateTime(2026, 11, 4, 22, 30, 0, DateTimeKind.Utc);

        var nextSafe = _pacingService.CalculateNextSafeDispatchUtc(
            wednesdayNight,
            "Europe/London",
            new TimeSpan(21, 0, 0),
            new TimeSpan(8, 30, 0),
            excludeWeekends: true
        );

        // Next morning Thursday 08:30 UTC
        nextSafe.Should().Be(new DateTime(2026, 11, 5, 8, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void LocalTimezonePacing_CalculatesNextSafeDispatchUtc_DuringMenaWeekend_AdvancesToSundayMorning()
    {
        // Friday 2026-11-06 14:00 local time in Dubai (UTC+4 -> 10:00 UTC)
        // Dubai is MENA: weekend is Friday + Saturday.
        // Earliest safe business day is Sunday 2026-11-08 at 08:30 local time (which is 04:30 UTC)
        var fridayUtc = new DateTime(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc);

        var nextSafe = _pacingService.CalculateNextSafeDispatchUtc(
            fridayUtc,
            "Asia/Dubai",
            new TimeSpan(21, 0, 0),
            new TimeSpan(8, 30, 0),
            excludeWeekends: true
        );

        // Sunday morning 08:30 Dubai time = 04:30 UTC
        nextSafe.DayOfWeek.Should().Be(DayOfWeek.Sunday);
        var localSafe = TimeZoneInfo.ConvertTimeFromUtc(nextSafe, LocalTimezonePacingService.ResolveTimeZone("Asia/Dubai"));
        localSafe.TimeOfDay.Should().Be(new TimeSpan(8, 30, 0));
    }

    [Fact]
    public void LocalTimezonePacing_EvaluatePacing_ReturnsComprehensiveResultDto()
    {
        var testUtc = new DateTime(2026, 11, 4, 14, 0, 0, DateTimeKind.Utc);

        var result = _pacingService.EvaluatePacing(
            currentUtc: testUtc,
            phoneNumber: "+213770123456",
            quietStart: new TimeSpan(21, 0, 0),
            quietEnd: new TimeSpan(8, 30, 0),
            excludeWeekends: true
        );

        result.TimezoneId.Should().Be("Africa/Algiers");
        result.CanDispatchNow.Should().BeTrue();
        result.IsInQuietHours.Should().BeFalse();
        result.IsWeekend.Should().BeFalse();
        result.NextSafeDispatchUtc.Should().Be(testUtc);
        result.Reason.Should().Contain("active safe window");
    }

    #endregion
}
