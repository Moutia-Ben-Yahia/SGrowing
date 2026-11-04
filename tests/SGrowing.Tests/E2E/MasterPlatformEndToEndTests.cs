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
        // tenantContext.SetTenant(Guid.NewGuid());
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

    [Fact]
    public async Task PlatformLifecycle_Unsubscribe_SurveyFailure_And_RagUnmatched_Scenarios()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var notifier = new Mock<IRealtimeNotifier>();

        // 1. Knowledge Base RAG: Empty prompt and unindexed query fallback
        var ragService = new HelpCenterRagService(db);
        var emptyResult = await ragService.GenerateAiDeflectionReplyAsync(tenantId, "");
        emptyResult.Deflected.Should().BeFalse();
        emptyResult.ConfidenceScore.Should().Be(0);

        var unmatchedResult = await ragService.GenerateAiDeflectionReplyAsync(tenantId, "How do I configure quantum computing teleportation protocol?");
        unmatchedResult.Deflected.Should().BeFalse();
        unmatchedResult.ConfidenceScore.Should().Be(35);

        // 2. Feedback Service: Submitting response to non-existent survey form fails cleanly
        var feedbackService = new FeedbackService(db, notifier.Object);
        var badSurveyResult = await feedbackService.SubmitFeedbackAsync(tenantId, Guid.NewGuid(), Guid.NewGuid(), 5, "Great app!");
        badSurveyResult.IsSuccess.Should().BeFalse();
        badSurveyResult.Error.Should().Be("Survey form not found.");

        // 3. Outbound Handoff: Unsubscribe opt-out behavior
        var sentimentClassifier = new ReplySentimentClassifier();
        var handoffService = new ProspectReplyHandoffService(db, sentimentClassifier, notifier.Object);

        var contact = Contact.Create(tenantId, "Aaron", "Burr", "aaron@duel.org");
        db.Contacts.Add(contact);

        var campaign = OutboundCampaign.Create(tenantId, "General Pitch", new List<Guid> { Guid.NewGuid() });
        db.OutboundCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var enrollment = ProspectEnrollment.Create(tenantId, campaign.Id, contact.Id);
        db.ProspectEnrollments.Add(enrollment);

        var conv = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        var handoffRes = await handoffService.HandleReplyAsync(tenantId, contact.Id, conv.Id, "Please unsubscribe me from this mailing list and remove my email.");
        handoffRes.IsSuccess.Should().BeTrue();
        handoffRes.Value.Sentiment.Should().Be(ReplySentiment.Unsubscribe);
        handoffRes.Value.DealCreated.Should().BeFalse();

        var updatedContact = await db.Contacts.FindAsync(contact.Id);
        updatedContact!.Tags.Should().Contain("opted_out");
        updatedContact.LifecycleStage.Should().Be(LifecycleStage.Churned);

        var updatedEnrollment = await db.ProspectEnrollments.FindAsync(enrollment.Id);
        updatedEnrollment!.Status.Should().Be(EnrollmentStatus.Unsubscribed);
    }

    [Fact]
    public async Task PlatformLifecycle_PositiveCsat_And_DelayedJourneySteps()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var notifier = new Mock<IRealtimeNotifier>();
        var outbox = new OutboxService(db);

        // 1. Feedback Service: Positive 5-star CSAT does not flag customer as at-risk
        var feedbackService = new FeedbackService(db, notifier.Object);
        var surveyForm = SurveyForm.Create(
            tenantId,
            "Quarterly Customer Delight",
            SurveyType.Csat,
            "How satisfied are you with our service?");
        db.SurveyForms.Add(surveyForm);

        var customer = Contact.Create(tenantId, "Thomas", "Jefferson", "thomas@monticello.org");
        db.Contacts.Add(customer);
        await db.SaveChangesAsync();

        var feedbackRes = await feedbackService.SubmitFeedbackAsync(
            tenantId,
            surveyForm.Id,
            customer.Id,
            score: 5,
            feedbackText: "Outstanding support and fast omnichannel responses!");

        feedbackRes.IsSuccess.Should().BeTrue();
        feedbackRes.Value.Sentiment.Should().Be(FeedbackSentiment.Positive);
        feedbackRes.Value.Score.Should().Be(5);

        var refreshedCustomer = await db.Contacts.FindAsync(customer.Id);
        refreshedCustomer!.Tags.Should().NotContain("at-risk");

        // 2. Journey Execution Service: Steps with delay do not process before elapsed time
        var journeyService = new JourneyExecutionService(db, outbox);
        var nurtureJourney = CustomerJourney.Create(tenantId, "Post-Purchase Nurture", "order.completed");
        db.CustomerJourneys.Add(nurtureJourney);
        await db.SaveChangesAsync();

        var step1 = JourneyStep.Create(
            tenantId,
            nurtureJourney.Id,
            stepOrder: 1,
            stepType: JourneyStepType.SendEmail,
            bodyTemplate: "Thanks for your order {{first_name}}!",
            subjectTemplate: "Order Confirmation",
            delaySeconds: 7200); // 2 hours delay before next step

        var step2 = JourneyStep.Create(
            tenantId,
            nurtureJourney.Id,
            stepOrder: 2,
            stepType: JourneyStepType.SendEmail,
            bodyTemplate: "How is your product working out {{first_name}}?",
            subjectTemplate: "Checking In",
            delaySeconds: 0);

        db.JourneySteps.AddRange(step1, step2);
        await db.SaveChangesAsync();

        // Trigger journey
        var evtRes = await journeyService.IngestEventAsync(tenantId, customer.Id, "order.completed");
        evtRes.IsSuccess.Should().BeTrue();

        // Process step 1 (immediate execution)
        var firstRunCount = await journeyService.ProcessDueStepsAsync(tenantId);
        firstRunCount.Should().Be(1);

        // Verification: Step advanced to step 2 with NextExecutionAtUtc 2 hours in future
        var enrollment = await db.JourneyEnrollments.FirstOrDefaultAsync(e => e.JourneyId == nurtureJourney.Id);
        enrollment.Should().NotBeNull();
        enrollment!.CurrentStepOrder.Should().Be(2);
        enrollment.Status.Should().Be(JourneyEnrollmentStatus.Active);
        enrollment.NextExecutionAtUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(90));

        // Attempting to process due steps immediately processes 0 because delay hasn't elapsed
        var secondRunCount = await journeyService.ProcessDueStepsAsync(tenantId);
        secondRunCount.Should().Be(0);
    }

    [Fact]
    public async Task MasterPlatform_HelpCenterRag_DeflectionMatching_And_ZeroKeywordFallback()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var ragService = new HelpCenterRagService(db);

        // 1. Seed Published Help Center Article
        var article = HelpArticle.Create(
            tenantId,
            Guid.NewGuid(),
            "Drop-and-Swap Courier Exchange Policy",
            "drop-and-swap-exchange-policy",
            "Customers can inspect footwear size at doorstep and execute immediate size swap via our courier network without return fees.",
            isPublished: true
        );
        db.HelpArticles.Add(article);
        await db.SaveChangesAsync();

        // 2. High-intent customer query deflects successfully with matched article and high confidence
        var deflection = await ragService.GenerateAiDeflectionReplyAsync(tenantId, "How does doorstep drop-and-swap size exchange work?");
        deflection.Deflected.Should().BeTrue();
        deflection.ArticleTitle.Should().Be("Drop-and-Swap Courier Exchange Policy");
        deflection.ArticleSlug.Should().Be("drop-and-swap-exchange-policy");
        deflection.ConfidenceScore.Should().BeGreaterThan(40);

        // 3. Query with only stop-words falls back to human escalation with low confidence
        var fallbackDeflection = await ragService.GenerateAiDeflectionReplyAsync(tenantId, "can i please help you with what is");
        fallbackDeflection.Deflected.Should().BeFalse();
        fallbackDeflection.Answer.Should().Contain("human support specialist");
        fallbackDeflection.ConfidenceScore.Should().Be(20);

        // 4. Empty query returns safe early message
        var emptyDeflection = await ragService.GenerateAiDeflectionReplyAsync(tenantId, "   ");
        emptyDeflection.Deflected.Should().BeFalse();
        emptyDeflection.ConfidenceScore.Should().Be(0);
    }
}
