using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Outbound;
using SGrowing.Api.Modules.Voice;
using SGrowing.Api.Services;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Xunit;

namespace SGrowing.Tests.Outbound;

public class MultiTouchSdrTests
{
    private OutboundDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid());
        return new OutboundDbContext(options, tenantContext);
    }

    [Fact]
    public async Task CadenceTouchpoint_Creation_WithMultipleChannels_ShouldPersistAndInstantiateCorrectly()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        // 1. Create touchpoints across channels: Email, LinkedInConnect, LinkedInMessage, WhatsApp, VoiceCall
        var emailTouchpoint = CadenceTouchpoint.Create(
            tenantId,
            campaignId,
            1,
            TouchpointChannel.Email,
            0,
            "Transforming outreach for {{first_name}}",
            "Hi {{first_name}}, check out our SDR automation.");

        var linkedInConnectTouchpoint = CadenceTouchpoint.Create(
            tenantId,
            campaignId,
            2,
            TouchpointChannel.LinkedInConnect,
            2,
            null,
            "Hi {{first_name}}, let's connect on LinkedIn!");

        var whatsAppTouchpoint = CadenceTouchpoint.Create(
            tenantId,
            campaignId,
            3,
            TouchpointChannel.WhatsApp,
            4,
            null,
            "Hey {{first_name}}, dropping a quick WhatsApp note.");

        var voiceCallTouchpoint = CadenceTouchpoint.Create(
            tenantId,
            campaignId,
            4,
            TouchpointChannel.VoiceCall,
            6,
            null,
            "AI SDR follow-up call regarding growth strategy.");

        db.CadenceTouchpoints.AddRange(
            emailTouchpoint,
            linkedInConnectTouchpoint,
            whatsAppTouchpoint,
            voiceCallTouchpoint);

        // 2. Create executions
        var scheduledTime = DateTime.UtcNow.AddMinutes(-10);
        var exec1 = TouchpointExecution.Create(tenantId, emailTouchpoint.Id, contactId, TouchpointChannel.Email, scheduledTime);
        var exec2 = TouchpointExecution.Create(tenantId, linkedInConnectTouchpoint.Id, contactId, TouchpointChannel.LinkedInConnect, scheduledTime);
        var exec3 = TouchpointExecution.Create(tenantId, whatsAppTouchpoint.Id, contactId, TouchpointChannel.WhatsApp, scheduledTime);
        var exec4 = TouchpointExecution.Create(tenantId, voiceCallTouchpoint.Id, contactId, TouchpointChannel.VoiceCall, scheduledTime);

        db.TouchpointExecutions.AddRange(exec1, exec2, exec3, exec4);
        await db.SaveChangesAsync();

        // 3. Verify touchpoint persistence
        var savedTouchpoints = await db.CadenceTouchpoints
            .Where(t => t.CampaignId == campaignId)
            .OrderBy(t => t.StepNumber)
            .ToListAsync();

        savedTouchpoints.Should().HaveCount(4);
        savedTouchpoints[0].Channel.Should().Be(TouchpointChannel.Email);
        savedTouchpoints[0].SubjectTemplate.Should().Be("Transforming outreach for {{first_name}}");
        savedTouchpoints[1].Channel.Should().Be(TouchpointChannel.LinkedInConnect);
        savedTouchpoints[2].Channel.Should().Be(TouchpointChannel.WhatsApp);
        savedTouchpoints[3].Channel.Should().Be(TouchpointChannel.VoiceCall);

        // 4. Verify execution initial state
        var savedExecutions = await db.TouchpointExecutions
            .Where(e => e.TenantId == tenantId)
            .ToListAsync();

        savedExecutions.Should().HaveCount(4);
        savedExecutions.Should().OnlyContain(e => e.Status == "Pending");
        savedExecutions.Should().OnlyContain(e => e.ExecutedAtUtc == null);
    }

    [Fact]
    public async Task Orchestrator_ProcessDueMultiTouchSteps_ShouldDispatchAcrossAllChannels_AndMarkSent()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var outbox = new OutboxService(db);
        var mailboxService = new MailboxService(db);
        var linkedInAdapter = new LinkedInOutreachAdapter(null, outbox);
        var voiceService = new VoiceTelephonyService(db);

        // 1. Setup Mailbox and SenderAccount
        var sender = SenderAccount.Create(tenantId, "alex.sdr@growth.ai", "Alex SDR", MailboxProvider.GoogleWorkspace, 50);
        db.SenderAccounts.Add(sender);

        // 2. Setup Campaign with sender
        var campaign = OutboundCampaign.Create(tenantId, "Omnichannel Enterprise SDR", new List<Guid> { sender.Id });
        db.OutboundCampaigns.Add(campaign);

        // 3. Setup Contact with email, phone, and LinkedIn profile
        var customFields = new Dictionary<string, object>
        {
            { "LinkedInUrl", "https://linkedin.com/in/tony-stark" }
        };
        var contact = Contact.Create(tenantId, "Tony", "Stark", "tony@starkindustries.com", "+12025550143", customFields: customFields);
        db.Contacts.Add(contact);

        // 4. Setup 4 Touchpoints: Email, LinkedInConnect, WhatsApp, VoiceCall
        var t1 = CadenceTouchpoint.Create(tenantId, campaign.Id, 1, TouchpointChannel.Email, 0, "Hello {{first_name}}", "Email body for {{first_name}}");
        var t2 = CadenceTouchpoint.Create(tenantId, campaign.Id, 2, TouchpointChannel.LinkedInConnect, 1, null, "Connect with me, {{first_name}}");
        var t3 = CadenceTouchpoint.Create(tenantId, campaign.Id, 3, TouchpointChannel.WhatsApp, 2, null, "WhatsApp ping for {{first_name}}");
        var t4 = CadenceTouchpoint.Create(tenantId, campaign.Id, 4, TouchpointChannel.VoiceCall, 3, null, "Calling {{first_name}} regarding partnership");
        db.CadenceTouchpoints.AddRange(t1, t2, t3, t4);

        // 5. Setup due executions (due 10 minutes ago)
        var dueTime = DateTime.UtcNow.AddMinutes(-10);
        var exec1 = TouchpointExecution.Create(tenantId, t1.Id, contact.Id, TouchpointChannel.Email, dueTime);
        var exec2 = TouchpointExecution.Create(tenantId, t2.Id, contact.Id, TouchpointChannel.LinkedInConnect, dueTime);
        var exec3 = TouchpointExecution.Create(tenantId, t3.Id, contact.Id, TouchpointChannel.WhatsApp, dueTime);
        var exec4 = TouchpointExecution.Create(tenantId, t4.Id, contact.Id, TouchpointChannel.VoiceCall, dueTime);

        // 6. Setup 1 future execution (due in 2 days, should NOT be processed yet)
        var futureExec = TouchpointExecution.Create(tenantId, t1.Id, contact.Id, TouchpointChannel.Email, DateTime.UtcNow.AddDays(2));

        db.TouchpointExecutions.AddRange(exec1, exec2, exec3, exec4, futureExec);
        await db.SaveChangesAsync();

        // 7. Execute Orchestrator
        var orchestrator = new MultiTouchCadenceOrchestrator(
            db,
            mailboxService,
            linkedInAdapter,
            voiceService,
            outbox);

        var processedCount = await orchestrator.ProcessDueMultiTouchStepsAsync(tenantId);

        // 8. Assertions
        processedCount.Should().Be(4);

        // Verify the 4 due executions are marked Sent with ExecutedAtUtc populated
        var updatedDueExecs = await db.TouchpointExecutions
            .Where(e => e.Id == exec1.Id || e.Id == exec2.Id || e.Id == exec3.Id || e.Id == exec4.Id)
            .ToListAsync();

        updatedDueExecs.Should().HaveCount(4);
        updatedDueExecs.Should().OnlyContain(e => e.Status == "Sent");
        updatedDueExecs.Should().OnlyContain(e => e.ExecutedAtUtc != null);

        // Verify future execution remains Pending
        var updatedFutureExec = await db.TouchpointExecutions.FindAsync(futureExec.Id);
        updatedFutureExec!.Status.Should().Be("Pending");
        updatedFutureExec.ExecutedAtUtc.Should().BeNull();

        // Verify Mailbox sender recorded send
        sender.SentTodayCount.Should().Be(1);

        // Verify LinkedIn adapter captured the connection request
        linkedInAdapter.SentConnectionRequests.Should().HaveCount(1);
        linkedInAdapter.SentConnectionRequests[0].ProfileUrl.Should().Be("https://linkedin.com/in/tony-stark");
        linkedInAdapter.SentConnectionRequests[0].NoteText.Should().Be("Connect with me, Tony");

        // Verify Voice telephony created an outbound session
        var voiceSessions = await db.VoiceCallSessions
            .Where(v => v.TenantId == tenantId)
            .ToListAsync();
        voiceSessions.Should().HaveCount(1);
        voiceSessions[0].ContactId.Should().Be(contact.Id);
        voiceSessions[0].ToNumber.Should().Be("+12025550143");
        voiceSessions[0].Direction.Should().Be(CallDirection.Outbound);

        // Verify Outbox messages
        var outboxEvents = await db.OutboxMessages
            .Where(o => o.TenantId == tenantId)
            .ToListAsync();

        // Should contain WhatsAppOutboundDispatched, LinkedInConnectionRequestSent, and TouchpointExecutionSent events
        outboxEvents.Should().Contain(o => o.EventType == "WhatsAppOutboundDispatched");
        outboxEvents.Should().Contain(o => o.EventType == "TouchpointExecutionSent");
        outboxEvents.Count(o => o.EventType == "TouchpointExecutionSent").Should().Be(4);
    }

    [Fact]
    public async Task Orchestrator_ProcessDueMultiTouchSteps_ShouldHandleLinkedInInMailMessage()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var outbox = new OutboxService(db);
        var mailboxService = new MailboxService(db);
        var linkedInAdapter = new LinkedInOutreachAdapter(null, outbox);
        var voiceService = new VoiceTelephonyService(db);

        var contact = Contact.Create(tenantId, "Pepper", "Potts", "pepper@stark.com");
        db.Contacts.Add(contact);

        var touchpoint = CadenceTouchpoint.Create(
            tenantId,
            Guid.NewGuid(),
            1,
            TouchpointChannel.LinkedInMessage,
            0,
            "Quick InMail for {{first_name}}",
            "Hi {{first_name}}, would love to discuss strategic AI initiatives.");
        db.CadenceTouchpoints.Add(touchpoint);

        var exec = TouchpointExecution.Create(
            tenantId,
            touchpoint.Id,
            contact.Id,
            TouchpointChannel.LinkedInMessage,
            DateTime.UtcNow.AddMinutes(-5));
        db.TouchpointExecutions.Add(exec);
        await db.SaveChangesAsync();

        var orchestrator = new MultiTouchCadenceOrchestrator(
            db,
            mailboxService,
            linkedInAdapter,
            voiceService,
            outbox);

        var processed = await orchestrator.ProcessDueMultiTouchStepsAsync(tenantId);

        processed.Should().Be(1);
        exec.Status.Should().Be("Sent");
        exec.ExecutedAtUtc.Should().NotBeNull();

        linkedInAdapter.SentInMails.Should().HaveCount(1);
        linkedInAdapter.SentInMails[0].ContactId.Should().Be(contact.Id);
        linkedInAdapter.SentInMails[0].Subject.Should().Be("Quick InMail for Pepper");
        linkedInAdapter.SentInMails[0].MessageText.Should().Be("Hi Pepper, would love to discuss strategic AI initiatives.");
    }

    [Fact]
    public async Task Orchestrator_ProcessDueMultiTouchSteps_ShouldRespectTenantIsolation()
    {
        var db = CreateDbContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var outbox = new OutboxService(db);
        var mailboxService = new MailboxService(db);
        var linkedInAdapter = new LinkedInOutreachAdapter(null, outbox);
        var voiceService = new VoiceTelephonyService(db);

        var contactA = Contact.Create(tenantA, "Agent", "A", "a@tenant.com");
        var contactB = Contact.Create(tenantB, "Agent", "B", "b@tenant.com");
        db.Contacts.AddRange(contactA, contactB);

        var tA = CadenceTouchpoint.Create(tenantA, Guid.NewGuid(), 1, TouchpointChannel.WhatsApp, 0, null, "Msg A");
        var tB = CadenceTouchpoint.Create(tenantB, Guid.NewGuid(), 1, TouchpointChannel.WhatsApp, 0, null, "Msg B");
        db.CadenceTouchpoints.AddRange(tA, tB);

        var execA = TouchpointExecution.Create(tenantA, tA.Id, contactA.Id, TouchpointChannel.WhatsApp, DateTime.UtcNow.AddMinutes(-1));
        var execB = TouchpointExecution.Create(tenantB, tB.Id, contactB.Id, TouchpointChannel.WhatsApp, DateTime.UtcNow.AddMinutes(-1));
        db.TouchpointExecutions.AddRange(execA, execB);
        await db.SaveChangesAsync();

        var orchestrator = new MultiTouchCadenceOrchestrator(
            db,
            mailboxService,
            linkedInAdapter,
            voiceService,
            outbox);

        // Only process tenant A
        var countA = await orchestrator.ProcessDueMultiTouchStepsAsync(tenantA);

        countA.Should().Be(1);
        execA.Status.Should().Be("Sent");

        // Tenant B must remain Pending
        var refreshedExecB = await db.TouchpointExecutions.FindAsync(execB.Id);
        refreshedExecB!.Status.Should().Be("Pending");
        refreshedExecB.ExecutedAtUtc.Should().BeNull();
    }
}
