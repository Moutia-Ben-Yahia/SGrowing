using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SGrowing.Api.Common;
using SGrowing.Api.Modules.Marketing.Journeys.Runtime;
using Xunit;

namespace SGrowing.Tests.Unit;

public class CustomerJourneyRuntimeTests
{
    [Fact]
    public async Task TriggerJourney_EnrollsContactAndSchedules15mDelay()
    {
        var service = new CustomerJourneyRuntimeService();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var email = "customer1@growthexample.com";

        var req = new TriggerJourneyEventRequest(
            TenantId: tenantId,
            EventName: "cart.abandoned",
            ContactId: contactId,
            ContactEmail: email,
            PayloadData: new Dictionary<string, string>
            {
                ["cart_amount"] = "450 SAR",
                ["currency"] = "SAR",
                ["cart_items_count"] = "3"
            }
        );

        var progress = await service.TriggerJourney(req);

        Assert.NotNull(progress);
        Assert.Equal(tenantId, progress.TenantId);
        Assert.Equal(CustomerJourneyRuntimeService.RamadanCartRecoveryJourneyId, progress.JourneyId);
        Assert.Equal(contactId, progress.ContactId);
        Assert.Equal(email, progress.ContactEmail);
        Assert.Equal(CustomerJourneyRuntimeService.NodeDelayId, progress.CurrentNodeId);
        Assert.Equal(JourneyExecutionStatus.PausedWaitingDelay, progress.Status);
        Assert.NotNull(progress.ResumeAtUtc);
        Assert.True(progress.ResumeAtUtc.Value > DateTime.UtcNow.AddMinutes(14));

        // Execution history must have TriggerNode and DelayNode entries
        Assert.Equal(2, progress.ExecutionHistory.Count);
        Assert.Equal(JourneyNodeType.TriggerNode, progress.ExecutionHistory[0].NodeType);
        Assert.Equal("TriggerEnrolled", progress.ExecutionHistory[0].Outcome);
        Assert.Equal(JourneyNodeType.DelayNode, progress.ExecutionHistory[1].NodeType);
        Assert.Equal("PausedWaitingDelay", progress.ExecutionHistory[1].Outcome);
    }

    [Fact]
    public async Task AdvanceProgress_WhenHighCartValue_RoutesToWhatsAppPromoAndCompletes()
    {
        var service = new CustomerJourneyRuntimeService();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var email = "vip.buyer@growthexample.com";

        // High cart value: 450 SAR >= 300 SAR
        var triggerReq = new TriggerJourneyEventRequest(
            TenantId: tenantId,
            EventName: "cart.abandoned",
            ContactId: contactId,
            ContactEmail: email,
            PayloadData: new Dictionary<string, string>
            {
                ["cart_amount"] = "450 SAR"
            }
        );

        var initialProgress = await service.TriggerJourney(triggerReq);
        Assert.Equal(CustomerJourneyRuntimeService.NodeDelayId, initialProgress.CurrentNodeId);

        // Advance past delay
        var advanceReq = new AdvanceJourneyRequest(
            TenantId: tenantId,
            ProgressId: initialProgress.ProgressId
        );

        var advanced = await service.AdvanceProgress(advanceReq);

        Assert.NotNull(advanced);
        Assert.Equal(CustomerJourneyRuntimeService.NodeConversionCheckpointId, advanced.CurrentNodeId);
        Assert.Equal(JourneyExecutionStatus.Completed, advanced.Status);
        Assert.NotNull(advanced.CompletedAtUtc);
        Assert.Null(advanced.ResumeAtUtc);

        // Verify history includes: Trigger, Delay Scheduled, Delay Completed, Condition True, WhatsApp Promo Dispatched, Conversion Checkpoint
        Assert.Equal(6, advanced.ExecutionHistory.Count);

        var delayCompletedLog = advanced.ExecutionHistory.FirstOrDefault(l => l.Outcome == "DelayCompleted");
        Assert.NotNull(delayCompletedLog);

        var conditionLog = advanced.ExecutionHistory.FirstOrDefault(l => l.NodeType == JourneyNodeType.ConditionNode);
        Assert.NotNull(conditionLog);
        Assert.Contains("ConditionTrue", conditionLog.Outcome);
        Assert.Contains("VIP WhatsApp", conditionLog.Details);

        var actionLog = advanced.ExecutionHistory.FirstOrDefault(l => l.Outcome == "WhatsAppPromoDispatched");
        Assert.NotNull(actionLog);
        Assert.Equal(JourneyNodeType.ActionNode, actionLog.NodeType);

        var checkpointLog = advanced.ExecutionHistory.FirstOrDefault(l => l.Outcome == "ConversionCheckpointReached");
        Assert.NotNull(checkpointLog);
    }

    [Fact]
    public async Task AdvanceProgress_WhenLowCartValue_RoutesToEmailVoucherAndCompletes()
    {
        var service = new CustomerJourneyRuntimeService();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var email = "budget.buyer@growthexample.com";

        // Low cart value: 150 SAR < 300 SAR
        var triggerReq = new TriggerJourneyEventRequest(
            TenantId: tenantId,
            EventName: "cart.abandoned",
            ContactId: contactId,
            ContactEmail: email,
            PayloadData: new Dictionary<string, string>
            {
                ["cart_amount"] = "150 SAR"
            }
        );

        var initialProgress = await service.TriggerJourney(triggerReq);

        var advanced = await service.AdvanceProgress(new AdvanceJourneyRequest(
            TenantId: tenantId,
            ProgressId: initialProgress.ProgressId
        ));

        Assert.NotNull(advanced);
        Assert.Equal(CustomerJourneyRuntimeService.NodeConversionCheckpointId, advanced.CurrentNodeId);
        Assert.Equal(JourneyExecutionStatus.Completed, advanced.Status);

        // Verify conditional route chose Email Voucher instead of WhatsApp
        var conditionLog = advanced.ExecutionHistory.FirstOrDefault(l => l.NodeType == JourneyNodeType.ConditionNode);
        Assert.NotNull(conditionLog);
        Assert.Contains("ConditionFalse", conditionLog.Outcome);
        Assert.Contains("Standard Email Recovery Voucher", conditionLog.Details);

        var emailActionLog = advanced.ExecutionHistory.FirstOrDefault(l => l.Outcome == "EmailVoucherDispatched");
        Assert.NotNull(emailActionLog);

        var whatsappLog = advanced.ExecutionHistory.FirstOrDefault(l => l.Outcome == "WhatsAppPromoDispatched");
        Assert.Null(whatsappLog);
    }

    [Fact]
    public async Task AdvanceProgress_WithOverrideNodeId_JumpsDirectlyToSpecifiedNode()
    {
        var service = new CustomerJourneyRuntimeService();
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var initial = await service.TriggerJourney(new TriggerJourneyEventRequest(
            TenantId: tenantId,
            EventName: "cart.abandoned",
            ContactId: contactId,
            ContactEmail: "override.test@growthexample.com",
            PayloadData: new Dictionary<string, string> { ["cart_amount"] = "100 SAR" }
        ));

        // Manually override straight to WhatsApp Node
        var overridden = await service.AdvanceProgress(new AdvanceJourneyRequest(
            TenantId: tenantId,
            ProgressId: initial.ProgressId,
            OverrideNodeId: CustomerJourneyRuntimeService.NodeActionWhatsAppId
        ));

        Assert.NotNull(overridden);
        Assert.Equal(CustomerJourneyRuntimeService.NodeActionWhatsAppId, overridden.CurrentNodeId);
        Assert.Contains(overridden.ExecutionHistory, l => l.Outcome == "WhatsAppPromoDispatched");
    }

    [Fact]
    public async Task TenantIsolation_EnforcesStrictMultiTenantDataBoundaries()
    {
        var service = new CustomerJourneyRuntimeService();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var progressA = await service.TriggerJourney(new TriggerJourneyEventRequest(
            TenantId: tenantA,
            EventName: "cart.abandoned",
            ContactId: Guid.NewGuid(),
            ContactEmail: "user.tenantA@company.sa",
            PayloadData: new Dictionary<string, string> { ["cart_amount"] = "500 SAR" }
        ));

        // Tenant B must not be able to read Tenant A's journey progress
        var crossTenantRead = await service.GetJourneyProgress(tenantB, progressA.ProgressId);
        Assert.Null(crossTenantRead);

        // Tenant B must not be able to advance Tenant A's journey progress
        var crossTenantAdvance = await service.AdvanceProgress(new AdvanceJourneyRequest(
            TenantId: tenantB,
            ProgressId: progressA.ProgressId
        ));
        Assert.Null(crossTenantAdvance);

        // Contacts list for Tenant B must not contain Tenant A's contact
        var tenantBContacts = await service.GetContactsInJourney(tenantB, CustomerJourneyRuntimeService.RamadanCartRecoveryJourneyId);
        Assert.DoesNotContain(tenantBContacts, c => c.ContactEmail == "user.tenantA@company.sa");

        // Tenant A sees their contact
        var tenantAContacts = await service.GetContactsInJourney(tenantA, CustomerJourneyRuntimeService.RamadanCartRecoveryJourneyId);
        Assert.Contains(tenantAContacts, c => c.ContactEmail == "user.tenantA@company.sa");
    }

    [Fact]
    public async Task GetActiveJourneys_CalculatesAggregateMetricsCorrectly()
    {
        var service = new CustomerJourneyRuntimeService();
        var tenantId = Guid.NewGuid();

        var summaries = await service.GetActiveJourneys(tenantId);
        Assert.NotEmpty(summaries);

        var journey = summaries[0];
        Assert.Equal(CustomerJourneyRuntimeService.RamadanCartRecoveryJourneyId, journey.JourneyId);
        Assert.Equal(5, journey.NodeCount);
        Assert.True(journey.EnrolledContacts >= 1420);
        Assert.True(journey.InDelayNodes >= 380);
        Assert.True(journey.ConvertedContacts >= 940);
        Assert.True(journey.ConversionRatePercentage > 60.0);

        // Triggering new journey increments enrolled & in-delay
        var prevEnrolled = journey.EnrolledContacts;
        var prevDelay = journey.InDelayNodes;

        await service.TriggerJourney(new TriggerJourneyEventRequest(
            TenantId: tenantId,
            EventName: "cart.abandoned",
            ContactId: Guid.NewGuid(),
            ContactEmail: "new.shopper@saudia.sa",
            PayloadData: new Dictionary<string, string> { ["cart_amount"] = "750 SAR" }
        ));

        var updatedSummaries = await service.GetActiveJourneys(tenantId);
        var updatedJourney = updatedSummaries[0];

        Assert.Equal(prevEnrolled + 1, updatedJourney.EnrolledContacts);
        Assert.Equal(prevDelay + 1, updatedJourney.InDelayNodes);
    }

    [Fact]
    public void CustomerJourneyRuntimeEndpoints_ImplementsIEndpointModule_ParameterlessConstructor()
    {
        var moduleType = typeof(CustomerJourneyRuntimeEndpoints);
        Assert.True(typeof(IEndpointModule).IsAssignableFrom(moduleType));

        var defaultCtor = moduleType.GetConstructor(Type.EmptyTypes);
        Assert.NotNull(defaultCtor);

        var instance = Activator.CreateInstance(moduleType) as IEndpointModule;
        Assert.NotNull(instance);
    }
}
