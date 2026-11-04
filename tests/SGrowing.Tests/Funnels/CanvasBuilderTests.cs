using SGrowing.Api.Modules.Funnels.CanvasBuilder;

namespace SGrowing.Tests.Funnels;

public class CanvasBuilderTests
{
    private readonly ICanvasEngine _engine = new CanvasEngine();

    [Fact]
    public void ListCanvases_ReturnsSeededFunnelCanvas()
    {
        var canvases = _engine.ListCanvases();

        Assert.NotEmpty(canvases);
        Assert.Equal("canvas_sdr_funnel", canvases[0].CanvasId);
        Assert.Equal(3, canvases[0].RootNodes.Count);
        Assert.Equal("#4f46e5", canvases[0].Theme.PrimaryColor);
    }

    [Fact]
    public void SaveCanvas_StoresNewHierarchyAndThemeTokens()
    {
        var nodes = new List<CanvasNode>
        {
            new("n_test", CanvasNodeType.HeroBlock, "Hero Test", new Dictionary<string, string>(), new List<CanvasNode>(), 0)
        };

        var request = new SaveCanvasRequest(
            Name: "B2B Enterprise Quote Request Funnel",
            Slug: "b2b-quote-funnel",
            Theme: new ThemeTokens("#2563eb", "#f59e0b", "Poppins, sans-serif", "rounded-2xl", "spacious"),
            RootNodes: nodes
        );

        var canvas = _engine.SaveCanvas(request);

        Assert.NotNull(canvas);
        Assert.Equal("b2b-quote-funnel", canvas.Slug);
        Assert.Single(canvas.RootNodes);
        Assert.Equal("#2563eb", canvas.Theme.PrimaryColor);
    }

    [Fact]
    public void CompileHtml_GeneratesSemanticStructureForViewport()
    {
        var compiled = _engine.CompileHtml(new CompileHtmlRequest(
            CanvasId: "canvas_sdr_funnel",
            TargetViewport: "mobile"
        ));

        Assert.NotNull(compiled);
        Assert.Equal("mobile", compiled.TargetViewport);
        Assert.Equal(3, compiled.TotalElementsCount);
        Assert.Contains("sgrowing-funnel-page", compiled.CompiledHtml);
        Assert.Contains("node_hero", compiled.CompiledHtml);
    }

    [Fact]
    public void GetCanvas_ExistingAndNonExistent_ReturnsExpected()
    {
        var existing = _engine.GetCanvas("canvas_sdr_funnel");
        Assert.NotNull(existing);
        Assert.Equal("ai-sdr-pilot", existing.Slug);

        var missing = _engine.GetCanvas("canvas_nonexistent_xyz");
        Assert.Null(missing);
    }

    [Fact]
    public void CompileHtml_UnknownCanvasId_ThrowsKeyNotFoundException()
    {
        var request = new CompileHtmlRequest("canvas_missing_404", "desktop");
        Assert.Throws<KeyNotFoundException>(() => _engine.CompileHtml(request));
    }

    [Fact]
    public void CompileHtml_DesktopViewport_IncludesThemeColorsAndProps()
    {
        var compiled = _engine.CompileHtml(new CompileHtmlRequest(
            CanvasId: "canvas_sdr_funnel",
            TargetViewport: "desktop"
        ));

        Assert.NotNull(compiled);
        Assert.Equal("desktop", compiled.TargetViewport);
        Assert.Contains("--primary: #4f46e5", compiled.CompiledHtml);
        Assert.Contains("--accent: #10b981", compiled.CompiledHtml);
        Assert.Contains("Scale Revenue Autonomously", compiled.CompiledHtml);
        Assert.Contains("Claim Sandbox Access", compiled.CompiledHtml);
    }

    [Fact]
    public void SaveCanvas_AppendsToCanvasCatalog()
    {
        var initialCount = _engine.ListCanvases().Count;

        var request = new SaveCanvasRequest(
            Name: "Omnichannel Webinar Funnel",
            Slug: "omnichannel-webinar",
            Theme: new ThemeTokens("#059669", "#d97706", "Inter", "rounded-lg", "default"),
            RootNodes: new List<CanvasNode>()
        );

        var saved = _engine.SaveCanvas(request);
        Assert.NotNull(saved);
        Assert.StartsWith("canvas_", saved.CanvasId);

        var list = _engine.ListCanvases();
        Assert.Equal(initialCount + 1, list.Count);
        Assert.Contains(list, c => c.CanvasId == saved.CanvasId);
    }
}
