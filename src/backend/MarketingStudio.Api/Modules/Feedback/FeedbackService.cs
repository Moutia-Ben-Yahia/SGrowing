using MarketingStudio.Api.Data;
using MarketingStudio.Api.Services;
using MarketingStudio.Core.Common;
using MarketingStudio.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarketingStudio.Api.Modules.Feedback;

public class FeedbackService
{
    private readonly MarketingStudioDbContext _db;
    private readonly IRealtimeNotifier _notifier;

    public FeedbackService(MarketingStudioDbContext db, IRealtimeNotifier notifier)
    {
        _db = db;
        _notifier = notifier;
    }

    public async Task<Result<SurveyResponse>> SubmitFeedbackAsync(
        Guid tenantId,
        Guid surveyFormId,
        Guid contactId,
        int score,
        string? feedbackText = null,
        CancellationToken cancellationToken = default)
    {
        var form = await _db.SurveyForms.FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == surveyFormId, cancellationToken);
        if (form == null)
            return Result<SurveyResponse>.Failure("Survey form not found.");

        var sentiment = CalculateSentiment(form.Type, score, feedbackText);

        var response = SurveyResponse.Create(tenantId, surveyFormId, contactId, score, feedbackText, sentiment);
        _db.SurveyResponses.Add(response);

        // Closed-Loop Retention Safeguard: Poor feedback triggers contact flagging & alerts
        if (sentiment == FeedbackSentiment.Negative)
        {
            var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == contactId, cancellationToken);
            if (contact != null)
            {
                if (!contact.Tags.Contains("at-risk"))
                {
                    contact.Tags.Add("at-risk");
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<SurveyResponse>.Success(response);
    }

    private FeedbackSentiment CalculateSentiment(SurveyType type, int score, string? feedbackText)
    {
        // Negative text overrides
        if (!string.IsNullOrWhiteSpace(feedbackText))
        {
            var text = feedbackText.ToLowerInvariant();
            if (text.Contains("broken") || text.Contains("terrible") || text.Contains("horrible") || text.Contains("cancel") || text.Contains("worst"))
            {
                return FeedbackSentiment.Negative;
            }
        }

        if (type == SurveyType.Nps)
        {
            if (score >= 9) return FeedbackSentiment.Positive;
            if (score >= 7) return FeedbackSentiment.Neutral;
            return FeedbackSentiment.Negative;
        }

        // Standard CSAT (1-5)
        if (score >= 4) return FeedbackSentiment.Positive;
        if (score == 3) return FeedbackSentiment.Neutral;
        return FeedbackSentiment.Negative;
    }
}
