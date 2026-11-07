namespace SGrowing.Api.Modules.CMS.VideoCreativePipeline;

public enum VideoAspectRatio
{
    Vertical_9_16,
    Square_1_1,
    Landscape_16_9
}

public enum ArabicRegionalDialect
{
    ModernStandardArabic,
    SaudiNajdi,
    SaudiHejazi,
    GulfKhaleeji,
    Egyptian
}

public enum AdCreativeStage
{
    HookIntro,
    ProblemStatement,
    ProductShowcase,
    SocialProofTestimonial,
    CallToAction
}

public record VideoStoryboardScene(
    int SceneNumber,
    AdCreativeStage Stage,
    string VisualPrompt,
    string VoiceoverScript,
    int DurationSeconds,
    string BackgroundTrack,
    string TextOverlayText
);

public record VideoAdCampaignProject(
    Guid ProjectId,
    Guid TenantId,
    string ProjectTitle,
    string ProductSku,
    string TargetAudience,
    ArabicRegionalDialect Dialect,
    VideoAspectRatio AspectRatio,
    List<VideoStoryboardScene> Scenes,
    int TotalDurationSeconds,
    decimal EstimatedRenderCostSar,
    DateTime CreatedAtUtc
);

public record CreativeVariantRecord(
    Guid VariantId,
    Guid ProjectId,
    Guid TenantId,
    string VariantCode,
    string HookText,
    string CtaText,
    double BanditExplorationScore,
    double PredictedCtrPercent,
    DateTime GeneratedAtUtc
);

public record CreateVideoProjectRequest(
    Guid TenantId,
    string ProjectTitle,
    string ProductSku,
    string TargetAudience,
    ArabicRegionalDialect Dialect,
    VideoAspectRatio AspectRatio,
    List<VideoStoryboardScene>? CustomScenes
);

public record GenerateVariantsRequest(
    Guid TenantId,
    Guid ProjectId,
    int VariantCount
);

public record SynthesizeDialectScriptRequest(
    string StandardScript,
    ArabicRegionalDialect TargetDialect
);

public record SynthesizedDialectScriptResponse(
    ArabicRegionalDialect TargetDialect,
    string StandardScript,
    string LocalizedScript,
    List<string> DialectIdiomsUsed
);

public record GenerateVariantsEndpointBody(
    int? VariantCount = null,
    Guid? TenantId = null
);
