using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.Automation;
using SGrowing.Api.Modules.Channels;
using SGrowing.Api.Modules.CRM;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Api.Modules.Outbound;
using SGrowing.Api.Modules.Voice;
using SGrowing.Api.Services;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace SGrowing.Tests.E2E;

public class MasterOmnichannelEnterpriseTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid());
        return new SGrowingDbContext(options, tenantContext);
    }

    [Fact]
    public async Task MasterOmnichannel_FullLifecycle_Social_Voice_SDR_B2B_And_Workflows()
    {
        // -------------------------------------------------------------------------
        // 0. SETUP: Database, Outbox, Realtime Notifier & Services
        // -------------------------------------------------------------------------
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var inboxId = Guid.NewGuid();
        var outbox = new OutboxService(db);
        var notifier = new Mock<IRealtimeNotifier>();

        var ingressService = new ChannelIngressService(db, outbox);

        // Adapters
        var telegramAdapter = new TelegramAdapter();
        var discordAdapter = new DiscordAdapter();
        var metaAdapter = new MetaGraphAdapter();
        var xAdapter = new XTwitterAdapter();
        var smsAdapter = new SmsAdapter();

        // Voice Services
        var voiceService = new VoiceTelephonyService(db);
        var voiceAgent = new RealtimeVoiceAgentService(db);

        // Social Services
        var socialPublishing = new SocialPublishingService(db, outbox);
        var commentDeflector = new SocialCommentDeflectionService(db, outbox);

        // SDR Services
        var mailboxService = new MailboxService(db);
        var linkedInAdapter = new LinkedInOutreachAdapter(null, outbox);
        var sdrOrchestrator = new MultiTouchCadenceOrchestrator(db, mailboxService, linkedInAdapter, voiceService, outbox);

        // B2B & Workflows
        var companyService = new CompanyAccountService(db);
        var workflowEngine = new WorkflowEngineService(db);

        // Ensure default inbox exists
        var inbox = new Inbox
        {
            Id = inboxId,
            TenantId = tenantId,
            Name = "Universal Omni Inbox",
            ChannelType = ChannelType.WhatsAppCloud
        };
        db.Inboxes.Add(inbox);
        await db.SaveChangesAsync();

        // =========================================================================
        // 1. UNIVERSAL CHANNEL INGRESS: Telegram, Discord, Meta, X, and SMS
        // =========================================================================
        
        // 1.1 Telegram Inbound
        var tgPayload = """
        {
            "update_id": 1001,
            "message": {
                "message_id": 55,
                "from": { "id": 987654321, "first_name": "Tony", "username": "ironman" },
                "text": "Hello SGrowing! I'm interested in the omnicommunication platform."
            }
        }
        """;
        var tgMsg = telegramAdapter.ParseInboundWebhook(tenantId, tgPayload);
        tgMsg.Should().NotBeNull();
        tgMsg!.ChannelType.Should().Be("telegram");
        var tgIngress = await ingressService.IngestAsync(inboxId, tgMsg);
        tgIngress.IsSuccess.Should().BeTrue();

        // 1.2 Discord Inbound
        var discordPayload = """
        {
            "id": "dsc-msg-123",
            "content": "Can we connect Discord support channels directly into CRM?",
            "author": { "id": "user-disc-999", "username": "GamerDev" }
        }
        """;
        var discordMsg = discordAdapter.ParseInboundWebhook(tenantId, discordPayload);
        discordMsg.Should().NotBeNull();
        discordMsg!.ChannelType.Should().Be("discord");
        var discordIngress = await ingressService.IngestAsync(inboxId, discordMsg);
        discordIngress.IsSuccess.Should().BeTrue();

        // 1.3 Meta Instagram DM Inbound
        var metaPayload = """
        {
            "object": "instagram",
            "entry": [
                {
                    "messaging": [
                        {
                            "sender": { "id": "ig-user-777" },
                            "recipient": { "id": "sgrowing-page" },
                            "message": { "mid": "mid.123", "text": "Can you share pricing details for enterprise?" }
                        }
                    ]
                }
            ]
        }
        """;
        var metaMsg = metaAdapter.ParseInboundWebhook(tenantId, metaPayload);
        metaMsg.Should().NotBeNull();
        metaMsg!.ChannelType.Should().Be("meta_graph");
        var metaIngress = await ingressService.IngestAsync(inboxId, metaMsg);
        metaIngress.IsSuccess.Should().BeTrue();

        // 1.4 X (Twitter) DM Inbound
        var xPayload = """
        {
            "direct_message_events": [
                {
                    "type": "message_create",
                    "id": "tw-dm-444",
                    "message_create": {
                        "sender_id": "twitter-user-555",
                        "message_data": { "text": "Loving the new SGrowing release on X!" }
                    }
                }
            ]
        }
        """;
        var xMsg = xAdapter.ParseInboundWebhook(tenantId, xPayload);
        xMsg.Should().NotBeNull();
        xMsg!.ChannelType.Should().Be("x_twitter");
        var xIngress = await ingressService.IngestAsync(inboxId, xMsg);
        xIngress.IsSuccess.Should().BeTrue();

        // 1.5 SMS / MMS Inbound
        var smsPayload = """
        {
            "MessageSid": "SM99887766",
            "From": "+15554443322",
            "To": "+18005550199",
            "Body": "YES, please book my consultation demo."
        }
        """;
        var smsMsg = smsAdapter.ParseInboundWebhook(tenantId, smsPayload);
        smsMsg.Should().NotBeNull();
        smsMsg!.ChannelType.Should().Be("sms");
        var smsIngress = await ingressService.IngestAsync(inboxId, smsMsg);
        smsIngress.IsSuccess.Should().BeTrue();

        // Total messages ingested across all channels
        var totalMessages = await db.Messages.Where(m => m.TenantId == tenantId).CountAsync();
        totalMessages.Should().Be(5);

        // =========================================================================
        // 2. TELEPHONY & REAL-TIME AI VOICE AGENT
        // =========================================================================
        var callSid = "CA_live_test_call_9901";
        var callerPhone = "+15553039999";
        
        // Inbound call arrives -> Generate TwiML
        var twiml = await voiceService.HandleInboundCallWebhookAsync(tenantId, callSid, callerPhone, "+18005550199");
        twiml.Should().Contain("<Response>");
        twiml.Should().Contain("<Say");
        twiml.Should().Contain("SGrowing");

        var voiceSession = await db.VoiceCallSessions.FirstOrDefaultAsync(s => s.CallSid == callSid);
        voiceSession.Should().NotBeNull();
        voiceSession!.Status.Should().Be(CallStatus.InProgress);

        // Voice reasoning loop turn
        var voiceResponse = await voiceAgent.ProcessVoiceTurnAsync(
            tenantId,
            voiceSession.Id,
            "I'd like to check our enterprise order and pricing."
        );
        voiceResponse.Should().NotBeNullOrWhiteSpace();

        // Complete call
        voiceSession.CompleteCall(75, transcript: "Caller inquired about enterprise order.");
        await db.SaveChangesAsync();

        var completedCall = await db.VoiceCallSessions.FindAsync(voiceSession.Id);
        completedCall!.Status.Should().Be(CallStatus.Completed);
        completedCall.DurationSeconds.Should().Be(75);

        // =========================================================================
        // 3. SOCIAL MEDIA MULTI-PUBLISHING & PUBLIC COMMENT DEFLECTION
        // =========================================================================
        
        // 3.1 Schedule & Publish Social Post across FB, IG, LinkedIn, Telegram, and X
        var targetPlatforms = new List<SocialPlatform>
        {
            SocialPlatform.Facebook,
            SocialPlatform.Instagram,
            SocialPlatform.LinkedIn,
            SocialPlatform.Telegram,
            SocialPlatform.X
        };
        var scheduledPost = await socialPublishing.CreatePostAsync(
            tenantId,
            "SGrowing Omnicommunication: Autonomous AI Agents across Voice, WhatsApp, Telegram, X & SMS!",
            targetPlatforms,
            scheduledAtUtc: DateTime.UtcNow.AddMinutes(-5) // Due for publishing
        );
        scheduledPost.Status.Should().Be(SocialPostStatus.Scheduled);

        var publishedPosts = await socialPublishing.PublishDuePostsAsync(tenantId);
        publishedPosts.Should().HaveCount(1);

        // Outbox should contain published events for all 5 platforms
        var socialOutboxEvents = await db.OutboxMessages
            .Where(m => m.TenantId == tenantId && m.EventType == "SocialPost.Published")
            .ToListAsync();
        socialOutboxEvents.Should().HaveCount(5);

        // 3.2 Public Instagram Comment asking for pricing -> Auto-Deflect to Private DM
        var deflectionRes = await commentDeflector.HandlePublicCommentAsync(
            tenantId,
            SocialPlatform.Instagram,
            "post_ig_8899",
            "growth_enthusiast",
            "What is the pricing for this? Can you send me a demo link?"
        );
        deflectionRes.Deflected.Should().BeTrue();
        deflectionRes.ReplyText.Should().Contain("Sent you a DM");

        // Verify DM task was enqueued to Outbox
        var dmOutboxTask = await db.OutboxMessages
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.EventType == "Social.DirectMessage.Send");
        dmOutboxTask.Should().NotBeNull();
        dmOutboxTask!.Payload.Should().Contain("growth_enthusiast");

        // =========================================================================
        // 4. MULTI-TOUCH AI SDR OUTREACH (LinkedIn + Email + WhatsApp + Voice)
        // =========================================================================
        var prospect = Contact.Create(tenantId, "Bruce", "Wayne", "bruce@wayne.corp", phone: "+1555998877");
        db.Contacts.Add(prospect);

        var senderAccount = SenderAccount.Create(tenantId, "sdr@trysgrowing.com", "Sarah SDR", MailboxProvider.CustomSmtp, targetDailyQuota: 50);
        db.SenderAccounts.Add(senderAccount);
        await db.SaveChangesAsync();

        var campaignId = Guid.NewGuid();

        // Create multi-touch touchpoints:
        // Touchpoint 1: LinkedIn Connect
        var tp1 = CadenceTouchpoint.Create(tenantId, campaignId, stepNumber: 1, TouchpointChannel.LinkedInConnect, delayDays: 0, subjectTemplate: null, bodyTemplate: "Hi {{first_name}}, let's connect!");
        // Touchpoint 2: Personalized Email
        var tp2 = CadenceTouchpoint.Create(tenantId, campaignId, stepNumber: 2, TouchpointChannel.Email, delayDays: 0, subjectTemplate: "SGrowing for Wayne Corp", bodyTemplate: "Hello {{first_name}}, check out our B2B case study.");
        // Touchpoint 3: WhatsApp
        var tp3 = CadenceTouchpoint.Create(tenantId, campaignId, stepNumber: 3, TouchpointChannel.WhatsApp, delayDays: 0, subjectTemplate: null, bodyTemplate: "Hi {{first_name}}, following up on my email via WhatsApp!");
        // Touchpoint 4: Voice Call
        var tp4 = CadenceTouchpoint.Create(tenantId, campaignId, stepNumber: 4, TouchpointChannel.VoiceCall, delayDays: 0, subjectTemplate: null, bodyTemplate: "Scheduled qualification call.");
        
        db.CadenceTouchpoints.AddRange(tp1, tp2, tp3, tp4);

        // Create pending executions due today
        var exec1 = TouchpointExecution.Create(tenantId, tp1.Id, prospect.Id, TouchpointChannel.LinkedInConnect, DateTime.UtcNow.AddMinutes(-10));
        var exec2 = TouchpointExecution.Create(tenantId, tp2.Id, prospect.Id, TouchpointChannel.Email, DateTime.UtcNow.AddMinutes(-10));
        var exec3 = TouchpointExecution.Create(tenantId, tp3.Id, prospect.Id, TouchpointChannel.WhatsApp, DateTime.UtcNow.AddMinutes(-10));
        var exec4 = TouchpointExecution.Create(tenantId, tp4.Id, prospect.Id, TouchpointChannel.VoiceCall, DateTime.UtcNow.AddMinutes(-10));
        
        db.TouchpointExecutions.AddRange(exec1, exec2, exec3, exec4);
        await db.SaveChangesAsync();

        // Dispatch all due multi-touch steps
        var sdrProcessed = await sdrOrchestrator.ProcessDueMultiTouchStepsAsync(tenantId);
        sdrProcessed.Should().Be(4);

        // Verify all 4 executions marked Sent
        var updatedExecs = await db.TouchpointExecutions.Where(e => e.TenantId == tenantId).ToListAsync();
        updatedExecs.Should().OnlyContain(e => e.Status == "Sent");

        // Verify mailbox quota incremented
        senderAccount.SentTodayCount.Should().Be(1);

        // =========================================================================
        // 5. B2B CRM ACCOUNT HIERARCHIES & VISUAL NODE WORKFLOW AUTOMATION
        // =========================================================================
        
        // 5.1 Create Parent Company and Subsidiary
        var parentCompany = await companyService.CreateCompanyAsync(
            tenantId,
            "Wayne Enterprises",
            "wayne.corp",
            industry: "Conglomerate & Defense",
            revenue: 85000000m
        );
        parentCompany.Should().NotBeNull();
        parentCompany.Domain.Should().Be("wayne.corp");

        // 5.2 Auto-link Contact by Email Domain (@wayne.corp)
        var linkedCompany = await companyService.AssociateContactByDomainAsync(tenantId, prospect.Id, prospect.Email!);
        linkedCompany.Should().NotBeNull();
        linkedCompany!.Id.Should().Be(parentCompany.Id);

        var refreshedProspect = await db.Contacts.FindAsync(prospect.Id);
        refreshedProspect!.CompanyId.Should().Be(parentCompany.Id);
        refreshedProspect.CustomFieldsJson.Should().Contain("companyId");

        // 5.3 Execute Visual Node Automation Graph (n8n model)
        var nodesJson = """
        [
            {
                "id": "node_trigger",
                "type": "Trigger",
                "name": "Inbound Message Trigger",
                "next": "node_condition"
            },
            {
                "id": "node_condition",
                "type": "Condition",
                "name": "Check Priority",
                "config": {
                    "field": "score",
                    "operator": ">",
                    "value": 80
                },
                "trueNext": "node_vip_action",
                "falseNext": "node_standard_action"
            },
            {
                "id": "node_vip_action",
                "type": "Action: SendMessage",
                "name": "Send VIP Telegram Message",
                "config": {
                    "channel": "telegram",
                    "template": "Hello {{name}}, an executive account rep has been assigned to your case."
                },
                "next": "node_crm_update"
            },
            {
                "id": "node_crm_update",
                "type": "Action: UpdateCRM",
                "name": "Promote to Opportunity",
                "config": {
                    "lifecycleStage": "Opportunity",
                    "tags": ["vip", "enterprise-qualified"]
                }
            }
        ]
        """;

        var workflowGraph = WorkflowGraph.Create(tenantId, "VIP Lead Inbound Router", "InboundMessage", nodesJson);
        WorkflowEngineService.RegisterWorkflow(workflowGraph);
        db.WorkflowGraphs.Add(workflowGraph);
        await db.SaveChangesAsync();

        var workflowPayload = """
        {
            "name": "Bruce Wayne",
            "score": 95,
            "contactId": "%CONTACT_ID%"
        }
        """.Replace("%CONTACT_ID%", prospect.Id.ToString());

        var execution = await workflowEngine.ExecuteWorkflowAsync(tenantId, workflowGraph.Id, workflowPayload);
        execution.Should().NotBeNull();
        execution.Status.Should().Be("Completed");
        execution.ExecutedNodesCount.Should().BeGreaterThanOrEqualTo(3);
        execution.ExecutionLog.Should().Contain("VIP");
        execution.ExecutionLog.Should().Contain("Telegram");
    }
}
