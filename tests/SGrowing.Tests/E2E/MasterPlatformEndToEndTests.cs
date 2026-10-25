using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Api.Modules.Feedback;
using SGrowing.Api.Modules.Journeys;
using SGrowing.Api.Modules.KnowledgeBase;
using SGrowing.Api.Modules.Outbound;
using SGrowing.Api.Services;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace SGrowing.Tests.E2E;

public class MasterPlatformEndToEndTests
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
    public async Task CompleteOmnicommunicationLifecycle_AcrossAllPlatformHorizons()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var outbox = new OutboxService(db);
        var notifier = new Mock<IRealtimeNotifier>();

        // Horizon 1 & 2 Services (Inbox, CRM, AI SDR)
        var mailboxService = new MailboxService(db);
        var aiSdrService = new AiSdrService();
        var sentimentClassifier = new ReplySentimentClassifier();
        var handoffService = new ProspectReplyHandoffService(db, sentimentClassifier, notifier.Object);
        var cadenceDispatcher = new CadenceDispatcherService(db, mailboxService, aiSdrService, outbox);

        // Horizon 3 Services (Journeys)
        var journeyService = new JourneyExecutionService(db, outbox);

        // Horizon 4 Services (Feedback & CSAT)
        var feedbackService = new FeedbackService(db, notifier.Object);

        // Horizon 5 Services (Knowledge Base & AI RAG)
        var ragService = new HelpCenterRagService(db);

        // =========================================================================
        // STEP 1: Customer Contact Creation & Behavioral Event Journey Ingestion
        // =========================================================================
        var contact = Contact.Create(tenantId, "Alexander", "Hamilton", "alexander@treasury.gov");
        db.Contacts.Add(contact);

        var onboardingJourney = CustomerJourney.Create(tenantId, "Automated Onboarding Journey", "user.signed_up");
        db.CustomerJourneys.Add(onboardingJourney);
        await db.SaveChangesAsync();

        var journeyStep1 = JourneyStep.Create(
            tenantId,
            onboardingJourney.Id,
            stepOrder: 1,
            stepType: JourneyStepType.SendEmail,
            bodyTemplate: "Hi {{first_name}}, welcome to our platform! Your account is ready.",
            subjectTemplate: "Welcome to SGrowing",
            delaySeconds: 0
        );
        db.JourneySteps.Add(journeyStep1);
        await db.SaveChangesAsync();

        // Customer signs up -> Ingest event
        var eventResult = await journeyService.IngestEventAsync(tenantId, contact.Id, "user.signed_up", "{\"tier\": \"enterprise\"}");
        eventResult.IsSuccess.Should().BeTrue();

        // Verify customer was enrolled into the journey
        var enrollment = await db.JourneyEnrollments.FirstOrDefaultAsync(e => e.ContactId == contact.Id && e.JourneyId == onboardingJourney.Id);
        enrollment.Should().NotBeNull();
        enrollment!.Status.Should().Be(JourneyEnrollmentStatus.Active);

        // Execute due journey steps -> Dispatches welcome email via Outbox
        var processedJourneys = await journeyService.ProcessDueStepsAsync(tenantId);
        processedJourneys.Should().Be(1);

        var outboxMessages = await db.OutboxMessages.ToListAsync();
        outboxMessages.Should().Contain(m => m.EventType == "Journey.Email.Send" && m.Payload.Contains("Alexander"));

        // =========================================================================
        // STEP 2: AI SDR Outbound Cadence & Positive Reply Handoff to CRM
        // =========================================================================
        var sender = SenderAccount.Create(tenantId, "sdr@trySGrowing.com", "SDR Alex", MailboxProvider.GoogleWorkspace, 40, 3);
        db.SenderAccounts.Add(sender);
        var campaign = OutboundCampaign.Create(tenantId, "Enterprise Expansion Cadence", new List<Guid> { sender.Id });
        db.OutboundCampaigns.Add(campaign);

        var cadenceStep = CadenceStep.Create(tenantId, campaign.Id, 1, 0, OutboundChannel.Email, "Quick question", "Hi {{first_name}}, {{icebreaker}}");
        db.CadenceSteps.Add(cadenceStep);
        await db.SaveChangesAsync();

        var sdrEnrollment = ProspectEnrollment.Create(tenantId, campaign.Id, contact.Id);
        var hookOutput = await aiSdrService.GeneratePersonalizationAsync(new SdrPersonalizationInput("Alexander", "Hamilton", "Treasury", "Public Sector"));
        sdrEnrollment.PersonalizedHook = hookOutput.Icebreaker;
        db.ProspectEnrollments.Add(sdrEnrollment);
        await db.SaveChangesAsync();

        // Dispatch outbound email
        var dispatchedCadences = await cadenceDispatcher.ProcessDueCadencesAsync(tenantId);
        dispatchedCadences.Should().Be(1);

        // Prospect replies enthusiastically
        var conv = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        var handoffRes = await handoffService.HandleReplyAsync(tenantId, contact.Id, conv.Id, "We loved this! Can we schedule a demo call with our VP?");
        handoffRes.IsSuccess.Should().BeTrue();
        handoffRes.Value.Sentiment.Should().Be(ReplySentiment.Interested);
        handoffRes.Value.DealCreated.Should().BeTrue();

        // Verify Deal created in CRM
        var deal = await db.Deals.FirstOrDefaultAsync(d => d.ContactId == contact.Id);
        deal.Should().NotBeNull();
        deal!.Amount.Should().Be(5000m);
        deal.Stage.Should().Be(DealStage.Qualified);

        // =========================================================================
        // STEP 3: Customer Feedback Sentinel (Negative CSAT Alerting)
        // =========================================================================
        var csatForm = SurveyForm.Create(tenantId, "Post-Resolution Survey", SurveyType.Csat, "Rate your experience");
        db.SurveyForms.Add(csatForm);
        await db.SaveChangesAsync();

        // Customer experiences a snag and leaves low score
        var feedbackRes = await feedbackService.SubmitFeedbackAsync(tenantId, csatForm.Id, contact.Id, 1, "The server crashed when uploading our CSV and it was terrible.");
        feedbackRes.IsSuccess.Should().BeTrue();
        feedbackRes.Value.Sentiment.Should().Be(FeedbackSentiment.Negative);

        // Verify Contact is tagged as at-risk
        var updatedContact = await db.Contacts.FindAsync(contact.Id);
        updatedContact!.Tags.Should().Contain("at-risk");

        // =========================================================================
        // STEP 4: Knowledge Base AI RAG Zero-Wait Deflection
        // =========================================================================
        var kbCategory = HelpCategory.Create(tenantId, "Channel Setup", "channels");
        db.HelpCategories.Add(kbCategory);

        var article = HelpArticle.Create(
            tenantId,
            kbCategory.Id,
            "Connecting WhatsApp Meta Cloud API",
            "whatsapp-meta-setup",
            "To connect WhatsApp Meta Cloud API: Go to Settings > Channels > WhatsApp, enter your Meta System User Access Token and Phone Number ID, and click Save."
        );
        db.HelpArticles.Add(article);
        await db.SaveChangesAsync();

        // Customer asks how to set up WhatsApp
        var ragResult = await ragService.GenerateAiDeflectionReplyAsync(tenantId, "How do I connect WhatsApp Meta Cloud API in settings?");
        ragResult.Deflected.Should().BeTrue();
        ragResult.ConfidenceScore.Should().BeGreaterThanOrEqualTo(80);
        ragResult.Answer.Should().Contain("Meta System User Access Token");
        ragResult.ArticleSlug.Should().Be("whatsapp-meta-setup");

        // Verify article view count incremented
        var updatedArticle = await db.HelpArticles.FindAsync(article.Id);
        updatedArticle!.ViewCount.Should().Be(1);
    }
}
