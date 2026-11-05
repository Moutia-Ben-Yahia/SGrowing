using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Conversations.Unified;
using Xunit;

namespace SGrowing.Tests.Conversations;

public class UnifiedInboxTests
{
    [Fact]
    public void OmnichannelStitching_EmailAndPhoneAcrossChannels_StitchesIntoSingleThread()
    {
        // Arrange
        var engine = new UnifiedInboxEngine();
        var tenantId = "tenant_growth_omni";

        // Step 1: Customer initiates contact via Email
        var emailReq = new IngestMessageRequest(
            TenantId: tenantId,
            Channel: ConversationChannel.Email,
            CustomerEmail: "sarah.connor@cyberdyne.io",
            CustomerPhone: string.Empty,
            SenderName: "Sarah Connor",
            Content: "Hello, I cannot access my billing invoice for last month."
        );
        var thread1 = engine.IngestMessage(emailReq);

        thread1.Should().NotBeNull();
        thread1.CustomerEmail.Should().Be("sarah.connor@cyberdyne.io");
        thread1.ActiveChannels.Should().ContainSingle().Which.Should().Be(ConversationChannel.Email);
        thread1.Messages.Should().HaveCount(1);

        // Step 2: Customer contacts via WhatsApp with same email and provides phone number
        var whatsappReq = new IngestMessageRequest(
            TenantId: tenantId,
            Channel: ConversationChannel.WhatsApp,
            CustomerEmail: "sarah.connor@cyberdyne.io",
            CustomerPhone: "+1-555-0199",
            SenderName: "Sarah Connor",
            Content: "Following up from WhatsApp as well, need this urgently."
        );
        var thread2 = engine.IngestMessage(whatsappReq);

        thread2.Id.Should().Be(thread1.Id, "Messages with matching customer email must be stitched into the existing thread");
        thread2.CustomerPhone.Should().Be("+1-555-0199");
        thread2.ActiveChannels.Should().BeEquivalentTo([ConversationChannel.Email, ConversationChannel.WhatsApp]);
        thread2.Messages.Should().HaveCount(2);

        // Step 3: Customer contacts via SMS with formatted phone number and no email
        var smsReq = new IngestMessageRequest(
            TenantId: tenantId,
            Channel: ConversationChannel.Sms,
            CustomerEmail: string.Empty,
            CustomerPhone: "+1 (555) 0199",
            SenderName: "Sarah Connor",
            Content: "Checking status via SMS."
        );
        var thread3 = engine.IngestMessage(smsReq);

        thread3.Id.Should().Be(thread1.Id, "Messages with matching normalized phone number must be stitched into the existing thread");
        thread3.ActiveChannels.Should().BeEquivalentTo([ConversationChannel.Email, ConversationChannel.WhatsApp, ConversationChannel.Sms]);
        thread3.Messages.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("I am furious and angry, this is totally unacceptable and I will contact my lawyer!", CustomerSentiment.Frustrated, TicketPriority.Urgent)]
    [InlineData("There is a critical bug in checkout and payments fail with error 500.", CustomerSentiment.Negative, TicketPriority.High)]
    [InlineData("Thank you so much! The support was awesome and amazing, problem resolved perfectly.", CustomerSentiment.Positive, TicketPriority.Low)]
    [InlineData("Could you please inform me when the next webinar starts?", CustomerSentiment.Neutral, TicketPriority.Medium)]
    public void SentimentClassification_AccuratelyDetectsToneAndCalculatesInitialPriority(
        string messageContent,
        CustomerSentiment expectedSentiment,
        TicketPriority expectedPriority)
    {
        // Arrange
        var engine = new UnifiedInboxEngine();
        var tenantId = "tenant_sentiment_test";

        var req = new IngestMessageRequest(
            TenantId: tenantId,
            Channel: ConversationChannel.WebChat,
            CustomerEmail: "tester@example.com",
            CustomerPhone: "+1234567890",
            SenderName: "Alex Mercer",
            Content: messageContent
        );

        // Act
        var thread = engine.IngestMessage(req);

        // Assert
        thread.Messages.Should().HaveCount(1);
        thread.Messages[0].Sentiment.Should().Be(expectedSentiment);
        thread.Priority.Should().Be(expectedPriority);
    }

    [Fact]
    public void AiDeflection_PricingInquiry_ProducesHighConfidenceDeflectionAndMarksThread()
    {
        // Arrange
        var engine = new UnifiedInboxEngine();
        var tenantId = "tenant_ai_deflect";

        var req = new IngestMessageRequest(
            TenantId: tenantId,
            Channel: ConversationChannel.WebChat,
            CustomerEmail: "prospect@saas.com",
            CustomerPhone: "+10987654321",
            SenderName: "Prospect User",
            Content: "Hi! How much does your enterprise plan cost, and what pricing tiers do you have?"
        );
        var thread = engine.IngestMessage(req);

        // Act
        var deflection = engine.EvaluateAiDeflection(tenantId, thread.Id);

        // Assert
        deflection.Should().NotBeNull();
        deflection.ConfidenceScore.Should().BeGreaterThanOrEqualTo(0.85);
        deflection.CanAutoSend.Should().BeTrue();
        deflection.SourceArticle.Should().Contain("Pricing and Subscription");
        deflection.SuggestedResponse.Should().Contain("/pricing");

        var updatedThread = engine.GetThread(tenantId, thread.Id);
        updatedThread.Should().NotBeNull();
        updatedThread!.AiDeflected.Should().BeTrue();
    }

    [Fact]
    public void AiDeflection_AmbiguousInquiry_ReturnsLowConfidenceWithoutAutoSend()
    {
        // Arrange
        var engine = new UnifiedInboxEngine();
        var tenantId = "tenant_ai_ambiguous";

        var req = new IngestMessageRequest(
            TenantId: tenantId,
            Channel: ConversationChannel.Email,
            CustomerEmail: "vague@saas.com",
            CustomerPhone: string.Empty,
            SenderName: "Vague User",
            Content: "Hello there."
        );
        var thread = engine.IngestMessage(req);

        // Act
        var deflection = engine.EvaluateAiDeflection(tenantId, thread.Id);

        // Assert
        deflection.Should().NotBeNull();
        deflection.ConfidenceScore.Should().BeLessThan(0.85);
        deflection.CanAutoSend.Should().BeFalse();
        deflection.SourceArticle.Should().Be("KB-001: General Support FAQ");

        var updatedThread = engine.GetThread(tenantId, thread.Id);
        updatedThread!.AiDeflected.Should().BeFalse();
    }

    [Fact]
    public void SlaBreachDetection_WhenClockPassesDueTime_FlagsBreachedProperly()
    {
        // Arrange
        var simulatedTime = new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc);
        var engine = new UnifiedInboxEngine(() => simulatedTime);
        var tenantId = "tenant_sla_test";

        // Ingest urgent message (1-hour SLA)
        var req = new IngestMessageRequest(
            TenantId: tenantId,
            Channel: ConversationChannel.WebChat,
            CustomerEmail: "ceo@critical.com",
            CustomerPhone: "+15559998888",
            SenderName: "Critical Client",
            Content: "Outraged! Everything is down and broken, unacceptable catastrophe!"
        );
        var thread = engine.IngestMessage(req);

        thread.Priority.Should().Be(TicketPriority.Urgent);
        thread.SlaDueAtUtc.Should().Be(simulatedTime.AddHours(1));
        thread.SlaBreached.Should().BeFalse();

        // Advance time by 30 minutes -> within SLA
        simulatedTime = simulatedTime.AddMinutes(30);
        var threadMidway = engine.GetThread(tenantId, thread.Id);
        threadMidway!.SlaBreached.Should().BeFalse();

        // Advance time past 1 hour (total 90 minutes) -> SLA breached
        simulatedTime = simulatedTime.AddMinutes(60);
        var threadBreached = engine.GetThread(tenantId, thread.Id);
        threadBreached!.SlaBreached.Should().BeTrue();
    }

    [Fact]
    public void AppendAgentReply_AppendsMessageUpdatesAssigneeAndTransitionsStatus()
    {
        // Arrange
        var engine = new UnifiedInboxEngine();
        var tenantId = "tenant_agent_reply";

        var req = new IngestMessageRequest(
            TenantId: tenantId,
            Channel: ConversationChannel.WebChat,
            CustomerEmail: "shopper@shop.com",
            CustomerPhone: "+14443332222",
            SenderName: "Shopper",
            Content: "Can I change my delivery address?"
        );
        var thread = engine.IngestMessage(req);

        thread.Status.Should().Be(TicketStatus.Open);
        thread.AssignedAgentId.Should().BeNull();

        // Act: Agent replies
        var updated = engine.AppendAgentReply(
            tenantId: tenantId,
            threadId: thread.Id,
            agentId: "agent_007",
            agentName: "James Bond",
            content: "Certainly! I have updated your destination address.",
            channel: ConversationChannel.WhatsApp
        );

        // Assert
        updated.AssignedAgentId.Should().Be("agent_007");
        updated.Status.Should().Be(TicketStatus.PendingCustomer);
        updated.ActiveChannels.Should().Contain(ConversationChannel.WhatsApp);
        updated.Messages.Should().HaveCount(2);

        var agentMsg = updated.Messages[1];
        agentMsg.SenderId.Should().Be("agent_007");
        agentMsg.SenderName.Should().Be("James Bond");
        agentMsg.IsFromCustomer.Should().BeFalse();
        agentMsg.Channel.Should().Be(ConversationChannel.WhatsApp);
    }

    [Fact]
    public void EscalateTicket_TransitionsStatusToEscalatedAndRecalculatesSla()
    {
        // Arrange
        var simulatedTime = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
        var engine = new UnifiedInboxEngine(() => simulatedTime);
        var tenantId = "tenant_escalate";

        var req = new IngestMessageRequest(
            TenantId: tenantId,
            Channel: ConversationChannel.Email,
            CustomerEmail: "support@client.com",
            CustomerPhone: "+18887776666",
            SenderName: "Client Lead",
            Content: "We would like to request clarification on our SLA response times."
        );
        var thread = engine.IngestMessage(req);

        thread.Priority.Should().Be(TicketPriority.Medium);
        thread.Status.Should().Be(TicketStatus.Open);

        // Act: Escalate ticket to Urgent
        var escalateReq = new EscalateTicketRequest(
            ThreadId: thread.Id,
            TenantId: tenantId,
            Reason: "Key Account Tier 1 Escalation requested by Account Exec",
            NewPriority: TicketPriority.Urgent
        );
        var escalated = engine.EscalateTicket(escalateReq);

        // Assert
        escalated.Status.Should().Be(TicketStatus.Escalated);
        escalated.Priority.Should().Be(TicketPriority.Urgent);
        escalated.SlaDueAtUtc.Should().Be(simulatedTime.AddHours(1));
        escalated.Messages.Should().Contain(m => m.SenderName == "Escalation Engine" && m.Content.Contains("Key Account Tier 1 Escalation"));
    }

    [Fact]
    public void GetThreads_WithStatusFilterAndMultiTenant_ReturnsIsolatedMatchingThreads()
    {
        // Arrange
        var engine = new UnifiedInboxEngine();
        var tenantA = "tenant_alpha";
        var tenantB = "tenant_beta";

        var t1 = engine.IngestMessage(new IngestMessageRequest(tenantA, ConversationChannel.Email, "a1@a.com", "", "Alpha1", "Inquiry 1"));
        var t2 = engine.IngestMessage(new IngestMessageRequest(tenantA, ConversationChannel.Email, "a2@a.com", "", "Alpha2", "Inquiry 2"));
        var t3 = engine.IngestMessage(new IngestMessageRequest(tenantB, ConversationChannel.Email, "b1@b.com", "", "Beta1", "Inquiry 3"));

        engine.EscalateTicket(new EscalateTicketRequest(t2.Id, tenantA, "Needs Tier 2 escalation", TicketPriority.High));

        // Act
        var alphaAll = engine.GetThreads(tenantA);
        var alphaEscalated = engine.GetThreads(tenantA, TicketStatus.Escalated);
        var betaAll = engine.GetThreads(tenantB);

        // Assert
        alphaAll.Should().HaveCount(2);
        alphaEscalated.Should().HaveCount(1).And.Contain(t => t.Id == t2.Id);
        betaAll.Should().HaveCount(1).And.Contain(t => t.Id == t3.Id);
    }

    [Fact]
    public void IngestMessage_WithDbContext_PersistsThreadAndMessagesToDatabase()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        using var db = new SGrowingDbContext(options);
        var engine = new UnifiedInboxEngine(db);

        var req = new IngestMessageRequest(
            TenantId: "tenant_ef_persist",
            Channel: ConversationChannel.Email,
            CustomerEmail: "customer@domain.com",
            CustomerPhone: "+15551234567",
            SenderName: "John Doe",
            Content: "I need help with my integration please."
        );

        var thread = engine.IngestMessage(req);

        // Verify thread entity in DbContext
        var threadEntity = db.UnifiedInboxThreads.Include(t => t.Messages).FirstOrDefault(t => t.Id == thread.Id);
        threadEntity.Should().NotBeNull();
        threadEntity!.TenantId.Should().Be("tenant_ef_persist");
        threadEntity.CustomerEmail.Should().Be("customer@domain.com");
        threadEntity.Messages.Should().HaveCount(1);
        threadEntity.Messages[0].Content.Should().Be("I need help with my integration please.");
    }

    [Fact]
    public void EngineRestart_WithDbContext_RestoresThreadsAndMessagesFromDatabase()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        Guid savedThreadId;

        using (var db1 = new SGrowingDbContext(options))
        {
            var engine1 = new UnifiedInboxEngine(db1);
            var req = new IngestMessageRequest(
                TenantId: "tenant_restart_test",
                Channel: ConversationChannel.WhatsApp,
                CustomerEmail: "alice@acme.corp",
                CustomerPhone: "+100200300",
                SenderName: "Alice",
                Content: "Urgent issue with production sync!"
            );

            var t = engine1.IngestMessage(req);
            savedThreadId = t.Id;
        }

        // Simulate app restart by instantiating a completely new engine with a fresh DbContext instance pointing to same DB
        using (var db2 = new SGrowingDbContext(options))
        {
            var engine2 = new UnifiedInboxEngine(db2);

            var restoredThread = engine2.GetThread("tenant_restart_test", savedThreadId);
            restoredThread.Should().NotBeNull();
            restoredThread!.CustomerEmail.Should().Be("alice@acme.corp");
            restoredThread.CustomerPhone.Should().Be("+100200300");
            restoredThread.ActiveChannels.Should().Contain(ConversationChannel.WhatsApp);
            restoredThread.Messages.Should().HaveCount(1);
            restoredThread.Messages[0].Content.Should().Be("Urgent issue with production sync!");
        }
    }

    [Fact]
    public void OmnichannelStitching_SurvivesEngineRestart_WhenPersistedToDbContext()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var tenantId = "tenant_stitch_restart";
        Guid threadId;

        // Session 1: customer sends Email
        using (var db1 = new SGrowingDbContext(options))
        {
            var engine1 = new UnifiedInboxEngine(db1);
            var req1 = new IngestMessageRequest(
                TenantId: tenantId,
                Channel: ConversationChannel.Email,
                CustomerEmail: "bob@builder.com",
                CustomerPhone: "",
                SenderName: "Bob Builder",
                Content: "Can we fix it?"
            );
            var t1 = engine1.IngestMessage(req1);
            threadId = t1.Id;
        }

        // Session 2: after restart, customer follows up on WhatsApp with phone + email
        using (var db2 = new SGrowingDbContext(options))
        {
            var engine2 = new UnifiedInboxEngine(db2);
            var req2 = new IngestMessageRequest(
                TenantId: tenantId,
                Channel: ConversationChannel.WhatsApp,
                CustomerEmail: "bob@builder.com",
                CustomerPhone: "+1999888777",
                SenderName: "Bob Builder",
                Content: "Yes we can!"
            );
            var t2 = engine2.IngestMessage(req2);

            t2.Id.Should().Be(threadId, "Should stitch into the thread loaded from database");
            t2.ActiveChannels.Should().Contain(ConversationChannel.Email);
            t2.ActiveChannels.Should().Contain(ConversationChannel.WhatsApp);
            t2.Messages.Should().HaveCount(2);
        }
    }

    [Fact]
    public void EscalateAndAgentReply_WithDbContext_PersistsStatusChangesAcrossRestarts()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var tenantId = "tenant_escalate_restart";
        Guid threadId;

        using (var db1 = new SGrowingDbContext(options))
        {
            var engine1 = new UnifiedInboxEngine(db1);
            var t = engine1.IngestMessage(new IngestMessageRequest(
                TenantId: tenantId,
                Channel: ConversationChannel.WebChat,
                CustomerEmail: "user@enterprise.com",
                CustomerPhone: "+123",
                SenderName: "Enterprise User",
                Content: "Broken database migration after release"
            ));
            threadId = t.Id;

            // Escalate
            engine1.EscalateTicket(new EscalateTicketRequest(threadId, tenantId, "Enterprise SLA Tier 1", TicketPriority.Urgent));

            // Agent replies
            engine1.AppendAgentReply(tenantId, threadId, "agent_lead_01", "Lead Engineer", "We are hotfixing right away.", ConversationChannel.WebChat);
        }

        // Verify across restart
        using (var db2 = new SGrowingDbContext(options))
        {
            var engine2 = new UnifiedInboxEngine(db2);
            var thread = engine2.GetThread(tenantId, threadId);

            thread.Should().NotBeNull();
            thread!.Status.Should().Be(TicketStatus.PendingCustomer);
            thread.Priority.Should().Be(TicketPriority.Urgent);
            thread.AssignedAgentId.Should().Be("agent_lead_01");
            thread.Messages.Should().HaveCount(3);
            thread.Messages[1].SenderName.Should().Be("Escalation Engine");
            thread.Messages[2].SenderName.Should().Be("Lead Engineer");
        }
    }
}
