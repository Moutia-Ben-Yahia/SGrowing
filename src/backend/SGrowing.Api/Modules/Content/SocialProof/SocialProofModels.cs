using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Content.SocialProof;

public enum TestimonialFormat
{
    VideoRecording,
    AudioClip,
    RichTextReview
}

public record TestimonialRecord(
    string TestimonialId,
    string CustomerName,
    string CustomerTitle,
    string CompanyName,
    string CompanyDomain,
    TestimonialFormat Format,
    int StarRating, // 1 - 5
    string FullTranscriptOrText,
    string KeySoundbiteQuote,
    double SentimentScore, // 0.0 - 1.0
    bool IsApprovedForWallOfLove,
    string? MediaAssetUrl,
    DateTime SubmittedAtUtc
);

public record RequestTestimonialPayload(
    string CustomerEmail,
    string CustomerName,
    string CompanyName,
    string TriggerMilestone // e.g. "NPS_10", "Quarterly_Goal_Exceeded", "Contract_Renewal"
);

public record RequestTestimonialResult(
    string RequestId,
    string MagicRecordingUrl,
    string ExpirationDateUtc,
    bool EmailDispatched
);

public record PublishWallOfLoveRequest(
    List<string> ApprovedTestimonialIds,
    string Theme = "DarkEnterprise",
    bool EnableRichSnippetsSchema = true
);

public record PublishWallOfLoveResult(
    string EmbedWidgetHtml,
    string SchemaJsonLdSnippet,
    int IncludedTestimonialsCount,
    DateTime PublishedAtUtc
);
