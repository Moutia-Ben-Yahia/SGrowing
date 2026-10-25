using FluentAssertions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Outbound;
using SGrowing.Api.Services;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace SGrowing.Tests.Outbound;

public class SentimentAndHandoffTests
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

    [Theory]
    [InlineData("I would love to see a demo, can you send times?", ReplySentiment.Interested)]
    [InlineData("Please unsubscribe me and remove me from your list.", ReplySentiment.Unsubscribe)]
    [InlineData("I am currently out of the office on annual leave.", ReplySentiment.OutOfOffice)]
    public void ReplySentimentClassifier_ShouldAccuratelyClassifyText(string text, ReplySentiment expected)
    {
        var classifier = new ReplySentimentClassifier();
        var result = classifier.Classify(text);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task ProcessProspectReply_WhenInterested_ShouldCreateDealAndHandoffToInbox()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var tenantId = Guid.NewGuid();

        var campaign = OutboundCampaign.Create(tenantId, "Q4 SaaS Outbound", new List<Guid> { Guid.NewGuid() });
        db.OutboundCampaigns.Add(campaign);
        var contact = Contact.Create(tenantId, "Clark", "Kent", "clark@dailyplanet.com");
        db.Contacts.Add(contact);
        var conv = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        db.Conversations.Add(conv);

        var enrollment = ProspectEnrollment.Create(tenantId, campaign.Id, contact.Id);
        db.ProspectEnrollments.Add(enrollment);
        await db.SaveChangesAsync();

        var handoffService = new ProspectReplyHandoffService(db, new ReplySentimentClassifier(), notifier.Object);
        var result = await handoffService.HandleReplyAsync(tenantId, contact.Id, conv.Id, "Yes, we are interested in scheduling a quick call!");

        result.IsSuccess.Should().BeTrue();
        result.Value.Sentiment.Should().Be(ReplySentiment.Interested);
        result.Value.DealCreated.Should().BeTrue();

        // 1. Enrollment status must be Replied (terminates follow-ups)
        enrollment.Status.Should().Be(EnrollmentStatus.Replied);
        enrollment.NextRunAtUtc.Should().BeNull();

        // 2. Deal should exist in CRM with Qualified stage
        var deal = await db.Deals.FirstOrDefaultAsync(d => d.ContactId == contact.Id);
        deal.Should().NotBeNull();
        deal!.Stage.Should().Be(DealStage.Qualified);
        deal.Amount.Should().Be(5000m);

        // 3. Conversation priority should be elevated to High
        conv.Priority.Should().Be(ConversationPriority.High);
    }

    [Fact]
    public async Task ProcessProspectReply_WhenUnsubscribe_ShouldStopCadenceAndTagContact()
    {
        var db = CreateDbContext();
        var notifier = new Mock<IRealtimeNotifier>();
        var tenantId = Guid.NewGuid();

        var campaign = OutboundCampaign.Create(tenantId, "Q4 SaaS Outbound", new List<Guid> { Guid.NewGuid() });
        db.OutboundCampaigns.Add(campaign);
        var contact = Contact.Create(tenantId, "Lex", "Luthor", "lex@lexcorp.com");
        db.Contacts.Add(contact);
        var conv = Conversation.Create(tenantId, Guid.NewGuid(), contact.Id);
        db.Conversations.Add(conv);

        var enrollment = ProspectEnrollment.Create(tenantId, campaign.Id, contact.Id);
        db.ProspectEnrollments.Add(enrollment);
        await db.SaveChangesAsync();

        var handoffService = new ProspectReplyHandoffService(db, new ReplySentimentClassifier(), notifier.Object);
        var result = await handoffService.HandleReplyAsync(tenantId, contact.Id, conv.Id, "Stop emailing me, unsubscribe!");

        result.IsSuccess.Should().BeTrue();
        result.Value.Sentiment.Should().Be(ReplySentiment.Unsubscribe);
        result.Value.DealCreated.Should().BeFalse();

        enrollment.Status.Should().Be(EnrollmentStatus.Unsubscribed);
        contact.Tags.Should().Contain("opted_out");
    }
}
