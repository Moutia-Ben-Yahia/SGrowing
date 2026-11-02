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
}
