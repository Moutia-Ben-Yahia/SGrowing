using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.Channels;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Api.Modules.CRM;
using SGrowing.Api.Modules.KnowledgeBase;
using SGrowing.Api.Services;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace SGrowing.Tests.E2E;

public class EnterpriseOmniEndToEndTests
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
    public async Task MasterEnterpriseLifecycle_EndToEnd_AutonomousBot_Identity360_Broadcast_Booking_Checkout_And_SLA()
    {
        // -------------------------------------------------------------------------
        // SETUP: DbContext, Outbox, Notifier & Enterprise Services
        // -------------------------------------------------------------------------
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var outbox = new OutboxService(db);
        var notifier = new Mock<IRealtimeNotifier>();
        var ragService = new HelpCenterRagService(db);

        var tools = new List<IAgentTool>
        {
            new KnowledgeBaseTool(ragService),
            new OrderLookupTool(db),
            new EscalateToHumanTool(db)
        };

        var autonomousAgent = new AutonomousAiAgentService(db, tools, notifier.Object);
        var identityResolver = new IdentityResolverService(db);
        var broadcastDispatcher = new BroadcastDispatcherService(db, outbox);
        var bookingService = new AppointmentBookingService(db, notifier.Object);
        var checkoutService = new CheckoutLinkService(db, notifier.Object);
        var slaService = new SlaMonitoringService(db, notifier.Object);

        // =========================================================================
        // 1. CROSS-CHANNEL 360 CUSTOMER IDENTITY RESOLUTION (Pillar B)
        // =========================================================================
        // Contact first arrives via WhatsApp
        var contactRes1 = await identityResolver.ResolveOrCreateContactAsync(
            tenantId,
            email: null,
            phone: "+1 (555) 303-9999",
            firstName: "Diana",
            lastName: "Prince"
        );
        contactRes1.IsSuccess.Should().BeTrue();
        var contact = contactRes1.Value;
        contact.Phone.Should().Be("15553039999");
        contact.Email.Should().BeNull();

        // Later contact contacts via Email with same phone
        var contactRes2 = await identityResolver.ResolveOrCreateContactAsync(
            tenantId,
            email: "diana.prince@themyscira.com",
            phone: "15553039999",
            firstName: "Diana",
            lastName: "Prince"
        );
        contactRes2.IsSuccess.Should().BeTrue();
        contactRes2.Value.Id.Should().Be(contact.Id); // Same contact unified!
        contactRes2.Value.Email.Should().Be("diana.prince@themyscira.com");
        contactRes2.Value.Phone.Should().Be("15553039999");

        // Tag contact for VIP campaign
        contact.Tags.Add("vip-attendee");
        await db.SaveChangesAsync();

        // =========================================================================
        // 2. AUTONOMOUS MULTI-TURN AI AGENT & ESCALATE TO HUMAN TOOL (Pillar A)
        // =========================================================================
        var conv = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        conv.AiMode = AiMode.Autonomous; // Autonomous bot handles directly
        db.Conversations.Add(conv);

        // Seed a sample order/deal for the order tool
        var existingDeal = Deal.Create(tenantId, contact.Id, "Enterprise Trial Order", 1200m, DealStage.Proposal);
        db.Deals.Add(existingDeal);
        await db.SaveChangesAsync();

        // Turn 1: Customer asks for order info -> Tool calling executes OrderLookup
        var botTurn1 = await autonomousAgent.HandleIncomingMessageAsync(
            tenantId,
            conv.Id,
            contact.Id,
            "Could you check the status of my order and deal?"
        );
        botTurn1.IsSuccess.Should().BeTrue();
        botTurn1.Value.Processed.Should().BeTrue();
        botTurn1.Value.EscalatedToHuman.Should().BeFalse();
        botTurn1.Value.ReplySent.Should().Contain("Enterprise Trial Order");

        // Turn 2: Customer wants human intervention -> EscalateToHuman tool called
        var botTurn2 = await autonomousAgent.HandleIncomingMessageAsync(
            tenantId,
            conv.Id,
            contact.Id,
            "I want to speak with a human representative please."
        );
        botTurn2.IsSuccess.Should().BeTrue();
        botTurn2.Value.Processed.Should().BeTrue();
        botTurn2.Value.EscalatedToHuman.Should().BeTrue();
        botTurn2.Value.ReplySent.Should().Contain("connected you with our human support team");

        // Conversation should be switched to Copilot and escalated to High priority
        var updatedConv = await db.Conversations.FindAsync(conv.Id);
        updatedConv!.AiMode.Should().Be(AiMode.Copilot);
        updatedConv.Priority.Should().Be(ConversationPriority.High);
        notifier.Verify(n => n.NotifyConversationUpdatedAsync(tenantId, It.IsAny<ConversationDto>()), Times.AtLeastOnce());

        // =========================================================================
        // 3. META WHATSAPP TEMPLATE MANAGER & AUDIENCE BROADCAST ENGINE (Pillar C)
        // =========================================================================
        var template = WhatsAppTemplate.Create(
            tenantId,
            "vip_annual_summit",
            WhatsAppTemplateCategory.Marketing,
            bodyText: "Hello {{1}} {{2}}, you are exclusively invited to SGrowing Global Summit! Reply YES to RSVP.",
            language: "en_US"
        );
        template.Status = WhatsAppTemplateStatus.Approved;
        db.WhatsAppTemplates.Add(template);

        var campaign = BroadcastCampaign.Create(
            tenantId,
            "Global Summit VIP Blast",
            template.Id,
            targetTag: "vip-attendee"
        );
        db.BroadcastCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        // Dispatch broadcast
        var broadcastRes = await broadcastDispatcher.DispatchBroadcastAsync(tenantId, campaign.Id);
        broadcastRes.IsSuccess.Should().BeTrue();
        broadcastRes.Value.RecipientsMatched.Should().Be(1);
        broadcastRes.Value.SentCount.Should().Be(1);

        // Check Outbox for WhatsApp broadcast message
        var outboxMsg = await db.OutboxMessages.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.EventType == "WhatsApp.Template.Send");
        outboxMsg.Should().NotBeNull();
        outboxMsg!.Payload.Should().Contain("Diana");
        outboxMsg.Payload.Should().Contain("15553039999");

        // =========================================================================
        // 4. IN-CHAT CALENDAR BOOKING & ASSISTED COMMERCE CHECKOUT (Pillar D)
        // =========================================================================
        var slot = AppointmentSlot.Create(tenantId, "Strategy & Architecture Consultation", 45, "Sales Specialist");
        db.AppointmentSlots.Add(slot);
        await db.SaveChangesAsync();

        // Customer books appointment
        var scheduledTime = DateTime.UtcNow.AddDays(2);
        var bookingRes = await bookingService.BookSlotAsync(tenantId, slot.Id, contact.Id, scheduledTime);
        bookingRes.IsSuccess.Should().BeTrue();
        bookingRes.Value.Status.Should().Be(BookingStatus.Confirmed);

        // Verify contact lifecycle advanced to Lead and tagged
        var bookedContact = await db.Contacts.FindAsync(contact.Id);
        bookedContact!.LifecycleStage.Should().Be(LifecycleStage.Lead);
        bookedContact.Tags.Should().Contain("demo-scheduled");

        // Agent creates assisted checkout link in conversation
        var checkoutRes = await checkoutService.CreateCheckoutLinkAsync(
            tenantId,
            contact.Id,
            conv.Id,
            "Enterprise Annual Subscription",
            499.00m,
            "USD"
        );
        checkoutRes.IsSuccess.Should().BeTrue();
        var checkoutLink = checkoutRes.Value;
        checkoutLink.PaymentUrl.Should().Contain("/session/");
        checkoutLink.Status.Should().Be(CheckoutStatus.Active);

        // Customer pays via checkout link
        var paymentRes = await checkoutService.MarkPaidAsync(tenantId, checkoutLink.Id);
        paymentRes.IsSuccess.Should().BeTrue();
        paymentRes.Value.Status.Should().Be(CheckoutStatus.Paid);

        // Verify a new Won Deal was automatically generated in CRM pipeline
        var wonDeal = await db.Deals.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.ContactId == contact.Id && d.Stage == DealStage.Won);
        wonDeal.Should().NotBeNull();
        wonDeal!.Amount.Should().Be(499.00m);
        wonDeal.Title.Should().Contain("Enterprise Annual Subscription");

        // Verify contact advanced to Customer
        var payingContact = await db.Contacts.FindAsync(contact.Id);
        payingContact!.LifecycleStage.Should().Be(LifecycleStage.Customer);
        payingContact.Tags.Should().Contain("paying-customer");

        // =========================================================================
        // 5. SLA ESCALATION ENGINE (Pillar E)
        // =========================================================================
        var urgentSlaPolicy = SlaPolicy.Create(
            tenantId,
            "Urgent Tier 1 SLA",
            ConversationPriority.High,
            targetFirstResponseMinutes: 10,
            targetResolutionMinutes: 60
        );
        db.SlaPolicies.Add(urgentSlaPolicy);

        // Create an unresponded conversation that has exceeded the 10 min target
        var staleConv = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        staleConv.Priority = ConversationPriority.High;
        typeof(Conversation).GetProperty("CreatedAtUtc")!.SetValue(staleConv, DateTime.UtcNow.AddMinutes(-25)); // 25 mins ago with no response
        staleConv.FirstResponseAtUtc = null;
        staleConv.IsSlaBreached = false;
        db.Conversations.Add(staleConv);
        await db.SaveChangesAsync();

        // Run SLA monitor evaluation
        var breachedCount = await slaService.EvaluateSlaBreachesAsync(tenantId);
        breachedCount.Should().Be(1);

        // Verify conversation is escalated to Urgent Priority and flagged breached
        var evaluatedConv = await db.Conversations.FindAsync(staleConv.Id);
        evaluatedConv!.IsSlaBreached.Should().BeTrue();
        evaluatedConv.Priority.Should().Be(ConversationPriority.Urgent);

        // =========================================================================
        // 6. 360 CUSTOMER UNIFIED TIMELINE AUDIT
        // =========================================================================
        var timelineRes = await identityResolver.GetUnifiedCustomerTimelineAsync(tenantId, contact.Id);
        timelineRes.IsSuccess.Should().BeTrue();
        var timeline = timelineRes.Value;
        timeline.Contact.Id.Should().Be(contact.Id);
        timeline.Conversations.Should().HaveCount(2); // conv + staleConv
        timeline.Deals.Should().HaveCount(2);         // existing deal + won deal from checkout
    }
}
