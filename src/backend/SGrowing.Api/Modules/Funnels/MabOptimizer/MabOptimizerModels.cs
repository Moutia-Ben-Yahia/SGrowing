namespace SGrowing.Api.Modules.Funnels.MabOptimizer;

public enum BlockType
{
    HeroBanner,
    FeatureGrid,
    PricingMatrix,
    TestimonialCarousel,
    LeadCaptureForm,
    UrgencyCountdown,
    CtaBanner
}

public record FunnelBlock(
    string Id,
    BlockType Type,
    string Title,
    string Subtitle,
    Dictionary<string, string> Config,
    int Order
);

public record VariantAllocation(
    string VariantId,
    string Name,
    double TrafficWeight,
    int Impressions,
    int Conversions,
    double ConversionRate,
    double Score
);

public record FunnelPage(
    string TenantId,
    string FunnelId,
    string Path,
    string Title,
    string SeoMetaDescription,
    List<FunnelBlock> Blocks,
    List<VariantAllocation> Variants,
    DateTime UpdatedAtUtc
);

public record CreateFunnelRequest(
    string TenantId,
    string FunnelId,
    string Path,
    string Title,
    string SeoMetaDescription,
    List<FunnelBlock> InitialBlocks,
    List<string>? InitialVariantNames = null
);

public record UpdateBlocksRequest(
    string TenantId,
    string FunnelId,
    List<FunnelBlock> Blocks
);

public record RecordImpressionRequest(
    string TenantId,
    string FunnelId,
    string VariantId
);

public record RecordConversionRequest(
    string TenantId,
    string FunnelId,
    string VariantId,
    decimal Revenue
);

public record MabOptimizationReport(
    string FunnelId,
    string LeadingVariantId,
    double ConfidenceLevel,
    double UpliftPercent,
    List<VariantAllocation> Variants,
    string GeneratedJsonLd
);
