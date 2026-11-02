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
}
