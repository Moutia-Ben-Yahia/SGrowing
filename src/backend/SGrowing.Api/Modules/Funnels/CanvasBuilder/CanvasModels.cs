namespace SGrowing.Api.Modules.Funnels.CanvasBuilder;

public enum CanvasNodeType
{
    Section,
    Row,
    Column,
    HeroBlock,
    PricingCards,
    TestimonialGrid,
    LeadForm,
    CtaButton
}

public record CanvasNode(
    string NodeId,
    CanvasNodeType Type,
    string Name,
    Dictionary<string, string> Props,
    List<CanvasNode> Children,
    int OrderIndex
);

public record ThemeTokens(
    string PrimaryColor,
    string AccentColor,
    string FontFamily,
    string BorderRadius,
    string SpacingScale
);

public record FunnelPageCanvas(
    string CanvasId,
    string Name,
    string Slug,
    ThemeTokens Theme,
    List<CanvasNode> RootNodes,
    bool IsPublished,
    DateTime UpdatedAtUtc
);

public record SaveCanvasRequest(
    string Name,
    string Slug,
    ThemeTokens Theme,
    List<CanvasNode> RootNodes
);

public record CompileHtmlRequest(
    string CanvasId,
    string TargetViewport = "desktop"
);

public record CompiledHtmlResult(
    string CanvasId,
    string CompiledHtml,
    int TotalElementsCount,
    string TargetViewport
);
