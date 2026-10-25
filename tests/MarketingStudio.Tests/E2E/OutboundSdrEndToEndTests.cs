using FluentAssertions;
using MarketingStudio.Api.Data;
using MarketingStudio.Api.Modules.Conversations;
using MarketingStudio.Api.Modules.Outbound;
using MarketingStudio.Api.Services;
using MarketingStudio.Core.Entities;
using MarketingStudio.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MarketingStudio.Tests.E2E;

public class OutboundSdrEndToEndTests
{
    private MarketingStudioDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MarketingStudioDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid());
        return new MarketingStudioDbContext(options, tenantContext);
    }

    [Fact]
    public async Task CompleteOutboundSdrLifecycle_FromCadenceDispatchToReplySentimentAndDealCreation()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var outbox = new OutboxService(db);
        var notifier = new Mock<IRealtimeNotifier>();

        var mailboxService = new MailboxService(db);
        var aiSdrService = new AiSdrService();
        var sentimentClassifier = new ReplySentimentClassifier();
        var handoffService = new ProspectReplyHandoffService(db, sentimentClassifier, notifier.Object);
        var dispatcher = new CadenceDispatcherService(db, mailboxService, aiSdrService, outbox);

        // 1. Configure Sender Mailbox with Warmup
        var sender = SenderAccount.Create(
            tenantId: tenantId,
            email: "sdr-alex@trymarketingstudio.com",
            displayName: "Alex Rivers",
            provider: MailboxProvider.GoogleWorkspace,
            targetDailyQuota: 50,
            rampDailyIncrement: 5
        );
        db.SenderAccounts.Add(sender);

        // 2. Create Target Contact
        var contact = Contact.Create(
            tenantId: tenantId,
            firstName: "Elena",
            lastName: "Rostova",
            email: "elena@finscale.tech"
        );
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        // 3. Create Multi-Touch Outbound Campaign
        var campaign = OutboundCampaign.Create(tenantId, "Q4 FinTech Hypergrowth", new List<Guid> { sender.Id });
        db.OutboundCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var step1 = CadenceStep.Create(
            tenantId,
            campaign.Id,
            stepNumber: 1,
            delayDays: 0,
            channel: OutboundChannel.Email,
            subjectTemplate: "Scaling FinScale outreach",
            bodyTemplate: "Hi {{first_name}}, {{icebreaker}} Wanted to check your thoughts on scaling sales."
        );
        var step2 = CadenceStep.Create(
            tenantId,
            campaign.Id,
            stepNumber: 2,
            delayDays: 3,
            channel: OutboundChannel.Email,
            subjectTemplate: "Quick follow up regarding FinScale",
            bodyTemplate: "Hi {{first_name}}, following up on my previous note."
        );
        db.CadenceSteps.AddRange(step1, step2);

        // 4. Enroll Contact and generate AI SDR personalized hook
        var enrollment = ProspectEnrollment.Create(tenantId, campaign.Id, contact.Id);
        var sdrOutput = await aiSdrService.GeneratePersonalizationAsync(new SdrPersonalizationInput("Elena", "Rostova", "FinScale", "FinTech"));
        enrollment.PersonalizedHook = sdrOutput.Icebreaker;
        db.ProspectEnrollments.Add(enrollment);
        await db.SaveChangesAsync();

        enrollment.Status.Should().Be(EnrollmentStatus.Active);
        enrollment.CurrentStepNumber.Should().Be(1);
        enrollment.PersonalizedHook.Should().NotBeNullOrWhiteSpace();

        // 5. Dispatcher runs Step 1: renders personalized content & enqueues to Outbox
        var processedCount = await dispatcher.ProcessDueCadencesAsync(tenantId);
        processedCount.Should().Be(1);

        // Verify outbox has outbound email
        var outboxMessages = await db.OutboxMessages.ToListAsync();
        outboxMessages.Should().ContainSingle();
        outboxMessages.First().Payload.Should().Contain(contact.Email);
        outboxMessages.First().Payload.Should().Contain("Elena");

        // Verify enrollment advanced to Step 2
        var updatedEnrollment = await db.ProspectEnrollments.FindAsync(enrollment.Id);
        updatedEnrollment!.CurrentStepNumber.Should().Be(2);

        // Verify sender account sent today incremented
        var updatedSender = await db.SenderAccounts.FindAsync(sender.Id);
        updatedSender!.SentTodayCount.Should().Be(1);

        // 6. Prospect Replies with Positive Intent: "This sounds great, can we schedule a demo call tomorrow?"
        var conversation = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();

        var prospectReplyText = "This sounds really interesting, we are looking for this. Can we schedule a demo call tomorrow?";
        var handoffResult = await handoffService.HandleReplyAsync(tenantId, contact.Id, conversation.Id, prospectReplyText);

        // 7. Verify Closed-Loop SDR Handoff
        handoffResult.IsSuccess.Should().BeTrue();
        handoffResult.Value.Sentiment.Should().Be(ReplySentiment.Interested);
        handoffResult.Value.DealCreated.Should().BeTrue();

        // Verify Deal created in CRM
        var deal = await db.Deals.FirstOrDefaultAsync(d => d.ContactId == contact.Id);
        deal.Should().NotBeNull();
        deal!.Amount.Should().Be(5000m);
        deal.Stage.Should().Be(DealStage.Qualified);
        deal.Title.Should().Contain("Elena");

        // Verify enrollment is marked Replied and stopped
        var finalEnrollment = await db.ProspectEnrollments.FindAsync(enrollment.Id);
        finalEnrollment!.Status.Should().Be(EnrollmentStatus.Replied);
        finalEnrollment.NextRunAtUtc.Should().BeNull();

        // Verify Real-time alert was emitted to omni agents
        notifier.Verify(n => n.NotifyConversationUpdatedAsync(tenantId, It.IsAny<ConversationDto>()), Times.Once);
    }
}
