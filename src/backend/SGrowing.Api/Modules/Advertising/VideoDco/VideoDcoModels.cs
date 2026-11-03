using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Advertising.VideoDco;

public sealed record VideoDcoOverview(
    int ActiveTemplatesCount,
    int TotalRenderedVariantsCount,
    double AverageRenderLatencySeconds,
    double CdnCacheHitRatioPct,
    IReadOnlyList<VideoTemplate> Templates,
    IReadOnlyList<RenderedVariant> RecentVariants
);

public sealed record VideoTemplate(
    string TemplateId,
    string TemplateName,
    string AspectRatio,
    int DurationSeconds,
    IReadOnlyList<string> DynamicLayers,
    string Status
);

public sealed record RenderedVariant(
    string VariantId,
    string TemplateId,
    string TargetAudience,
    string LocalizedCurrency,
    decimal DisplayedPrice,
    string RenditionUrl,
    string CdnStatus,
    DateTime CreatedAt
);

public sealed record GenerateVideoManifestRequest(
    string TemplateId,
    string TargetAudience,
    string LocalizedCurrency,
    decimal DisplayedPrice,
    string DynamicHeadline,
    string CallToActionText,
    string BrandLogoUrl
);

public sealed record VideoManifestResult(
    string ManifestId,
    string TemplateId,
    string AspectRatio,
    int EstimatedRenderFrames,
    IReadOnlyList<string> LayerSequences,
    string AudioTrackUrl,
    string OverlayJson
);

public sealed record RenderVideoVariantRequest(
    string ManifestId,
    string Resolution,
    bool InvalidateCdnCache
);

public sealed record RenderJobReceipt(
    string JobId,
    string ManifestId,
    string OutputResolution,
    string OutputVideoUrl,
    string CdnPurgeReceipt,
    int RenderDurationMs,
    string Status
);
