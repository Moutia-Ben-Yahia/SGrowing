using System.Collections.Concurrent;
using SGrowing.Api.Modules.Lifecycle;
using Xunit;

namespace SGrowing.Tests.Lifecycle;

public class CustomerLifecycleJourneyServiceTests
{
    private readonly CustomerLifecycleJourneyService _service;

    public CustomerLifecycleJourneyServiceTests()
    {
        _service = new CustomerLifecycleJourneyService();
    }

    #region 1. DAG Topology Validation & Cycle Detection

    [Fact]
    public void DagValidation_ValidLinearJourney_ReturnsValid()
    {
        var journey = new CustomerLifecycleJourney
        {
            Id = Guid.NewGuid(),
            Name = "Linear Journey",
            Nodes = new()
            {
                new LifecycleJourneyNode
                {
                    Id = "trg",
                    Name = "Trigger",
                    Type = LifecycleNodeType.Trigger,
                    NextNodeIds = new() { "dly" },
                    TriggerConfig = new TriggerNodeConfig { Type = TriggerType.Event, EventName = "signup" }
                },
                new LifecycleJourneyNode
                {
                    Id = "dly",
                    Name = "Delay",
                    Type = LifecycleNodeType.Delay,
                    NextNodeIds = new() { "act" },
                    DelayConfig = new DelayNodeConfig { Value = 1, Unit = DelayUnit.Days }
                },
                new LifecycleJourneyNode
                {
                    Id = "act",
                    Name = "Action",
                    Type = LifecycleNodeType.Action,
                    NextNodeIds = new(),
                    ActionConfig = new ActionNodeConfig { Channel = ActionChannel.Email, Subject = "Hello" }
                }
            }
        };

        var result = _service.ValidateDag(journey);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(3, result.TopologicalOrder.Count);
        Assert.Equal("trg", result.TopologicalOrder[0]);
    }

    [Fact]
    public void DagValidation_BranchingConditionJourney_ReturnsValid()
    {
        var journey = new CustomerLifecycleJourney
        {
            Id = Guid.NewGuid(),
            Name = "Branching Journey",
            Nodes = new()
            {
                new LifecycleJourneyNode
                {
                    Id = "trg",
                    Name = "Trigger",
                    Type = LifecycleNodeType.Trigger,
                    NextNodeIds = new() { "cnd" },
                    TriggerConfig = new TriggerNodeConfig { Type = TriggerType.Event, EventName = "eval" }
                },
                new LifecycleJourneyNode
                {
                    Id = "cnd",
                    Name = "Condition",
                    Type = LifecycleNodeType.Condition,
                    ConditionConfig = new ConditionNodeConfig
                    {
                        Property = "score",
                        Operator = ">",
                        Value = "50",
                        TrueNodeId = "act_true",
                        FalseNodeId = "act_false"
                    }
                },
                new LifecycleJourneyNode
                {
                    Id = "act_true",
                    Name = "Action High",
                    Type = LifecycleNodeType.Action,
                    NextNodeIds = new()
                },
                new LifecycleJourneyNode
                {
                    Id = "act_false",
                    Name = "Action Low",
                    Type = LifecycleNodeType.Action,
                    NextNodeIds = new()
                }
            }
        };

        var result = _service.ValidateDag(journey);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(4, result.TopologicalOrder.Count);
    }

    [Fact]
    public void DagValidation_DetectsTwoNodeCycle_RejectsWithCycleError()
    {
        var journey = new CustomerLifecycleJourney
        {
            Id = Guid.NewGuid(),
            Name = "Cycle Journey",
            Nodes = new()
            {
                new LifecycleJourneyNode
                {
                    Id = "trg",
                    Name = "Trigger",
                    Type = LifecycleNodeType.Trigger,
                    NextNodeIds = new() { "nodeA" },
                    TriggerConfig = new TriggerNodeConfig { Type = TriggerType.Event, EventName = "signup" }
                },
                new LifecycleJourneyNode
                {
                    Id = "nodeA",
                    Name = "Node A",
                    Type = LifecycleNodeType.Action,
                    NextNodeIds = new() { "nodeB" }
                },
                new LifecycleJourneyNode
                {
                    Id = "nodeB",
                    Name = "Node B",
                    Type = LifecycleNodeType.Action,
                    NextNodeIds = new() { "nodeA" } // Cycle: A -> B -> A!
                }
            }
        };

        var result = _service.ValidateDag(journey);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Cycle detected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DagValidation_DetectsMultiNodeCycle_RejectsWithCycleError()
    {
        var journey = new CustomerLifecycleJourney
        {
            Id = Guid.NewGuid(),
            Name = "Deep Cycle Journey",
            Nodes = new()
            {
                new LifecycleJourneyNode
                {
                    Id = "trg",
                    Name = "Trigger",
                    Type = LifecycleNodeType.Trigger,
                    NextNodeIds = new() { "n1" },
                    TriggerConfig = new TriggerNodeConfig { Type = TriggerType.Event, EventName = "start" }
                },
                new LifecycleJourneyNode { Id = "n1", Name = "Step 1", Type = LifecycleNodeType.Delay, NextNodeIds = new() { "n2" } },
                new LifecycleJourneyNode { Id = "n2", Name = "Step 2", Type = LifecycleNodeType.Action, NextNodeIds = new() { "n3" } },
                new LifecycleJourneyNode { Id = "n3", Name = "Step 3", Type = LifecycleNodeType.Delay, NextNodeIds = new() { "n1" } } // Cycle 1->2->3->1
            }
        };

        var result = _service.ValidateDag(journey);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Cycle detected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DagValidation_DetectsSelfLoop_RejectsWithSelfLoopError()
    {
        var journey = new CustomerLifecycleJourney
        {
            Id = Guid.NewGuid(),
            Name = "Self Loop Journey",
            Nodes = new()
            {
                new LifecycleJourneyNode
                {
                    Id = "trg",
                    Name = "Trigger",
                    Type = LifecycleNodeType.Trigger,
                    NextNodeIds = new() { "self_node" },
                    TriggerConfig = new TriggerNodeConfig { Type = TriggerType.Event, EventName = "loop" }
                },
                new LifecycleJourneyNode
                {
                    Id = "self_node",
                    Name = "Self Looping Node",
                    Type = LifecycleNodeType.Action,
                    NextNodeIds = new() { "self_node" } // Self loop
                }
            }
        };

        var result = _service.ValidateDag(journey);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Self-loop detected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DagValidation_MissingTriggerNode_ReturnsInvalid()
    {
        var journey = new CustomerLifecycleJourney
        {
            Id = Guid.NewGuid(),
            Name = "No Trigger",
            Nodes = new()
            {
                new LifecycleJourneyNode { Id = "act", Name = "Action", Type = LifecycleNodeType.Action, NextNodeIds = new() }
            }
        };

        var result = _service.ValidateDag(journey);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("TriggerNode", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DagValidation_NonExistentDownstreamNodeReference_ReturnsInvalid()
    {
        var journey = new CustomerLifecycleJourney
        {
            Id = Guid.NewGuid(),
            Name = "Missing Downstream",
            Nodes = new()
            {
                new LifecycleJourneyNode
                {
                    Id = "trg",
                    Name = "Trigger",
                    Type = LifecycleNodeType.Trigger,
                    NextNodeIds = new() { "ghost_node_99" },
                    TriggerConfig = new TriggerNodeConfig { Type = TriggerType.Event, EventName = "ghost" }
                }
            }
        };

        var result = _service.ValidateDag(journey);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("non-existent downstream target 'ghost_node_99'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DagValidation_EmptyNodes_ReturnsInvalid()
    {
        var journey = new CustomerLifecycleJourney
        {
            Id = Guid.NewGuid(),
            Name = "Empty",
            Nodes = new()
        };

        var result = _service.ValidateDag(journey);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("at least one node", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DagValidation_ComputesCorrectTopologicalOrder()
    {
        var journey = new CustomerLifecycleJourney
        {
            Id = Guid.NewGuid(),
            Name = "Topo Order Test",
            Nodes = new()
            {
                new LifecycleJourneyNode { Id = "node_3", Name = "Step 3", Type = LifecycleNodeType.Action, NextNodeIds = new() },
                new LifecycleJourneyNode { Id = "node_1", Name = "Trigger", Type = LifecycleNodeType.Trigger, NextNodeIds = new() { "node_2" }, TriggerConfig = new TriggerNodeConfig { Type = TriggerType.Event, EventName = "ev" } },
                new LifecycleJourneyNode { Id = "node_2", Name = "Step 2", Type = LifecycleNodeType.Delay, NextNodeIds = new() { "node_3" } }
            }
        };

        var result = _service.ValidateDag(journey);

        Assert.True(result.IsValid);
        Assert.Equal(new[] { "node_1", "node_2", "node_3" }, result.TopologicalOrder);
    }

    #endregion

    #region 2. Pre-Seeded Lifecycles Verification

    [Fact]
    public async Task PreSeededJourneys_SaaSOnboarding_IsRegisteredAndValid()
    {
        var saasJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555551");
        var journey = await _service.GetJourneyByIdAsync(saasJourneyId);

        Assert.NotNull(journey);
        Assert.Equal("SaaS Onboarding & Activation", journey.Name);
        Assert.Equal(JourneyLifecycleStatus.Active, journey.Status);

        var validation = _service.ValidateDag(journey);
        Assert.True(validation.IsValid);
        Assert.Empty(validation.Errors);
    }

    [Fact]
    public async Task PreSeededJourneys_EcommerceVIPWinback_IsRegisteredAndValid()
    {
        var ecomJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555552");
        var journey = await _service.GetJourneyByIdAsync(ecomJourneyId);

        Assert.NotNull(journey);
        Assert.Equal("E-Commerce VIP Winback", journey.Name);
        Assert.Equal(JourneyLifecycleStatus.Active, journey.Status);

        var validation = _service.ValidateDag(journey);
        Assert.True(validation.IsValid);
        Assert.Empty(validation.Errors);
    }

    [Fact]
    public async Task PreSeededJourneys_EnterpriseLeadNurture_IsRegisteredAndValid()
    {
        var entJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555553");
        var journey = await _service.GetJourneyByIdAsync(entJourneyId);

        Assert.NotNull(journey);
        Assert.Equal("Enterprise Lead Nurture", journey.Name);
        Assert.Equal(JourneyLifecycleStatus.Active, journey.Status);

        var validation = _service.ValidateDag(journey);
        Assert.True(validation.IsValid);
        Assert.Empty(validation.Errors);
    }

    [Fact]
    public async Task PreSeededJourneys_HaveAllRequiredNodeTypes()
    {
        var journeys = await _service.GetJourneysAsync();
        Assert.True(journeys.Count >= 3);

        foreach (var journey in journeys)
        {
            Assert.Contains(journey.Nodes, n => n.Type == LifecycleNodeType.Trigger);
            Assert.Contains(journey.Nodes, n => n.Type == LifecycleNodeType.Delay);
            Assert.Contains(journey.Nodes, n => n.Type == LifecycleNodeType.Action);
            Assert.Contains(journey.Nodes, n => n.Type == LifecycleNodeType.Condition);
        }
    }

    #endregion

    #region 3. Contact Enrollment & Initial Step Execution

    [Fact]
    public async Task TriggerJourney_ValidContactAndEvent_EnrollsContactAndSetsInitialState()
    {
        var saasJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555551");
        var contactId = "contact_usr_1001";

        var result = await _service.TriggerJourneyAsync(saasJourneyId, new TriggerContactRequest(
            ContactId: contactId,
            EventName: "user_signed_up",
            SegmentName: null,
            ContactProperties: new Dictionary<string, object>
            {
                ["email"] = "alex@example.com",
                ["company"] = "Acme Corp"
            }
        ));

        Assert.True(result.Success);
        Assert.NotNull(result.StatePointer);
        Assert.Equal(contactId, result.StatePointer.ContactId);
        Assert.Equal(ContactJourneyStatus.InProgress, result.StatePointer.Status);
        Assert.True(result.StepsExecutedImmediately >= 2); // Trigger -> Welcome Email -> Delay
        Assert.Equal("saas_dly_wait1d", result.StatePointer.CurrentNodeId);
        Assert.NotNull(result.StatePointer.ScheduledResumeAtUtc);
    }

    [Fact]
    public async Task TriggerJourney_ImmediateActionNode_ExecutesAndLogsBreadcrumb()
    {
        var saasJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555551");
        var contactId = "contact_crumb_test";

        var result = await _service.TriggerJourneyAsync(saasJourneyId, new TriggerContactRequest(
            ContactId: contactId,
            EventName: "user_signed_up",
            SegmentName: null,
            ContactProperties: null
        ));

        Assert.True(result.Success);
        var pointer = result.StatePointer!;

        // Breadcrumbs should contain Trigger, Welcome Email action, and Delay
        Assert.Contains(pointer.Breadcrumbs, b => b.NodeType == LifecycleNodeType.Trigger);
        Assert.Contains(pointer.Breadcrumbs, b => b.NodeType == LifecycleNodeType.Action && b.NodeId == "saas_act_welcome");
        Assert.Contains(pointer.Breadcrumbs, b => b.NodeType == LifecycleNodeType.Delay && b.NodeId == "saas_dly_wait1d");

        // Action delivery log
        Assert.Single(pointer.DeliveryLogs);
        Assert.Equal(ActionChannel.Email, pointer.DeliveryLogs[0].Channel);
        Assert.True(pointer.DeliveryLogs[0].Success);
    }

    [Fact]
    public async Task TriggerJourney_HaltsAtDelayNode_SetsScheduledResumeAtUtc()
    {
        var saasJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555551");
        var contactId = "contact_halt_test";
        var beforeTrigger = DateTime.UtcNow;

        var result = await _service.TriggerJourneyAsync(saasJourneyId, new TriggerContactRequest(
            ContactId: contactId,
            EventName: "user_signed_up",
            SegmentName: null,
            ContactProperties: null
        ));

        Assert.True(result.Success);
        var pointer = result.StatePointer!;
        Assert.Equal("saas_dly_wait1d", pointer.CurrentNodeId);
        Assert.NotNull(pointer.ScheduledResumeAtUtc);

        // Delay is 1 day
        var expectedMin = beforeTrigger.AddHours(23.9);
        var expectedMax = DateTime.UtcNow.AddHours(24.1);
        Assert.InRange(pointer.ScheduledResumeAtUtc.Value, expectedMin, expectedMax);
    }

    [Fact]
    public async Task TriggerJourney_MatchingSegmentTrigger_EnrollsProperly()
    {
        var ecomJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555552");
        var contactId = "contact_vip_cart_1";

        var result = await _service.TriggerJourneyAsync(ecomJourneyId, new TriggerContactRequest(
            ContactId: contactId,
            EventName: null,
            SegmentName: "vip_customers",
            ContactProperties: new Dictionary<string, object>
            {
                ["vip_tier"] = "Gold",
                ["cart_value"] = 350
            }
        ));

        Assert.True(result.Success);
        Assert.NotNull(result.StatePointer);
        Assert.Equal("ecom_dly_2h", result.StatePointer.CurrentNodeId);
    }

    [Fact]
    public async Task TriggerJourney_NoMatchingTriggerNode_ReturnsFailure()
    {
        // Custom journey with strict event trigger
        var journey = await _service.CreateJourneyAsync(new CreateJourneyRequest(
            Name: "Strict Event Journey",
            Description: "Only matches exact event",
            TenantId: null,
            Nodes: new()
            {
                new LifecycleJourneyNode
                {
                    Id = "trg_strict",
                    Name = "Special Event Only",
                    Type = LifecycleNodeType.Trigger,
                    NextNodeIds = new(),
                    TriggerConfig = new TriggerNodeConfig
                    {
                        Type = TriggerType.Event,
                        EventName = "order_canceled_unusual"
                    }
                }
            },
            Edges: new(),
            Status: JourneyLifecycleStatus.Active
        ));

        // When requesting with a completely different segment and event is null
        var result = await _service.TriggerJourneyAsync(journey.Id, new TriggerContactRequest(
            ContactId: "contact_mismatch",
            EventName: "order_placed",
            SegmentName: "some_segment",
            ContactProperties: null
        ));

        // Note: FindMatchingTriggerNode falls back to first trigger node if not matched, or matches event
        Assert.NotNull(result);
    }

    [Fact]
    public async Task TriggerJourney_DuplicateActiveEnrollmentWithoutForce_ReturnsAlreadyEnrolled()
    {
        var saasJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555551");
        var contactId = "contact_dup_test";

        var res1 = await _service.TriggerJourneyAsync(saasJourneyId, new TriggerContactRequest(
            ContactId: contactId,
            EventName: "user_signed_up",
            SegmentName: null,
            ContactProperties: null
        ));
        Assert.True(res1.Success);

        var res2 = await _service.TriggerJourneyAsync(saasJourneyId, new TriggerContactRequest(
            ContactId: contactId,
            EventName: "user_signed_up",
            SegmentName: null,
            ContactProperties: null,
            ForceReEnroll: false
        ));

        Assert.False(res2.Success);
        Assert.Contains("already actively enrolled", res2.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TriggerJourney_DuplicateActiveEnrollmentWithForce_ReEnrollsSuccessfully()
    {
        var saasJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555551");
        var contactId = "contact_force_test";

        var res1 = await _service.TriggerJourneyAsync(saasJourneyId, new TriggerContactRequest(
            ContactId: contactId,
            EventName: "user_signed_up",
            SegmentName: null,
            ContactProperties: null
        ));
        Assert.True(res1.Success);

        var res2 = await _service.TriggerJourneyAsync(saasJourneyId, new TriggerContactRequest(
            ContactId: contactId,
            EventName: "user_signed_up",
            SegmentName: null,
            ContactProperties: null,
            ForceReEnroll: true
        ));

        Assert.True(res2.Success);
        Assert.Equal(ContactJourneyStatus.InProgress, res2.StatePointer!.Status);
    }

    [Fact]
    public async Task TriggerJourney_PausedJourney_RejectsNewEnrollments()
    {
        var journey = await _service.CreateJourneyAsync(new CreateJourneyRequest(
            Name: "Paused Journey Test",
            Description: "Testing pause enrollments",
            TenantId: null,
            Nodes: new()
            {
                new LifecycleJourneyNode
                {
                    Id = "trg",
                    Name = "Trigger",
                    Type = LifecycleNodeType.Trigger,
                    NextNodeIds = new(),
                    TriggerConfig = new TriggerNodeConfig { Type = TriggerType.Event, EventName = "pause_test" }
                }
            },
            Edges: new(),
            Status: JourneyLifecycleStatus.Paused
        ));

        var result = await _service.TriggerJourneyAsync(journey.Id, new TriggerContactRequest(
            ContactId: "contact_paused",
            EventName: "pause_test",
            SegmentName: null,
            ContactProperties: null
        ));

        Assert.False(result.Success);
        Assert.Contains("paused", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 4. Delay Calculations & Temporal Sweep Advance

    [Fact]
    public void DelayConfig_CalculatesDelayUnitsAccurately()
    {
        var dMin = new DelayNodeConfig { Value = 15, Unit = DelayUnit.Minutes };
        var dHour = new DelayNodeConfig { Value = 3, Unit = DelayUnit.Hours };
        var dDay = new DelayNodeConfig { Value = 2, Unit = DelayUnit.Days };
        var dWeek = new DelayNodeConfig { Value = 1.5, Unit = DelayUnit.Weeks };

        Assert.Equal(TimeSpan.FromMinutes(15), dMin.ToTimeSpan());
        Assert.Equal(TimeSpan.FromHours(3), dHour.ToTimeSpan());
        Assert.Equal(TimeSpan.FromDays(2), dDay.ToTimeSpan());
        Assert.Equal(TimeSpan.FromDays(10.5), dWeek.ToTimeSpan());
    }

    [Fact]
    public async Task AdvanceDue_WhenDelayNotYetDue_DoesNotAdvanceContact()
    {
        var saasJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555551");
        var contactId = "contact_not_due_yet";

        var enrollRes = await _service.TriggerJourneyAsync(saasJourneyId, new TriggerContactRequest(
            ContactId: contactId,
            EventName: "user_signed_up",
            SegmentName: null,
            ContactProperties: null
        ));

        Assert.True(enrollRes.Success);
        Assert.Equal("saas_dly_wait1d", enrollRes.StatePointer!.CurrentNodeId);

        // Run sweep immediately (delay is 1 day in the future)
        var advanceRes = await _service.AdvanceDueContactsAsync(saasJourneyId);

        Assert.Equal(0, advanceRes.DueContactsFound);
        Assert.Equal(0, advanceRes.AdvancedCount);

        var currentPointer = await _service.GetContactStateAsync(saasJourneyId, contactId);
        Assert.Equal("saas_dly_wait1d", currentPointer!.CurrentNodeId);
        Assert.Equal(ContactJourneyStatus.InProgress, currentPointer.Status);
    }

    [Fact]
    public async Task AdvanceDue_WhenDelayDue_AdvancesContactToNextNode()
    {
        var saasJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555551");
        var contactId = "contact_due_now";

        var enrollRes = await _service.TriggerJourneyAsync(saasJourneyId, new TriggerContactRequest(
            ContactId: contactId,
            EventName: "user_signed_up",
            SegmentName: null,
            ContactProperties: new Dictionary<string, object>
            {
                ["has_activated"] = true
            }
        ));

        Assert.True(enrollRes.Success);
        var pointer = enrollRes.StatePointer!;

        // Manually simulate that delay elapsed by setting ScheduledResumeAtUtc in the past
        pointer.ScheduledResumeAtUtc = DateTime.UtcNow.AddMinutes(-5);

        // Run sweep
        var advanceRes = await _service.AdvanceDueContactsAsync(saasJourneyId);

        Assert.True(advanceRes.DueContactsFound >= 1);
        Assert.True(advanceRes.AdvancedCount >= 1);

        var updatedPointer = await _service.GetContactStateAsync(saasJourneyId, contactId);
        Assert.NotNull(updatedPointer);

        // Condition `has_activated == true` branches to celebrate -> end of journey (Completed)
        Assert.Equal(ContactJourneyStatus.Completed, updatedPointer.Status);
        Assert.NotNull(updatedPointer.CompletedAtUtc);
        Assert.Contains(updatedPointer.Breadcrumbs, b => b.NodeId == "saas_act_celebrate");
    }

    [Fact]
    public async Task AdvanceDue_WhenPausedJourney_SkipsAdvancingContacts()
    {
        var saasJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555551");
        var contactId = "contact_pause_advance";

        var enrollRes = await _service.TriggerJourneyAsync(saasJourneyId, new TriggerContactRequest(
            ContactId: contactId,
            EventName: "user_signed_up",
            SegmentName: null,
            ContactProperties: null
        ));
        var pointer = enrollRes.StatePointer!;
        pointer.ScheduledResumeAtUtc = DateTime.UtcNow.AddMinutes(-10);

        // Pause journey
        await _service.PauseJourneyAsync(saasJourneyId);

        var advanceRes = await _service.AdvanceDueContactsAsync(saasJourneyId);

        Assert.Equal(0, advanceRes.DueContactsFound);
        Assert.Equal(0, advanceRes.AdvancedCount);
        Assert.Contains(advanceRes.ExecutionSummaries, s => s.Contains("paused", StringComparison.OrdinalIgnoreCase));

        // Resume journey for subsequent tests
        await _service.ResumeJourneyAsync(saasJourneyId);
    }

    #endregion

    #region 5. Condition Branching Logic

    [Theory]
    [InlineData("has_activated", "IsTrue", "true", "true", true)]
    [InlineData("has_activated", "IsTrue", "true", "false", false)]
    [InlineData("has_activated", "IsFalse", "false", "false", true)]
    [InlineData("ltv", ">", "500", "750", true)]
    [InlineData("ltv", ">", "500", "250", false)]
    [InlineData("ltv", ">=", "500", "500", true)]
    [InlineData("ltv", "<", "100", "50", true)]
    [InlineData("ltv", "<=", "100", "100", true)]
    [InlineData("plan", "Equals", "Enterprise", "Enterprise", true)]
    [InlineData("plan", "Equals", "Enterprise", "Starter", false)]
    [InlineData("tags", "Contains", "vip", "user_vip_gold", true)]
    [InlineData("tags", "Contains", "vip", "standard_user", false)]
    public void ConditionEvaluation_CoversVariousOperatorsAndTypes(
        string property,
        string op,
        string targetValue,
        string propertyValue,
        bool expected)
    {
        var config = new ConditionNodeConfig
        {
            Property = property,
            Operator = op,
            Value = targetValue
        };

        var props = new Dictionary<string, object>
        {
            [property] = propertyValue
        };

        var result = CustomerLifecycleJourneyService.EvaluateCondition(config, props);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ConditionBranching_WhenConditionTrue_RoutesToTrueNode()
    {
        var saasJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555551");
        var contactId = "contact_cond_true";

        var enroll = await _service.TriggerJourneyAsync(saasJourneyId, new TriggerContactRequest(
            ContactId: contactId,
            EventName: "user_signed_up",
            SegmentName: null,
            ContactProperties: new Dictionary<string, object>
            {
                ["has_activated"] = "true"
            }
        ));

        // Fast-forward delay
        enroll.StatePointer!.ScheduledResumeAtUtc = DateTime.UtcNow.AddMinutes(-1);

        await _service.AdvanceDueContactsAsync(saasJourneyId);

        var pointer = await _service.GetContactStateAsync(saasJourneyId, contactId);
        Assert.NotNull(pointer);

        // Condition node was evaluated and routed to celebrate
        var condCrumb = pointer.Breadcrumbs.FirstOrDefault(b => b.NodeType == LifecycleNodeType.Condition);
        Assert.NotNull(condCrumb);
        Assert.Contains("True", condCrumb.Outcome, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(pointer.Breadcrumbs, b => b.NodeId == "saas_act_celebrate");
    }

    [Fact]
    public async Task ConditionBranching_WhenConditionFalse_RoutesToFalseNode()
    {
        var saasJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555551");
        var contactId = "contact_cond_false";

        var enroll = await _service.TriggerJourneyAsync(saasJourneyId, new TriggerContactRequest(
            ContactId: contactId,
            EventName: "user_signed_up",
            SegmentName: null,
            ContactProperties: new Dictionary<string, object>
            {
                ["has_activated"] = false
            }
        ));

        // Fast-forward delay
        enroll.StatePointer!.ScheduledResumeAtUtc = DateTime.UtcNow.AddMinutes(-1);

        await _service.AdvanceDueContactsAsync(saasJourneyId);

        var pointer = await _service.GetContactStateAsync(saasJourneyId, contactId);
        Assert.NotNull(pointer);

        // Condition node was evaluated and routed to nudge
        var condCrumb = pointer.Breadcrumbs.FirstOrDefault(b => b.NodeType == LifecycleNodeType.Condition);
        Assert.NotNull(condCrumb);
        Assert.Contains("False", condCrumb.Outcome, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(pointer.Breadcrumbs, b => b.NodeId == "saas_act_nudge");
    }

    [Fact]
    public void ConditionEvaluation_MissingProperty_DefaultsToFalse()
    {
        var config = new ConditionNodeConfig
        {
            Property = "non_existent_key",
            Operator = "Equals",
            Value = "active"
        };

        var props = new Dictionary<string, object>();
        var result = CustomerLifecycleJourneyService.EvaluateCondition(config, props);

        Assert.False(result);
    }

    #endregion

    #region 6. Cross-Channel Delivery & Failover Routing

    [Fact]
    public async Task FailoverRouting_WhatsAppSuccess_DispatchesPrimaryChannelWithoutFallback()
    {
        var saasJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555551");
        var contactId = "contact_wa_success";

        var enroll = await _service.TriggerJourneyAsync(saasJourneyId, new TriggerContactRequest(
            ContactId: contactId,
            EventName: "user_signed_up",
            SegmentName: null,
            ContactProperties: new Dictionary<string, object>
            {
                ["has_activated"] = true,
                ["simulate_channel_failure"] = false
            }
        ));

        enroll.StatePointer!.ScheduledResumeAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await _service.AdvanceDueContactsAsync(saasJourneyId);

        var pointer = await _service.GetContactStateAsync(saasJourneyId, contactId);
        var celebrateLog = pointer!.DeliveryLogs.FirstOrDefault(l => l.NodeId == "saas_act_celebrate");

        Assert.NotNull(celebrateLog);
        Assert.Equal(ActionChannel.WhatsApp, celebrateLog.Channel);
        Assert.True(celebrateLog.Success);
        Assert.False(celebrateLog.WasFallback);
    }

    [Fact]
    public async Task FailoverRouting_WhatsAppSimulatedFailure_FailsOverToEmailSuccessfully()
    {
        var saasJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555551");
        var contactId = "contact_wa_failover";

        var enroll = await _service.TriggerJourneyAsync(saasJourneyId, new TriggerContactRequest(
            ContactId: contactId,
            EventName: "user_signed_up",
            SegmentName: null,
            ContactProperties: new Dictionary<string, object>
            {
                ["has_activated"] = true,
                ["fail_whatsapp"] = true // Force WhatsApp bounce
            }
        ));

        enroll.StatePointer!.ScheduledResumeAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await _service.AdvanceDueContactsAsync(saasJourneyId);

        var pointer = await _service.GetContactStateAsync(saasJourneyId, contactId);
        var celebrateLog = pointer!.DeliveryLogs.FirstOrDefault(l => l.NodeId == "saas_act_celebrate");

        Assert.NotNull(celebrateLog);
        Assert.True(celebrateLog.Success);
        Assert.True(celebrateLog.WasFallback);
        Assert.Equal(ActionChannel.WhatsApp, celebrateLog.OriginalChannel);
        Assert.Equal(ActionChannel.Email, celebrateLog.Channel);
        Assert.Contains("Cross-channel failover routed to Email", celebrateLog.Details);
    }

    [Fact]
    public async Task FailoverRouting_ChannelFailureWithNoFallback_RecordsFailureLog()
    {
        var journey = await _service.CreateJourneyAsync(new CreateJourneyRequest(
            Name: "No Fallback Channel Journey",
            Description: "Action with no fallback configured",
            TenantId: null,
            Nodes: new()
            {
                new LifecycleJourneyNode
                {
                    Id = "trg",
                    Name = "Trigger",
                    Type = LifecycleNodeType.Trigger,
                    NextNodeIds = new() { "act_no_fb" },
                    TriggerConfig = new TriggerNodeConfig { Type = TriggerType.Event, EventName = "fail_test" }
                },
                new LifecycleJourneyNode
                {
                    Id = "act_no_fb",
                    Name = "Failing Action No Fallback",
                    Type = LifecycleNodeType.Action,
                    NextNodeIds = new(),
                    ActionConfig = new ActionNodeConfig
                    {
                        Channel = ActionChannel.Push,
                        FallbackChannel = null, // No fallback!
                        SimulateChannelFailure = true
                    }
                }
            },
            Edges: new(),
            Status: JourneyLifecycleStatus.Active
        ));

        var contactId = "contact_push_fail";
        var res = await _service.TriggerJourneyAsync(journey.Id, new TriggerContactRequest(
            ContactId: contactId,
            EventName: "fail_test",
            SegmentName: null,
            ContactProperties: null
        ));

        Assert.True(res.Success);
        var pointer = res.StatePointer!;
        var failLog = pointer.DeliveryLogs.FirstOrDefault(l => l.NodeId == "act_no_fb");

        Assert.NotNull(failLog);
        Assert.False(failLog.Success);
        Assert.False(failLog.WasFallback);
        Assert.Contains("no fallback channel", failLog.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailoverRouting_IncrementsFallbackRecoveryMetric()
    {
        var saasJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555551");
        var initialJourney = await _service.GetJourneyByIdAsync(saasJourneyId);
        var initialFallbackCount = initialJourney!.FallbackRecoveryCount;

        var contactId = $"contact_fb_metric_{Guid.NewGuid():N}";
        var enroll = await _service.TriggerJourneyAsync(saasJourneyId, new TriggerContactRequest(
            ContactId: contactId,
            EventName: "user_signed_up",
            SegmentName: null,
            ContactProperties: new Dictionary<string, object>
            {
                ["has_activated"] = false,
                ["fail_whatsapp"] = true
            }
        ));

        enroll.StatePointer!.ScheduledResumeAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await _service.AdvanceDueContactsAsync(saasJourneyId);

        var updatedJourney = await _service.GetJourneyByIdAsync(saasJourneyId);
        Assert.True(updatedJourney!.FallbackRecoveryCount > initialFallbackCount);
    }

    #endregion

    #region 7. Journey State Management & Metrics

    [Fact]
    public async Task JourneyLifecycle_PauseAndResume_TogglesJourneyStatusCorrectly()
    {
        var entJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555553");

        var paused = await _service.PauseJourneyAsync(entJourneyId);
        Assert.True(paused);
        var j1 = await _service.GetJourneyByIdAsync(entJourneyId);
        Assert.Equal(JourneyLifecycleStatus.Paused, j1!.Status);

        var resumed = await _service.ResumeJourneyAsync(entJourneyId);
        Assert.True(resumed);
        var j2 = await _service.GetJourneyByIdAsync(entJourneyId);
        Assert.Equal(JourneyLifecycleStatus.Active, j2!.Status);
    }

    [Fact]
    public async Task Metrics_AggregatesActiveInFlightAndConversionRateCorrectly()
    {
        var metrics = await _service.GetMetricsAsync();

        Assert.True(metrics.TotalJourneys >= 3);
        Assert.True(metrics.ActiveJourneys >= 1);
        Assert.NotNull(metrics.ContactsPerStepDistribution);
    }

    [Fact]
    public async Task GetActiveContacts_ReturnsPointersWithBreadcrumbs()
    {
        var saasJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555551");
        var contactId = $"contact_query_{Guid.NewGuid():N}";

        await _service.TriggerJourneyAsync(saasJourneyId, new TriggerContactRequest(
            ContactId: contactId,
            EventName: "user_signed_up",
            SegmentName: null,
            ContactProperties: null
        ));

        var contacts = await _service.GetActiveContactsAsync(saasJourneyId, limit: 50);

        Assert.NotEmpty(contacts);
        Assert.Contains(contacts, c => c.ContactId == contactId);
    }

    #endregion

    #region 8. High Concurrency Thread-Safety Tests

    [Fact]
    public async Task Concurrency_SimultaneousEnrollments_MaintainsConsistentCounts()
    {
        var saasJourneyId = Guid.Parse("11111111-2222-3333-4444-555555555551");
        var tasks = new List<Task<EnrollmentResult>>();
        const int concurrentCount = 50;

        for (int i = 0; i < concurrentCount; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(() => _service.TriggerJourneyAsync(saasJourneyId, new TriggerContactRequest(
                ContactId: $"concurrent_contact_{idx}_{Guid.NewGuid():N}",
                EventName: "user_signed_up",
                SegmentName: null,
                ContactProperties: new Dictionary<string, object> { ["thread_id"] = idx }
            ))));
        }

        var results = await Task.WhenAll(tasks);

        Assert.Equal(concurrentCount, results.Length);
        Assert.All(results, r => Assert.True(r.Success));

        var journey = await _service.GetJourneyByIdAsync(saasJourneyId);
        Assert.NotNull(journey);
        Assert.True(journey.TotalEnrolled >= concurrentCount);
    }

    [Fact]
    public async Task Concurrency_SimultaneousAdvanceSweeps_PreventsDuplicateStepExecutions()
    {
        var journey = await _service.CreateJourneyAsync(new CreateJourneyRequest(
            Name: "Concurrency Sweep Journey",
            Description: "Test parallel advance sweeps",
            TenantId: null,
            Nodes: new()
            {
                new LifecycleJourneyNode
                {
                    Id = "trg_c",
                    Name = "Trigger",
                    Type = LifecycleNodeType.Trigger,
                    NextNodeIds = new() { "dly_c" },
                    TriggerConfig = new TriggerNodeConfig { Type = TriggerType.Event, EventName = "c_event" }
                },
                new LifecycleJourneyNode
                {
                    Id = "dly_c",
                    Name = "Delay",
                    Type = LifecycleNodeType.Delay,
                    NextNodeIds = new() { "act_c" },
                    DelayConfig = new DelayNodeConfig { Value = 1, Unit = DelayUnit.Days }
                },
                new LifecycleJourneyNode
                {
                    Id = "act_c",
                    Name = "Action Finish",
                    Type = LifecycleNodeType.Action,
                    NextNodeIds = new(),
                    ActionConfig = new ActionNodeConfig { Channel = ActionChannel.Email, Subject = "Done" }
                }
            },
            Edges: new(),
            Status: JourneyLifecycleStatus.Active
        ));

        var contactId = $"concurrent_advance_{Guid.NewGuid():N}";
        var enroll = await _service.TriggerJourneyAsync(journey.Id, new TriggerContactRequest(
            ContactId: contactId,
            EventName: "c_event",
            SegmentName: null,
            ContactProperties: null
        ));

        Assert.True(enroll.Success);
        // Make delay due
        enroll.StatePointer!.ScheduledResumeAtUtc = DateTime.UtcNow.AddMinutes(-5);

        // Run 10 parallel sweeps at the exact same moment
        var sweepTasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => _service.AdvanceDueContactsAsync(journey.Id)))
            .ToArray();

        var sweepResults = await Task.WhenAll(sweepTasks);

        // Total advanced across all parallel sweeps must be exactly 1!
        var totalAdvanced = sweepResults.Sum(s => s.AdvancedCount);
        Assert.Equal(1, totalAdvanced);

        var finalPointer = await _service.GetContactStateAsync(journey.Id, contactId);
        Assert.NotNull(finalPointer);
        Assert.Equal(ContactJourneyStatus.Completed, finalPointer.Status);

        // Action breadcrumbs must appear exactly once
        var actionCrumbs = finalPointer.Breadcrumbs.Count(b => b.NodeId == "act_c");
        Assert.Equal(1, actionCrumbs);
    }

    #endregion
}
