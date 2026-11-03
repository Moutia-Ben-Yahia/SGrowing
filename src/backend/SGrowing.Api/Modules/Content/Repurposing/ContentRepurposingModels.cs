using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Content.Repurposing;

public enum ContentChannel
{
    LinkedInPost,
    XTwitterThread,
    ExecutiveNewsletter,
    ShortVideoScript,
    SlideCarousel
}

public record CarouselSlide(
    int SlideNumber,
    string Header,
    string BulletPoints,
    string VisualNote
);

public record DerivativeContent(
    string DerivativeId,
    string SourceAssetId,
    ContentChannel Channel,
    string Title,
    string FormattedBody,
    int EstimatedReadSeconds,
    List<string> RecommendedHashtags,
    List<CarouselSlide>? Slides
);

public record RepurposingManifest(
    string SourceAssetId,
    string SourceTitle,
    string CoreThesis,
    List<string> KeyProofPoints,
    List<DerivativeContent> Derivatives,
    DateTime CreatedAtUtc
);

public record DecomposeContentRequest(
    string SourceAssetId,
    string SourceTitle,
    string RawTranscriptOrMarkdown,
    List<ContentChannel>? TargetChannels
);
