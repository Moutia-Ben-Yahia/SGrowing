using System;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.FrontendArchitecture.MicroFrontends;

/// <summary>
/// Service contract for Edge SSR pre-rendering and progressive hydration strategy resolution.
/// </summary>
public interface IEdgeSsrHydrationService
{
    EdgeSsrManifest GenerateManifest(string routePath, HydrationStrategy? preferredStrategy = null, int viewportWidth = 1280);
    string PreRenderShellHtml(string routePath, IReadOnlyList<string> components, IReadOnlyDictionary<string, HydrationStrategy> hydrationMap);
    string GenerateCriticalCss(string routePath);
    IReadOnlyList<string> ResolveIntersectingComponents(string routePath, int viewportWidth = 1280);
    HydrationStrategy DetermineHydrationStrategy(string componentName, bool isAboveTheFold);
    IReadOnlyList<EdgeSsrManifest> GetPreCachedManifests();
}

/// <summary>
/// Service that generates Edge SSR manifests, pre-renders HTML skeletons with 0-CLS critical CSS,
/// and maps viewport intersections to progressive hydration strategies (Immediate, OnVisible, OnIdle).
/// </summary>
public sealed class EdgeSsrHydrationService : IEdgeSsrHydrationService
{
    private static readonly Dictionary<string, RouteComponentProfile> RouteProfiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/cockpit/commerce"] = new(
            AboveTheFold: ["@sgrowing/cockpit-commerce/HeroBanner"],
            ViewportVisible: ["@sgrowing/cockpit-commerce/ProductGrid"],
            BelowFold: ["@sgrowing/cockpit-commerce/CartSummary"]),

        ["/cockpit/fintech"] = new(
            AboveTheFold: ["@sgrowing/cockpit-fintech/RevenueKpiSummary"],
            ViewportVisible: ["@sgrowing/cockpit-fintech/TransactionLedger"],
            BelowFold: ["@sgrowing/cockpit-fintech/PayoutDrawer"]),

        ["/cockpit/agents"] = new(
            AboveTheFold: ["@sgrowing/cockpit-agents/AgentOrchestratorView"],
            ViewportVisible: ["@sgrowing/cockpit-agents/LiveChatThread"],
            BelowFold: ["@sgrowing/cockpit-agents/ToolInvocationList"]),

        ["/cockpit/voice"] = new(
            AboveTheFold: ["@sgrowing/cockpit-voice/TelephonyDialer"],
            ViewportVisible: ["@sgrowing/cockpit-voice/LiveTranscriptionTimeline"],
            BelowFold: ["@sgrowing/cockpit-voice/AudioWaveform"])
    };

    public EdgeSsrManifest GenerateManifest(string routePath, HydrationStrategy? preferredStrategy = null, int viewportWidth = 1280)
    {
        var normalizedRoute = NormalizeRoute(routePath);
        var profile = RouteProfiles.TryGetValue(normalizedRoute, out var p)
            ? p
            : new RouteComponentProfile(
                AboveTheFold: ["@sgrowing/core-ui/GlobalHeader"],
                ViewportVisible: ["@sgrowing/core-ui/DynamicRouteContainer"],
                BelowFold: ["@sgrowing/core-ui/GlobalFooter"]);

        var intersecting = ResolveIntersectingComponents(normalizedRoute, viewportWidth);

        var hydrationMap = new Dictionary<string, HydrationStrategy>(StringComparer.OrdinalIgnoreCase);
        foreach (var comp in profile.AboveTheFold)
        {
            hydrationMap[comp] = preferredStrategy ?? DetermineHydrationStrategy(comp, isAboveTheFold: true);
        }
        foreach (var comp in profile.ViewportVisible)
        {
            hydrationMap[comp] = preferredStrategy ?? DetermineHydrationStrategy(comp, isAboveTheFold: false);
        }
        foreach (var comp in profile.BelowFold)
        {
            hydrationMap[comp] = preferredStrategy ?? HydrationStrategy.OnIdle;
        }

        var criticalCss = GenerateCriticalCss(normalizedRoute);
        var allComponents = profile.AboveTheFold.Concat(profile.ViewportVisible).Concat(profile.BelowFold).ToList();
        var html = PreRenderShellHtml(normalizedRoute, allComponents, hydrationMap);

        var overallStrategy = preferredStrategy ?? (profile.AboveTheFold.Count > 0 ? HydrationStrategy.Immediate : HydrationStrategy.OnVisible);

        return new EdgeSsrManifest(
            RoutePath: normalizedRoute,
            PreRenderedHtml: html,
            CriticalCss: criticalCss,
            IntersectingComponents: intersecting,
            Strategy: overallStrategy,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            ComponentHydrationMap: hydrationMap,
            HostBundleUrl: "https://cdn.sgrowing.io/shell/main.js");
    }

    public string PreRenderShellHtml(string routePath, IReadOnlyList<string> components, IReadOnlyDictionary<string, HydrationStrategy> hydrationMap)
    {
        var normalizedRoute = NormalizeRoute(routePath);
        var nodes = components.Select(comp =>
        {
            hydrationMap.TryGetValue(comp, out var strat);
            var isImmediate = strat == HydrationStrategy.Immediate;
            var skeletonClass = isImmediate ? "sg-skeleton-pulse sg-above-fold" : "sg-skeleton-shimmer";

            return $"""
                <section class="sg-mfe-container {skeletonClass}"
                         data-mfe-component="{comp}"
                         data-hydration-strategy="{strat}"
                         data-route="{normalizedRoute}"
                         aria-busy="true">
                    <header class="sg-skeleton-header">
                        <div class="sg-skeleton-title"></div>
                    </header>
                    <div class="sg-skeleton-body">
                        <div class="sg-skeleton-line short"></div>
                        <div class="sg-skeleton-line medium"></div>
                        <div class="sg-skeleton-block"></div>
                    </div>
                </section>
                """;
        });

        var bodyMarkup = string.Join("\n", nodes);

        return $"""
            <!-- SGrowing Edge SSR Pre-rendered Shell for {normalizedRoute} -->
            <div id="sg-mfe-host-root" class="sg-edge-ssr-root" data-route="{normalizedRoute}">
                <main class="sg-mfe-grid-layout">
            {bodyMarkup}
                </main>
            </div>
            """;
    }

    public string GenerateCriticalCss(string routePath)
    {
        return """
            /* SGrowing Critical SSR Inlined Stylesheet (Zero CLS Guarantee) */
            .sg-edge-ssr-root { display: flex; flex-direction: column; width: 100%; min-height: 100vh; box-sizing: border-box; }
            .sg-mfe-grid-layout { display: grid; grid-template-columns: repeat(12, 1fr); gap: 16px; padding: 24px; max-width: 1440px; margin: 0 auto; width: 100%; }
            .sg-mfe-container { grid-column: span 12; border-radius: 8px; background: #ffffff; border: 1px solid #e2e8f0; min-height: 180px; padding: 16px; contain: layout style paint; }
            .sg-above-fold { min-height: 280px; }
            .sg-skeleton-title { height: 24px; width: 35%; background: #e2e8f0; border-radius: 4px; margin-bottom: 16px; }
            .sg-skeleton-line { height: 14px; background: #f1f5f9; border-radius: 4px; margin-bottom: 8px; }
            .sg-skeleton-line.short { width: 45%; }
            .sg-skeleton-line.medium { width: 75%; }
            .sg-skeleton-block { height: 100px; width: 100%; background: #f8fafc; border-radius: 6px; }
            @keyframes sgPulse { 0% { opacity: 0.9; } 50% { opacity: 0.5; } 100% { opacity: 0.9; } }
            .sg-skeleton-pulse { animation: sgPulse 1.8s infinite ease-in-out; }
            """;
    }

    public IReadOnlyList<string> ResolveIntersectingComponents(string routePath, int viewportWidth = 1280)
    {
        var normalizedRoute = NormalizeRoute(routePath);
        if (!RouteProfiles.TryGetValue(normalizedRoute, out var profile))
        {
            return ["@sgrowing/core-ui/GlobalHeader", "@sgrowing/core-ui/DynamicRouteContainer"];
        }

        // On mobile (< 768px), fewer components intersect initially; desktop intersects above + visible
        if (viewportWidth < 768)
        {
            return profile.AboveTheFold;
        }

        return profile.AboveTheFold.Concat(profile.ViewportVisible).ToList();
    }

    public HydrationStrategy DetermineHydrationStrategy(string componentName, bool isAboveTheFold)
    {
        if (isAboveTheFold) return HydrationStrategy.Immediate;

        if (componentName.Contains("Grid", StringComparison.OrdinalIgnoreCase) ||
            componentName.Contains("Ledger", StringComparison.OrdinalIgnoreCase) ||
            componentName.Contains("Chat", StringComparison.OrdinalIgnoreCase) ||
            componentName.Contains("Transcription", StringComparison.OrdinalIgnoreCase))
        {
            return HydrationStrategy.OnVisible;
        }

        return HydrationStrategy.OnIdle;
    }

    public IReadOnlyList<EdgeSsrManifest> GetPreCachedManifests()
    {
        return RouteProfiles.Keys.Select(r => GenerateManifest(r)).ToList();
    }

    private static string NormalizeRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route)) return "/";
        var trimmed = route.Trim();
        if (!trimmed.StartsWith('/')) trimmed = "/" + trimmed;
        return trimmed.TrimEnd('/');
    }

    private sealed record RouteComponentProfile(
        IReadOnlyList<string> AboveTheFold,
        IReadOnlyList<string> ViewportVisible,
        IReadOnlyList<string> BelowFold);
}
