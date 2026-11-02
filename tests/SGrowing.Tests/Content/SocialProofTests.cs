using System.Collections.Generic;
using SGrowing.Api.Modules.Content.SocialProof;
using Xunit;

namespace SGrowing.Tests.Content;

public class SocialProofTests
{
    private readonly SocialProofEngine _engine = new();

    [Fact]
    public void RequestTestimonial_GeneratesMagicUrlAndValidExpiration()
    {
        // Arrange
        var request = new RequestTestimonialPayload(
            CustomerEmail: "cmo@enterprise.corp",
            CustomerName: "Victoria Sterling",
            CompanyName: "Apex Logistics",
            TriggerMilestone: "NPS_10"
        );

        // Act
        var result = _engine.RequestTestimonial(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.EmailDispatched);
        Assert.Contains("record-testimonial", result.MagicRecordingUrl);
        Assert.StartsWith("req_", result.RequestId);
    }

    [Fact]
    public void GetAllTestimonials_ReturnsExistingHighSentimentEntries()
    {
        // Act
        var list = _engine.GetAllTestimonials();

        // Assert
        Assert.NotNull(list);
        Assert.True(list.Count >= 2);
        Assert.All(list, t =>
        {
            Assert.True(t.StarRating >= 4);
            Assert.True(t.SentimentScore >= 0.90);
            Assert.False(string.IsNullOrWhiteSpace(t.KeySoundbiteQuote));
        });
    }

    [Fact]
    public void PublishWallOfLove_GeneratesWidgetHtmlAndSchemaJsonLd()
    {
        // Arrange
        var request = new PublishWallOfLoveRequest(
            ApprovedTestimonialIds: new List<string> { "tst_video_01", "tst_text_02" },
            Theme: "DarkEnterprise"
        );

        // Act
        var result = _engine.PublishWallOfLove(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.IncludedTestimonialsCount);
        Assert.Contains("sgrowing-wall-of-love", result.EmbedWidgetHtml);
        Assert.Contains("\"@type\": \"Product\"", result.SchemaJsonLdSnippet);
    }
}
