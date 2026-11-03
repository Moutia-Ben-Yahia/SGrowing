using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Advertising.VideoDco;

public sealed class VideoDcoEngine
{
    private readonly ConcurrentDictionary<string, VideoTemplate> _templates = new();
    private readonly ConcurrentDictionary<string, RenderedVariant> _variants = new();
    private readonly ConcurrentBag<RenderJobReceipt> _jobHistory = new();

    public VideoDcoEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var tpl169 = new VideoTemplate(
            "TPL-LANDSCAPE-169",
            "B2B Enterprise Showcase 16:9",
            "16:9",
            30,
            new[] { "VideoBackground", "LocalizedLogo", "ValuePropositionText", "DynamicPriceBadge", "OutroCTA" },
            "ACTIVE"
        );

        var tpl916 = new VideoTemplate(
            "TPL-VERTICAL-916",
            "TikTok & Reels Dynamic Teaser 9:16",
            "9:16",
            15,
            new[] { "HookIntro", "UGCReviewerCard", "AnimatedOfferSticker", "SwipeUpCTA" },
            "ACTIVE"
        );

        var tpl11 = new VideoTemplate(
            "TPL-SQUARE-11",
            "LinkedIn Feed Carousel 1:1",
            "1:1",
            20,
            new[] { "StatCounterLayer", "CustomerQuoteLayer", "BookDemoButton" },
            "ACTIVE"
        );

        _templates[tpl169.TemplateId] = tpl169;
        _templates[tpl916.TemplateId] = tpl916;
        _templates[tpl11.TemplateId] = tpl11;

        var v1 = new RenderedVariant(
            "VAR-EU-DE-01",
            tpl169.TemplateId,
            "DACH Enterprise FinTech",
            "EUR",
            4990m,
            "https://cdn.sgrowing.io/dco/render/eu-de-fintech-1080p.mp4",
            "WARMED_EDGE",
            DateTime.UtcNow.AddHours(-2)
        );

        var v2 = new RenderedVariant(
            "VAR-US-NY-02",
            tpl916.TemplateId,
            "US SaaS Founders",
            "USD",
            299m,
            "https://cdn.sgrowing.io/dco/render/us-saas-founders-4k.mp4",
            "WARMED_EDGE",
            DateTime.UtcNow.AddHours(-1)
        );

        _variants[v1.VariantId] = v1;
        _variants[v2.VariantId] = v2;
    }

    public VideoDcoOverview GetOverview()
    {
        var templatesList = _templates.Values.ToList();
        var variantsList = _variants.Values.ToList();

        return new VideoDcoOverview(
            templatesList.Count,
            variantsList.Count,
            1.45,
            97.8,
            templatesList,
            variantsList
        );
    }

    public VideoManifestResult GenerateManifest(GenerateVideoManifestRequest request)
    {
        var template = _templates.TryGetValue(request.TemplateId, out var tpl) ? tpl : _templates.Values.First();
        var manifestId = $"MAN-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var frames = template.DurationSeconds * 60; // 60fps

        var overlay = $"{{\"headline\": \"{request.DynamicHeadline}\", \"currency\": \"{request.LocalizedCurrency}\", \"price\": {request.DisplayedPrice}, \"cta\": \"{request.CallToActionText}\"}}";

        return new VideoManifestResult(
            manifestId,
            template.TemplateId,
            template.AspectRatio,
            frames,
            template.DynamicLayers,
            "https://cdn.sgrowing.io/audio/ambient-tech-pulse-48k.aac",
            overlay
        );
    }

    public RenderJobReceipt RenderVariant(RenderVideoVariantRequest request)
    {
        var jobId = $"JOB-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var outputUrl = $"https://cdn.sgrowing.io/dco/render/{jobId}-{request.Resolution.ToLowerInvariant()}.mp4";
        var purgeReceipt = request.InvalidateCdnCache ? $"PURGE-{Guid.NewGuid():N}"[..8].ToUpperInvariant() : "NO_PURGE";

        var receipt = new RenderJobReceipt(
            jobId,
            request.ManifestId,
            request.Resolution,
            outputUrl,
            purgeReceipt,
            1420,
            "RENDER_COMPLETE"
        );

        _jobHistory.Add(receipt);
        return receipt;
    }
}
