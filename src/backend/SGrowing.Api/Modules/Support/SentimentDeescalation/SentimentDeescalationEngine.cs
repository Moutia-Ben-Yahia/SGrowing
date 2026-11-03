using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Support.SentimentDeescalation;

public interface ISentimentDeescalationEngine
{
    CsatSubmission SubmitCsat(SubmitCsatRequest request);
    List<CsatSubmission> GetDeescalationAlerts();
    CsatSubmission? IssueGoodwill(IssueGoodwillRequest request);
}

public class SentimentDeescalationEngine : ISentimentDeescalationEngine
{
    private readonly ConcurrentDictionary<string, CsatSubmission> _submissions = new(StringComparer.OrdinalIgnoreCase);

    public SentimentDeescalationEngine()
    {
        // Seed initial CSAT submissions
        SubmitCsat(new SubmitCsatRequest(
            "cust_fintech_88",
            "cto@fintechscale.io",
            2,
            "The API rate limit hit during our quarterly batch sync. Unacceptable downtime for enterprise pricing."
        ));

        SubmitCsat(new SubmitCsatRequest(
            "cust_health_99",
            "vp@healthcloud.org",
            5,
            "Outstanding support response! The agent swarm identified and patched our webhook payload within 4 minutes."
        ));
    }

    public CsatSubmission SubmitCsat(SubmitCsatRequest request)
    {
        var rating = Math.Clamp(request.RatingScore, 1, 5);
        var text = (request.FeedbackText ?? string.Empty).ToLowerInvariant();

        var sentiment = rating switch
        {
            5 => SentimentLevel.Delighted,
            4 => SentimentLevel.Satisfied,
            3 => SentimentLevel.PassiveNeutral,
            _ => SentimentLevel.FrustratedAtRisk
        };

        if (text.Contains("unacceptable") || text.Contains("canceling") || text.Contains("terrible") || text.Contains("escalate"))
        {
            sentiment = SentimentLevel.FrustratedAtRisk;
        }

        var urgent = rating <= 2 || sentiment == SentimentLevel.FrustratedAtRisk;
        var submissionId = $"csat_{DateTime.UtcNow.Ticks % 1000000}";

        var submission = new CsatSubmission(
            submissionId,
            request.CustomerId,
            request.CustomerEmail,
            rating,
            request.FeedbackText ?? string.Empty,
            sentiment,
            urgent,
            AppliedGoodwill: null,
            DateTime.UtcNow
        );

        _submissions[submissionId] = submission;
        return submission;
    }

    public List<CsatSubmission> GetDeescalationAlerts()
    {
        return _submissions.Values
            .Where(s => s.RequiresUrgentTriage)
            .OrderByDescending(s => s.SubmittedAtUtc)
            .ToList();
    }

    public CsatSubmission? IssueGoodwill(IssueGoodwillRequest request)
    {
        if (!_submissions.TryGetValue(request.SubmissionId, out var existing))
        {
            return null;
        }

        var goodwill = new GoodwillCompensation(
            $"comp_{DateTime.UtcNow.Ticks % 10000}",
            request.CreditDollars,
            request.BenefitDescription,
            DateTime.UtcNow
        );

        var updated = existing with
        {
            AppliedGoodwill = goodwill,
            RequiresUrgentTriage = false // Resolved
        };

        _submissions[request.SubmissionId] = updated;
        return updated;
    }
}
