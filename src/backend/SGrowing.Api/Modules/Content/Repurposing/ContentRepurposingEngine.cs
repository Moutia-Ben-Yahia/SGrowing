using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Content.Repurposing;

public interface IContentRepurposingEngine
{
    RepurposingManifest DecomposeContent(DecomposeContentRequest request);
    RepurposingManifest? GetManifestByAssetId(string sourceAssetId);
    DerivativeContent? GetDerivativeById(string derivativeId);
}

public class ContentRepurposingEngine : IContentRepurposingEngine
{
    private readonly ConcurrentDictionary<string, RepurposingManifest> _manifests = new(StringComparer.OrdinalIgnoreCase);

    public ContentRepurposingEngine()
    {
        // Seed default cornerstone asset repurposing
        DecomposeContent(new DecomposeContentRequest(
            "asset_webinar_101",
            "The 2026 Shift from Manual SDR Outbound to Autonomous Agent Swarms",
            "In this 45-minute keynote, we demonstrate why traditional SDR outbound generates <1.2% reply rates, whereas autonomous agent swarms achieve 8.4% by coordinating research, intent de-anonymization, and customized multi-turn objection handling.",
            new List<ContentChannel> { ContentChannel.LinkedInPost, ContentChannel.XTwitterThread, ContentChannel.SlideCarousel }
        ));
    }

    public RepurposingManifest DecomposeContent(DecomposeContentRequest request)
    {
        var channels = (request.TargetChannels != null && request.TargetChannels.Count > 0)
            ? request.TargetChannels
            : new List<ContentChannel> { ContentChannel.LinkedInPost, ContentChannel.XTwitterThread, ContentChannel.ExecutiveNewsletter, ContentChannel.SlideCarousel };

        var derivatives = new List<DerivativeContent>();

        foreach (var channel in channels)
        {
            var derivative = channel switch
            {
                ContentChannel.LinkedInPost => new DerivativeContent(
                    $"deriv_{request.SourceAssetId}_li",
                    request.SourceAssetId,
                    ContentChannel.LinkedInPost,
                    "Why SDR Cold Outreach Died in 2026 (And What Swarms Do Instead)",
                    "Traditional cold emailing is burning your domain reputation. Here is the data: 1.2% response vs 8.4% with autonomous swarms.\n\n3 key takeaways:\n1. De-anonymize buying intent before sending a single email.\n2. Involve the whole buying committee, not just one lead.\n3. Automate objection handling under 150ms.\n\nWhat are you seeing in your outbound numbers this quarter?",
                    45,
                    new List<string> { "#B2BGrowth", "#AgenticAI", "#OutboundRevolution" },
                    null
                ),
                ContentChannel.XTwitterThread => new DerivativeContent(
                    $"deriv_{request.SourceAssetId}_x",
                    request.SourceAssetId,
                    ContentChannel.XTwitterThread,
                    "The Death of the 200-SDR Sales Floor [1/5]",
                    "1/ The typical $100k B2B SaaS deal now involves 8 decision makers.\n2/ Blasting 1 persona is dead on arrival.\n3/ Autonomous swarms map the committee, verify CISO compliance upfront, and tailor multi-year ramps.\n4/ Result? 3.2x higher win rate with zero headcount bloat.\n5/ Full architecture breakdown in the link below.",
                    30,
                    new List<string> { "#SalesTech", "#AI", "#Growth" },
                    null
                ),
                ContentChannel.ExecutiveNewsletter => new DerivativeContent(
                    $"deriv_{request.SourceAssetId}_nl",
                    request.SourceAssetId,
                    ContentChannel.ExecutiveNewsletter,
                    "Executive Briefing: Modernizing Your Go-To-Market Engine",
                    "Dear Founder,\n\nWe recently analyzed 10,000 enterprise B2B sales cycles. The data reveals a massive divergence: teams relying on static sequences saw reply rates collapse to 1.2%, while autonomous multi-agent meshes achieved 8.4% engagement.\n\nHere are the 3 structural changes your team should implement this month...",
                    90,
                    new List<string> { "#ExecutiveBriefing", "#EnterpriseSaaS" },
                    null
                ),
                ContentChannel.SlideCarousel => new DerivativeContent(
                    $"deriv_{request.SourceAssetId}_slides",
                    request.SourceAssetId,
                    ContentChannel.SlideCarousel,
                    "5-Slide Carousel: Why Autonomous Swarms Outperform Manual Outbound",
                    "Slide-by-slide executive breakdown ready for LinkedIn PDF publishing.",
                    60,
                    new List<string> { "#Slides", "#Carousel" },
                    new List<CarouselSlide>
                    {
                        new(1, "The Outbound Dilemma", "SDR reply rates dropped to 1.2% in 2026.", "Bold typography on dark minimalist background"),
                        new(2, "The Real Cause", "8+ stakeholders per buying committee; single-lead outreach fails.", "Visual org chart graphic"),
                        new(3, "The Autonomous Solution", "Coordinated agent swarms handle research, CISO briefs, and follow-ups.", "Agent workflow diagram"),
                        new(4, "The Business Impact", "8.4% reply rate, 3.4x productivity multiplier, zero spam penalties.", "Comparative metrics chart"),
                        new(5, "Takeaway & Action", "Upgrade to the AI-Native Growth OS today.", "Strong CTA button mockup")
                    }
                ),
                _ => new DerivativeContent(
                    $"deriv_{request.SourceAssetId}_video",
                    request.SourceAssetId,
                    ContentChannel.ShortVideoScript,
                    "60s TikTok / Shorts Script",
                    "[Hook] If your sales team is still manually writing cold emails, stop scrolling.\n[Body] Here is why top Series B companies switched to autonomous swarms...",
                    60,
                    new List<string> { "#Shorts", "#AI" },
                    null
                )
            };

            derivatives.Add(derivative);
        }

        var manifest = new RepurposingManifest(
            request.SourceAssetId,
            request.SourceTitle,
            "Autonomous multi-agent swarms achieve 8.4% engagement vs 1.2% for manual cold outreach.",
            new List<string>
            {
                "8.4% response rate for agent swarms vs 1.2% industry average.",
                "8+ stakeholders per enterprise buying committee required for consensus.",
                "Zero-spam domain reputation protection through intent de-anonymization."
            },
            derivatives,
            DateTime.UtcNow
        );

        _manifests[request.SourceAssetId] = manifest;
        return manifest;
    }

    public RepurposingManifest? GetManifestByAssetId(string sourceAssetId)
    {
        _manifests.TryGetValue(sourceAssetId, out var manifest);
        return manifest;
    }

    public DerivativeContent? GetDerivativeById(string derivativeId)
    {
        return _manifests.Values
            .SelectMany(m => m.Derivatives)
            .FirstOrDefault(d => d.DerivativeId.Equals(derivativeId, StringComparison.OrdinalIgnoreCase));
    }
}
