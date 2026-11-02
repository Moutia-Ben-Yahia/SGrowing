using System.Collections.Concurrent;
using System.Text;

namespace SGrowing.Api.Modules.Funnels.CanvasBuilder;

public interface ICanvasEngine
{
    List<FunnelPageCanvas> ListCanvases();
    FunnelPageCanvas? GetCanvas(string canvasId);
    FunnelPageCanvas SaveCanvas(SaveCanvasRequest request);
    CompiledHtmlResult CompileHtml(CompileHtmlRequest request);
}

public class CanvasEngine : ICanvasEngine
{
    private readonly ConcurrentDictionary<string, FunnelPageCanvas> _canvases = new();

    public CanvasEngine()
    {
        SeedDefaultCanvas();
    }

    private void SeedDefaultCanvas()
    {
        var heroNode = new CanvasNode(
            NodeId: "node_hero",
            Type: CanvasNodeType.HeroBlock,
            Name: "Primary Hero Section",
            Props: new Dictionary<string, string>
            {
                { "headline", "Scale Revenue Autonomously With AI SDRs" },
                { "subheadline", "Deploy duplex voice agents, automated WhatsApp broadcasts, and predictive attribution." },
                { "ctaLabel", "Start 14-Day Free Pilot" }
            },
            Children: new List<CanvasNode>(),
            OrderIndex: 0
        );

        var pricingNode = new CanvasNode(
            NodeId: "node_pricing",
            Type: CanvasNodeType.PricingCards,
            Name: "3-Tier Enterprise Pricing Grid",
            Props: new Dictionary<string, string>
            {
                { "tier1", "Starter - $2,500/mo (3 Voice SDRs)" },
                { "tier2", "Growth - $6,500/mo (10 Voice SDRs + Warmup Mesh)" },
                { "tier3", "Enterprise - $12,500/mo (Unlimited + Air-Gapped Key)" }
            },
            Children: new List<CanvasNode>(),
            OrderIndex: 1
        );

        var formNode = new CanvasNode(
            NodeId: "node_form",
            Type: CanvasNodeType.LeadForm,
            Name: "Interactive Demo Capture Form",
            Props: new Dictionary<string, string>
            {
                { "buttonText", "Claim Sandbox Access" },
                { "redirectUrl", "/developer/sandbox" }
            },
            Children: new List<CanvasNode>(),
            OrderIndex: 2
        );

        var canvas = new FunnelPageCanvas(
            CanvasId: "canvas_sdr_funnel",
            Name: "Autonomous AI SDR High-Conversion Landing Funnel",
            Slug: "ai-sdr-pilot",
            Theme: new ThemeTokens(
                PrimaryColor: "#4f46e5",
                AccentColor: "#10b981",
                FontFamily: "Inter, sans-serif",
                BorderRadius: "rounded-xl",
                SpacingScale: "comfortable"
            ),
            RootNodes: new List<CanvasNode> { heroNode, pricingNode, formNode },
            IsPublished: true,
            UpdatedAtUtc: DateTime.UtcNow.AddDays(-3)
        );

        _canvases[canvas.CanvasId] = canvas;
    }

    public List<FunnelPageCanvas> ListCanvases() =>
        _canvases.Values.OrderByDescending(c => c.UpdatedAtUtc).ToList();

    public FunnelPageCanvas? GetCanvas(string canvasId) =>
        _canvases.TryGetValue(canvasId, out var canvas) ? canvas : null;

    public FunnelPageCanvas SaveCanvas(SaveCanvasRequest request)
    {
        var id = "canvas_" + Guid.NewGuid().ToString("N")[..8];
        var canvas = new FunnelPageCanvas(
            CanvasId: id,
            Name: request.Name,
            Slug: request.Slug,
            Theme: request.Theme,
            RootNodes: request.RootNodes,
            IsPublished: true,
            UpdatedAtUtc: DateTime.UtcNow
        );

        _canvases[id] = canvas;
        return canvas;
    }

    public CompiledHtmlResult CompileHtml(CompileHtmlRequest request)
    {
        if (!_canvases.TryGetValue(request.CanvasId, out var canvas))
        {
            throw new KeyNotFoundException($"Funnel canvas '{request.CanvasId}' not found.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"<!-- SGrowing Funnel Canvas: {canvas.Name} ({request.TargetViewport}) -->");
        sb.AppendLine($"<div class=\"sgrowing-funnel-page font-sans\" style=\"--primary: {canvas.Theme.PrimaryColor}; --accent: {canvas.Theme.AccentColor};\">");

        foreach (var node in canvas.RootNodes.OrderBy(n => n.OrderIndex))
        {
            sb.AppendLine($"  <section id=\"{node.NodeId}\" class=\"canvas-block my-8 p-6 {canvas.Theme.BorderRadius} border\">");
            sb.AppendLine($"    <h2 class=\"text-2xl font-bold mb-2\">{node.Name}</h2>");
            foreach (var (k, v) in node.Props)
            {
                sb.AppendLine($"    <div data-prop=\"{k}\" class=\"text-sm text-slate-700\">{v}</div>");
            }
            sb.AppendLine("  </section>");
        }
        sb.AppendLine("</div>");

        return new CompiledHtmlResult(
            CanvasId: canvas.CanvasId,
            CompiledHtml: sb.ToString(),
            TotalElementsCount: canvas.RootNodes.Count,
            TargetViewport: request.TargetViewport
        );
    }
}
