using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Feedback;
using SGrowing.Api.Services;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace SGrowing.Tests.Feedback;

public class FeedbackTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        return new SGrowingDbContext(options, tenantContext);
    }

    [Fact]
    public async Task SubmitFeedback_PositiveScore_ReturnsPositiveSentiment()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var service = new FeedbackService(db, notifier.Object);
        var tenantId = Guid.NewGuid();

        var form = SurveyForm.Create(tenantId, "Post-Support CSAT", SurveyType.Csat, "How satisfied were you with our support?");
        db.SurveyForms.Add(form);
        var contact = Contact.Create(tenantId, "Grace", "Hopper", "grace@navy.mil");
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var res = await service.SubmitFeedbackAsync(tenantId, form.Id, contact.Id, 5, "Support agent resolved my issue within 2 minutes!");

        res.IsSuccess.Should().BeTrue();
        res.Value.Sentiment.Should().Be(FeedbackSentiment.Positive);
        res.Value.Score.Should().Be(5);

        var savedContact = await db.Contacts.FindAsync(contact.Id);
        savedContact!.Tags.Should().NotContain("at-risk");
    }

    [Fact]
    public async Task SubmitFeedback_LowScore_TagsContactAsAtRisk()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var service = new FeedbackService(db, notifier.Object);
        var tenantId = Guid.NewGuid();

        var form = SurveyForm.Create(tenantId, "Product NPS", SurveyType.Nps, "How likely are you to recommend us?", scaleMax: 10);
        db.SurveyForms.Add(form);
        var contact = Contact.Create(tenantId, "Ada", "Lovelace", "ada@computing.org");
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        // Score 4 is Detractor / Negative
        var res = await service.SubmitFeedbackAsync(tenantId, form.Id, contact.Id, 4, "Too slow, feature was broken and we want to cancel");

        res.IsSuccess.Should().BeTrue();
        res.Value.Sentiment.Should().Be(FeedbackSentiment.Negative);

        var savedContact = await db.Contacts.FindAsync(contact.Id);
        savedContact!.Tags.Should().Contain("at-risk");
    }

    [Fact]
    public async Task SubmitFeedback_ReturnsFailure_WhenSurveyFormDoesNotExist()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var service = new FeedbackService(db, notifier.Object);
        var tenantId = Guid.NewGuid();

        var res = await service.SubmitFeedbackAsync(tenantId, Guid.NewGuid(), Guid.NewGuid(), 5, "Great app");

        res.IsSuccess.Should().BeFalse();
        res.Error.Should().Be("Survey form not found.");
    }

    [Fact]
    public async Task SubmitFeedback_NpsScoresSevenAndEight_EvaluateToNeutralSentiment()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var service = new FeedbackService(db, notifier.Object);
        var tenantId = Guid.NewGuid();

        var form = SurveyForm.Create(tenantId, "NPS Evaluation", SurveyType.Nps, "Recommend?", scaleMax: 10);
        db.SurveyForms.Add(form);
        var contact = Contact.Create(tenantId, "Alan", "Turing", "alan@turing.ac.uk");
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var res7 = await service.SubmitFeedbackAsync(tenantId, form.Id, contact.Id, 7, "Standard average performance");
        var res8 = await service.SubmitFeedbackAsync(tenantId, form.Id, contact.Id, 8, "Good enough for now");

        res7.Value.Sentiment.Should().Be(FeedbackSentiment.Neutral);
        res8.Value.Sentiment.Should().Be(FeedbackSentiment.Neutral);
    }

    [Fact]
    public async Task SubmitFeedback_NegativeKeywords_OverrideHighNumericScoreToNegative()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var service = new FeedbackService(db, notifier.Object);
        var tenantId = Guid.NewGuid();

        var form = SurveyForm.Create(tenantId, "CSAT Review", SurveyType.Csat, "Satisfaction?");
        db.SurveyForms.Add(form);
        var contact = Contact.Create(tenantId, "Margaret", "Hamilton", "margaret@nasa.gov");
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        // High score 5 but text contains "terrible" and "cancel"
        var res = await service.SubmitFeedbackAsync(tenantId, form.Id, contact.Id, 5, "I mistakenly clicked 5, this was terrible and we will cancel!");

        res.Value.Sentiment.Should().Be(FeedbackSentiment.Negative);

        var savedContact = await db.Contacts.FindAsync(contact.Id);
        savedContact!.Tags.Should().Contain("at-risk");
    }

    [Fact]
    public async Task SubmitFeedback_AtRiskTagging_IsIdempotentOnRepeatedNegativeFeedback()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var service = new FeedbackService(db, notifier.Object);
        var tenantId = Guid.NewGuid();

        var form = SurveyForm.Create(tenantId, "CSAT Repeat", SurveyType.Csat, "Satisfaction?");
        db.SurveyForms.Add(form);
        var contact = Contact.Create(tenantId, "Claude", "Shannon", "claude@bell.com");
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        // Submit twice with negative feedback
        await service.SubmitFeedbackAsync(tenantId, form.Id, contact.Id, 1, "horrible latency");
        await service.SubmitFeedbackAsync(tenantId, form.Id, contact.Id, 1, "worst experience");

        var savedContact = await db.Contacts.FindAsync(contact.Id);
        savedContact!.Tags.Count(t => t == "at-risk").Should().Be(1);
    }
}
