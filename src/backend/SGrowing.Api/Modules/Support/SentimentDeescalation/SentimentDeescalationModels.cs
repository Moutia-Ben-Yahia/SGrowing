using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Support.SentimentDeescalation;

public enum SentimentLevel
{
    Delighted,
    Satisfied,
    PassiveNeutral,
    FrustratedAtRisk
}

public record GoodwillCompensation(
    string CompensationId,
    decimal AccountCreditDollars,
    string AppliedBenefit,
    DateTime IssuedAtUtc
);

public record CsatSubmission(
    string SubmissionId,
    string CustomerId,
    string CustomerEmail,
    int RatingScore, // 1 to 5 stars
    string FeedbackText,
    SentimentLevel EvaluatedSentiment,
    bool RequiresUrgentTriage,
    GoodwillCompensation? AppliedGoodwill,
    DateTime SubmittedAtUtc
);

public record SubmitCsatRequest(
    string CustomerId,
    string CustomerEmail,
    int RatingScore,
    string FeedbackText
);

public record IssueGoodwillRequest(
    string SubmissionId,
    decimal CreditDollars,
    string BenefitDescription
);
