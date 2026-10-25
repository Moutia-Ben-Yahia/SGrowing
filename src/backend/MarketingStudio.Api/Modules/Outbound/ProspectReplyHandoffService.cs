using MarketingStudio.Api.Data;
using MarketingStudio.Api.Services;
using MarketingStudio.Core.Common;
using MarketingStudio.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarketingStudio.Api.Modules.Outbound;

public record ReplyHandoffResult(ReplySentiment Sentiment, bool DealCreated, Guid? DealId);

public class ProspectReplyHandoffService
{
    private readonly MarketingStudioDbContext _db;
    private readonly ReplySentimentClassifier _classifier;
    private readonly IRealtimeNotifier _notifier;

    public ProspectReplyHandoffService(
        MarketingStudioDbContext db,
        ReplySentimentClassifier classifier,
        IRealtimeNotifier notifier)
    {
        _db = db;
        _classifier = classifier;
        _notifier = notifier;
    }

    public async Task<Result<ReplyHandoffResult>> HandleReplyAsync(
        Guid tenantId,
        Guid contactId,
        Guid conversationId,
        string messageContent,
        CancellationToken cancellationToken = default)
    {
        var sentiment = _classifier.Classify(messageContent);

        // Find active outbound enrollments for this contact
        var enrollments = await _db.ProspectEnrollments
            .Where(e => e.TenantId == tenantId && e.ContactId == contactId && e.Status == EnrollmentStatus.Active)
            .ToListAsync(cancellationToken);

        var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == contactId, cancellationToken);
        var conversation = await _db.Conversations.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == conversationId, cancellationToken);

        bool dealCreated = false;
        Guid? dealId = null;

        switch (sentiment)
        {
            case ReplySentiment.Interested:
                foreach (var enrollment in enrollments)
                {
                    enrollment.MarkReplied();
                }

                // 1. Create a Deal in CRM
                var dealTitle = $"{contact?.FirstName ?? "Prospect"} - Outbound Qualified Opportunity";
                var deal = Deal.Create(tenantId, contactId, dealTitle, 5000m, DealStage.Qualified);
                _db.Deals.Add(deal);
                dealCreated = true;
                dealId = deal.Id;

                // 2. Elevate Conversation Priority in Shared Inbox
                if (conversation != null)
                {
                    conversation.Priority = ConversationPriority.High;
                    conversation.Status = ConversationStatus.Open;
                }

                if (contact != null && !contact.Tags.Contains("hot-lead"))
                {
                    contact.Tags.Add("hot-lead");
                }
                break;

            case ReplySentiment.Unsubscribe:
                foreach (var enrollment in enrollments)
                {
                    enrollment.MarkUnsubscribed();
                }

                if (contact != null && !contact.Tags.Contains("opted_out"))
                {
                    contact.Tags.Add("opted_out");
                    contact.LifecycleStage = LifecycleStage.Churned;
                }
                break;

            case ReplySentiment.OutOfOffice:
                foreach (var enrollment in enrollments)
                {
                    // Delay next step by 5 days
                    enrollment.NextRunAtUtc = DateTime.UtcNow.AddDays(5);
                }
                break;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result<ReplyHandoffResult>.Success(new ReplyHandoffResult(sentiment, dealCreated, dealId));
    }
}
