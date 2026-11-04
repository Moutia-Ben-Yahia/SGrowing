using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Api.Modules.Outbound;
using SGrowing.Api.Services;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace SGrowing.Tests.E2E;

public class OutboundSdrEndToEndTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        // tenantContext.SetTenant(Guid.NewGuid());
        return new SGrowingDbContext(options, tenantContext);
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
            email: "sdr-alex@trySGrowing.com",
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

    [Fact]
    public async Task OutboundSdr_OutOfOffice_And_QuotaExhaustion_Should_Be_Handled()
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

        // 1. Quota exhaustion: sender has reached target daily quota
        var exhaustedSender = SenderAccount.Create(
            tenantId: tenantId,
            email: "quota-maxed@trySGrowing.com",
            displayName: "Busy Bot",
            provider: MailboxProvider.GoogleWorkspace,
            targetDailyQuota: 10,
            rampDailyIncrement: 2
        );
        exhaustedSender.SentTodayCount = 10; // Quota completely reached
        db.SenderAccounts.Add(exhaustedSender);

        var contact = Contact.Create(tenantId, "Marcus", "Vance", "marcus@enterprise.co");
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var campaign = OutboundCampaign.Create(tenantId, "Cold Outreach Batch", new List<Guid> { exhaustedSender.Id });
        db.OutboundCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var step1 = CadenceStep.Create(tenantId, campaign.Id, 1, 0, OutboundChannel.Email, "Hello", "Content");
        db.CadenceSteps.Add(step1);

        var enrollment = ProspectEnrollment.Create(tenantId, campaign.Id, contact.Id);
        enrollment.PersonalizedHook = "Great seeing your work!";
        db.ProspectEnrollments.Add(enrollment);
        await db.SaveChangesAsync();

        // Dispatcher runs: since no sender in pool has quota remaining, 0 processed
        var processedCount = await dispatcher.ProcessDueCadencesAsync(tenantId);
        processedCount.Should().Be(0);

        var untouchedEnrollment = await db.ProspectEnrollments.FindAsync(enrollment.Id);
        untouchedEnrollment!.CurrentStepNumber.Should().Be(1);
        untouchedEnrollment.Status.Should().Be(EnrollmentStatus.Active);

        // 2. Out of Office reply handling: delays next step without creating deals
        var conversation = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();

        var oooReply = "Thank you for reaching out. I am currently out of office on annual leave until next Monday.";
        var handoffResult = await handoffService.HandleReplyAsync(tenantId, contact.Id, conversation.Id, oooReply);

        handoffResult.IsSuccess.Should().BeTrue();
        handoffResult.Value.Sentiment.Should().Be(ReplySentiment.OutOfOffice);
        handoffResult.Value.DealCreated.Should().BeFalse();

        var delayedEnrollment = await db.ProspectEnrollments.FindAsync(enrollment.Id);
        delayedEnrollment!.Status.Should().Be(EnrollmentStatus.Active);
        delayedEnrollment.NextRunAtUtc.Should().BeAfter(DateTime.UtcNow.AddDays(4));
    }

    [Fact]
    public async Task OutboundSdr_PausedCampaign_And_NeutralReply_Handling()
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

        // 1. Paused Campaign: Dispatcher skips enrollments in paused campaigns
        var sender = SenderAccount.Create(
            tenantId: tenantId,
            email: "campaign-lead@trySGrowing.com",
            displayName: "Lead SDR",
            provider: MailboxProvider.GoogleWorkspace,
            targetDailyQuota: 50,
            rampDailyIncrement: 10
        );
        db.SenderAccounts.Add(sender);

        var pausedCampaign = OutboundCampaign.Create(tenantId, "Halted Summer Sequence", new List<Guid> { sender.Id });
        pausedCampaign.Status = CampaignStatus.Paused;
        db.OutboundCampaigns.Add(pausedCampaign);

        var contact = Contact.Create(tenantId, "Diana", "Prince", "diana@themyscira.gov");
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var step1 = CadenceStep.Create(tenantId, pausedCampaign.Id, 1, 0, OutboundChannel.Email, "Introduction", "Hello Diana");
        db.CadenceSteps.Add(step1);

        var enrollment = ProspectEnrollment.Create(tenantId, pausedCampaign.Id, contact.Id);
        enrollment.PersonalizedHook = "Admire your global advocacy.";
        db.ProspectEnrollments.Add(enrollment);
        await db.SaveChangesAsync();

        // Dispatcher executes: since campaign is paused, 0 cadences processed
        var processedCount = await dispatcher.ProcessDueCadencesAsync(tenantId);
        processedCount.Should().Be(0);

        var untouchedEnrollment = await db.ProspectEnrollments.FindAsync(enrollment.Id);
        untouchedEnrollment!.CurrentStepNumber.Should().Be(1);
        untouchedEnrollment.Status.Should().Be(EnrollmentStatus.Active);

        // 2. Neutral Reply: no deal created, no opt-out tags added, enrollment unaffected
        var conversation = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();

        var neutralReply = "Thank you for the note. We will keep your information on file.";
        var handoffResult = await handoffService.HandleReplyAsync(tenantId, contact.Id, conversation.Id, neutralReply);

        handoffResult.IsSuccess.Should().BeTrue();
        handoffResult.Value.Sentiment.Should().Be(ReplySentiment.Neutral);
        handoffResult.Value.DealCreated.Should().BeFalse();
        handoffResult.Value.DealId.Should().BeNull();

        var refreshedContact = await db.Contacts.FindAsync(contact.Id);
        refreshedContact!.Tags.Should().NotContain("hot-lead");
        refreshedContact.Tags.Should().NotContain("opted_out");

        var refreshedEnrollment = await db.ProspectEnrollments.FindAsync(enrollment.Id);
        refreshedEnrollment!.Status.Should().Be(EnrollmentStatus.Active);
    }

    [Fact]
    public async Task OutboundSdr_MailboxQuotaExhaustion_And_MultiChannelSequencing()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var outbox = new OutboxService(db);
        var mailboxService = new MailboxService(db);
        var aiSdrService = new AiSdrService();
        var dispatcher = new CadenceDispatcherService(db, mailboxService, aiSdrService, outbox);

        // 1. Create Sender Account with target quota of 1 email/day
        var sender = SenderAccount.Create(
            tenantId: tenantId,
            email: "quota-test@trySGrowing.com",
            displayName: "Test SDR",
            provider: MailboxProvider.GoogleWorkspace,
            targetDailyQuota: 1,
            rampDailyIncrement: 1
        );
        db.SenderAccounts.Add(sender);

        var campaign = OutboundCampaign.Create(tenantId, "Strict Quota Campaign", new List<Guid> { sender.Id });
        db.OutboundCampaigns.Add(campaign);

        var contact = Contact.Create(tenantId, "Arthur", "Curry", "arthur@atlantis.gov");
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var step1 = CadenceStep.Create(tenantId, campaign.Id, 1, 0, OutboundChannel.Email, "Hello", "Content");
        db.CadenceSteps.Add(step1);

        var enrollment = ProspectEnrollment.Create(tenantId, campaign.Id, contact.Id);
        enrollment.PersonalizedHook = "Deep sea logistics.";
        db.ProspectEnrollments.Add(enrollment);
        await db.SaveChangesAsync();

        // 2. Consume sender's entire daily quota
        await mailboxService.RecordSendAsync(sender.Id);
        var reloadedSender = await db.SenderAccounts.FindAsync(sender.Id);
        reloadedSender!.SentTodayCount.Should().Be(1);
        reloadedSender.CanSendToday().Should().BeFalse();

        // 3. Sender pool is now exhausted -> Dispatcher processes 0 cadences
        var nextSender = await mailboxService.GetNextAvailableSenderAsync(tenantId, new[] { sender.Id });
        nextSender.Should().BeNull();

        var processedCount = await dispatcher.ProcessDueCadencesAsync(tenantId);
        processedCount.Should().Be(0);
    }
}
