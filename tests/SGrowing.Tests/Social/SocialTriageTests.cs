using SGrowing.Api.Modules.Social.AutoTriage;
using Xunit;

namespace SGrowing.Tests.Social;

public sealed class SocialTriageTests
{
    private readonly SocialTriageEngine _engine = new();

    [Fact]
    public async Task IngestAndTriageMessage_DetectsSevereHazardAndEscalates()
    {
        var request = new IngestSocialMessageRequest(
            Channel: "WhatsApp",
            SenderHandle: "+447911123456",
            SenderName: "E-Commerce Director",
            MessageText: "Our primary payment gateway is broken and returning 500 error! Fix this now or we cancel our annual subscription!"
        );

        var item = await _engine.IngestAndTriageMessageAsync(request);

        Assert.NotNull(item);
        Assert.Equal("SevereHazard", item.Assessment.Sentiment);
        Assert.True(item.Assessment.RequiresImmediateEscalation);
        Assert.True(item.Assessment.UrgencyScore >= 90);
        Assert.Equal("ExecutiveEscalationLead", item.AssignedAgentRole);
        Assert.True(item.SlaDeadlineUtc <= DateTime.UtcNow.AddMinutes(20));
        Assert.Contains("apologize", item.DraftReply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IngestAndTriageMessage_DetectsSalesInquiryAndRoutesToAe()
    {
        var request = new IngestSocialMessageRequest(
            Channel: "LinkedIn",
            SenderHandle: "linkedin.com/in/director-tech",
            SenderName: "Marcus Vance",
            MessageText: "We want to purchase 250 enterprise seats. Can you send a quote and schedule a demo?"
        );

        var item = await _engine.IngestAndTriageMessageAsync(request);

        Assert.NotNull(item);
        Assert.Equal("Positive", item.Assessment.Sentiment);
        Assert.False(item.Assessment.RequiresImmediateEscalation);
        Assert.Equal("SalesInquiry", item.Assessment.IntentCategory);
        Assert.Equal("EnterpriseAccountExecutive", item.AssignedAgentRole);
        Assert.Contains("demonstration", item.DraftReply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApproveAndSendReply_UpdatesQueueItemStatus()
    {
        var queue = await _engine.GetQueueItemsAsync();
        var pending = queue.First();

        var approved = await _engine.ApproveAndSendReplyAsync(
            pending.QueueId,
            new ApproveReplyRequest("Approved: Our engineering team has already mitigated the issue.")
        );

        Assert.NotNull(approved);
        Assert.Equal("ApprovedAndSent", approved.Status);
        Assert.Equal("Approved: Our engineering team has already mitigated the issue.", approved.DraftReply);
    }

    [Fact]
    public async Task IngestAndTriageMessage_NullRequest_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _engine.IngestAndTriageMessageAsync(null!));
    }

    [Fact]
    public async Task ApproveAndSendReply_WhenQueueIdDoesNotExist_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _engine.ApproveAndSendReplyAsync("non_existent_queue_id", new ApproveReplyRequest("Reply")));
    }

    [Fact]
    public async Task GetQueueItems_FilterByChannelAndStatus_ReturnsFilteredSubsets()
    {
        var waItems = await _engine.GetQueueItemsAsync(channel: "WhatsApp");
        Assert.All(waItems, i => Assert.Equal("WhatsApp", i.Message.Channel));

        var liItems = await _engine.GetQueueItemsAsync(channel: "LinkedIn");
        Assert.All(liItems, i => Assert.Equal("LinkedIn", i.Message.Channel));

        var pendingItems = await _engine.GetQueueItemsAsync(status: "PendingReview");
        Assert.All(pendingItems, i => Assert.Equal("PendingReview", i.Status));
    }

    [Fact]
    public async Task GetAnalytics_ReturnsAggregatesAndChannelBreakdown()
    {
        var analytics = await _engine.GetAnalyticsAsync();

        Assert.NotNull(analytics);
        Assert.True(analytics.TotalTriaged >= 2);
        Assert.True(analytics.SlaCompliancePercent > 90.0);
        Assert.Contains("WhatsApp", analytics.ChannelBreakdown.Keys);
        Assert.Contains("LinkedIn", analytics.ChannelBreakdown.Keys);
    }

    [Fact]
    public async Task IngestAndTriageMessage_WhenGeneralInquiry_RoutesToSupportSpecialistWith120MinuteSla()
    {
        var request = new IngestSocialMessageRequest(
            Channel: "Instagram",
            SenderHandle: "@growth_user",
            SenderName: "Amira Ben Salem",
            MessageText: "Hello, where can I find instructions to update our team billing email address?"
        );

        var item = await _engine.IngestAndTriageMessageAsync(request);

        Assert.NotNull(item);
        Assert.Equal("Neutral", item.Assessment.Sentiment);
        Assert.Equal("GeneralSupport", item.Assessment.IntentCategory);
        Assert.False(item.Assessment.RequiresImmediateEscalation);
        Assert.Equal("SupportSpecialist", item.AssignedAgentRole);
        Assert.True(item.SlaDeadlineUtc <= DateTime.UtcNow.AddMinutes(125));
        Assert.Contains("thank you for reaching out", item.DraftReply, StringComparison.OrdinalIgnoreCase);
    }
}
