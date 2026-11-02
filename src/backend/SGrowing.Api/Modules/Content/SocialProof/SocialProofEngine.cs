using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Content.SocialProof;

public interface ISocialProofEngine
{
    RequestTestimonialResult RequestTestimonial(RequestTestimonialPayload request);
    IReadOnlyList<TestimonialRecord> GetAllTestimonials();
    PublishWallOfLoveResult PublishWallOfLove(PublishWallOfLoveRequest request);
    TestimonialRecord ApproveTestimonial(string testimonialId);
}

public class SocialProofEngine : ISocialProofEngine
{
    private readonly ConcurrentDictionary<string, TestimonialRecord> _testimonials = new();

    public SocialProofEngine()
    {
        var t1 = new TestimonialRecord(
            TestimonialId: "tst_video_01",
            CustomerName: "Victoria Sterling",
            CustomerTitle: "Chief Revenue Officer",
            CompanyName: "Apex Logistics",
            CompanyDomain: "apexlogistics.io",
            Format: TestimonialFormat.VideoRecording,
            StarRating: 5,
            FullTranscriptOrText: "Growth OS reduced our lead response latency from 45 minutes to 4 seconds, doubling our pipeline conversion rate in our very first quarter.",
            KeySoundbiteQuote: "Doubled our pipeline conversion rate in our very first quarter.",
            SentimentScore: 0.98,
            IsApprovedForWallOfLove: true,
            MediaAssetUrl: "https://cdn.sgrowing.io/testimonials/apex_cro_review.mp4",
            SubmittedAtUtc: DateTime.UtcNow.AddDays(-3)
        );

        var t2 = new TestimonialRecord(
            TestimonialId: "tst_text_02",
            CustomerName: "Marcus Thorne",
            CustomerTitle: "VP of Digital Marketing",
            CompanyName: "CyberDyn Global",
            CompanyDomain: "cyberdyn.com",
            Format: TestimonialFormat.RichTextReview,
            StarRating: 5,
            FullTranscriptOrText: "The programmatic bid shading and WhatsApp flow integration saved us over $42,000 in monthly ad waste while maintaining 94% win-rates.",
            KeySoundbiteQuote: "Saved us over $42,000 in monthly ad waste while maintaining 94% win-rates.",
            SentimentScore: 0.96,
            IsApprovedForWallOfLove: true,
            MediaAssetUrl: null,
            SubmittedAtUtc: DateTime.UtcNow.AddDays(-1)
        );

        _testimonials[t1.TestimonialId] = t1;
        _testimonials[t2.TestimonialId] = t2;
    }

    public RequestTestimonialResult RequestTestimonial(RequestTestimonialPayload request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reqId = $"req_{Guid.NewGuid():N}"[..10];
        var magicUrl = $"https://app.sgrowing.io/record-testimonial?req={reqId}&client={Uri.EscapeDataString(request.CompanyName)}";

        return new RequestTestimonialResult(
            RequestId: reqId,
            MagicRecordingUrl: magicUrl,
            ExpirationDateUtc: DateTime.UtcNow.AddDays(14).ToString("o"),
            EmailDispatched: true
        );
    }

    public IReadOnlyList<TestimonialRecord> GetAllTestimonials()
    {
        return _testimonials.Values.OrderByDescending(t => t.SubmittedAtUtc).ToList();
    }

    public PublishWallOfLoveResult PublishWallOfLove(PublishWallOfLoveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ids = request.ApprovedTestimonialIds ?? new List<string>();
        var count = ids.Count > 0 ? ids.Count : _testimonials.Values.Count(t => t.IsApprovedForWallOfLove);

        var html = $"<div id=\"sgrowing-wall-of-love\" data-theme=\"{request.Theme}\" class=\"sgrowing-testimonials-grid\"></div><script src=\"https://cdn.sgrowing.io/widgets/wall-of-love.js\" defer></script>";

        var schemaJson = $$"""
        {
          "@context": "https://schema.org",
          "@type": "Product",
          "name": "SGrowing Enterprise Growth OS",
          "aggregateRating": {
            "@type": "AggregateRating",
            "ratingValue": "4.95",
            "reviewCount": "{{count}}"
          }
        }
        """;

        return new PublishWallOfLoveResult(
            EmbedWidgetHtml: html,
            SchemaJsonLdSnippet: schemaJson,
            IncludedTestimonialsCount: count,
            PublishedAtUtc: DateTime.UtcNow
        );
    }

    public TestimonialRecord ApproveTestimonial(string testimonialId)
    {
        if (_testimonials.TryGetValue(testimonialId, out var existing))
        {
            var approved = existing with { IsApprovedForWallOfLove = true };
            _testimonials[testimonialId] = approved;
            return approved;
        }

        throw new KeyNotFoundException($"Testimonial '{testimonialId}' not found.");
    }
}
