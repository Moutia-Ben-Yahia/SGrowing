using FluentAssertions;
using MarketingStudio.Api.Data;
using MarketingStudio.Api.Modules.Feedback;
using MarketingStudio.Api.Services;
using MarketingStudio.Core.Entities;
using MarketingStudio.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MarketingStudio.Tests.Feedback;

public class FeedbackTests
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
}
