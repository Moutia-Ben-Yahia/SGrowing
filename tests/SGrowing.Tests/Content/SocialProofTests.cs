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

    [Fact]
    public void ApproveTestimonial_ExistingAndMissing_ApprovesOrThrows()
    {
        var approved = _engine.ApproveTestimonial("tst_text_02");
        Assert.NotNull(approved);
        Assert.True(approved.IsApprovedForWallOfLove);

        Assert.Throws<KeyNotFoundException>(() =>
            _engine.ApproveTestimonial("tst_missing_999"));
    }

    [Fact]
    public void RequestAndPublish_NullRequests_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _engine.RequestTestimonial(null!));

        Assert.Throws<ArgumentNullException>(() =>
            _engine.PublishWallOfLove(null!));
    }

    [Fact]
    public void PublishWallOfLove_NullApprovedIds_DefaultsToAllApprovedCount()
    {
        var result = _engine.PublishWallOfLove(new PublishWallOfLoveRequest(null!, "LightMinimal"));

        Assert.NotNull(result);
        Assert.True(result.IncludedTestimonialsCount >= 2);
        Assert.Contains("data-theme=\"LightMinimal\"", result.EmbedWidgetHtml);
    }
}
