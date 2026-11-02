using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.SEO.Programmatic;

public interface IProgrammaticSeoEngine
{
    List<ProgrammaticPage> GeneratePages(BatchGeneratePagesRequest request);
    List<ProgrammaticPage> ListPages();
    List<BacklinkOpportunity> ListBacklinkOpportunities();
    IndexNowSubmissionResult SubmitToIndexNow(List<string> pageUrls);
}

public class ProgrammaticSeoEngine : IProgrammaticSeoEngine
{
    private readonly ConcurrentBag<ProgrammaticPage> _pages = new();
    private readonly ConcurrentBag<BacklinkOpportunity> _backlinks = new();

    public ProgrammaticSeoEngine()
    {
        SeedDefaultData();
    }

    private void SeedDefaultData()
    {
        var samplePages = new List<ProgrammaticPage>
        {
            new(
                PageId: "pg_fintech_01",
                Slug: "/solutions/crm-for-fintech",
                TargetKeyword: "Best Growth CRM for FinTech Startups",
                Industry: "FinTech",
                MetaTitle: "Enterprise Growth OS & Compliant CRM for FinTech | SGrowing",
                MetaDescription: "Scale FinTech acquisition with air-gapped security, SOC2-compliant SDR agents, and automated dunning.",
                H1Title: "Autonomous Growth Infrastructure for FinTech",
                SchemaJsonLd: "{\"@context\":\"https://schema.org\",\"@type\":\"SoftwareApplication\",\"name\":\"SGrowing FinTech CRM\"}",
                InternalLinks: new List<string> { "/features/webrtc-voice", "/pricing", "/solutions/crm-for-ecommerce" },
                WordCount: 1450,
                Status: PagePublishStatus.Indexed,
                PublishedAtUtc: DateTime.UtcNow.AddDays(-14)
            ),
            new(
                PageId: "pg_ecom_02",
                Slug: "/solutions/crm-for-ecommerce",
                TargetKeyword: "B2B E-Commerce Marketing Automation Platform",
                Industry: "E-Commerce",
                MetaTitle: "High-Volume Omnichannel CRM for B2B E-Commerce | SGrowing",
                MetaDescription: "Recover abandoned carts and scale Meta CAPI ROAS with automated multi-armed bandit funnels.",
                H1Title: "E-Commerce Growth Automation at Scale",
                SchemaJsonLd: "{\"@context\":\"https://schema.org\",\"@type\":\"SoftwareApplication\",\"name\":\"SGrowing E-Commerce CRM\"}",
                InternalLinks: new List<string> { "/solutions/crm-for-fintech", "/features/mab-funnels" },
                WordCount: 1620,
                Status: PagePublishStatus.Indexed,
                PublishedAtUtc: DateTime.UtcNow.AddDays(-7)
            )
        };

        foreach (var p in samplePages) _pages.Add(p);

        var sampleBacklinks = new List<BacklinkOpportunity>
        {
            new("bl_techcrunch_01", "techcrunch.com", 93, "https://techcrunch.com/best-saas-crm", "Identified", "autonomous growth operating system"),
            new("bl_g2_02", "g2.com", 89, "https://g2.com/categories/marketing-automation", "Pitched", "b2b growth OS with webrtc sdr"),
            new("bl_venturebeat_03", "venturebeat.com", 91, "https://venturebeat.com/ai-agents-b2b", "Secured", "multi-agent swarm coordination platform")
        };

        foreach (var bl in sampleBacklinks) _backlinks.Add(bl);
    }

    public List<ProgrammaticPage> GeneratePages(BatchGeneratePagesRequest request)
    {
        var created = new List<ProgrammaticPage>();

        foreach (var industry in request.Industries)
        {
            var cleanIndustry = industry.Trim();
            var slug = request.BaseSlugPattern.Replace("{industry}", cleanIndustry.ToLowerInvariant().Replace(" ", "-"));
            if (!slug.StartsWith("/")) slug = "/" + slug;

            var page = new ProgrammaticPage(
                PageId: "pg_" + Guid.NewGuid().ToString("N")[..8],
                Slug: slug,
                TargetKeyword: $"Best Growth CRM & Automation for {cleanIndustry}",
                Industry: cleanIndustry,
                MetaTitle: $"{cleanIndustry} Growth Automation & CRM | SGrowing",
                MetaDescription: $"Scale pipeline and customer acquisition in {cleanIndustry} with autonomous AI SDRs and multi-touch attribution.",
                H1Title: $"Autonomous Growth Infrastructure for {cleanIndustry}",
                SchemaJsonLd: $"{{\"@context\":\"https://schema.org\",\"@type\":\"SoftwareApplication\",\"name\":\"SGrowing {cleanIndustry} Suite\"}}",
                InternalLinks: new List<string> { "/features/webrtc-voice", "/pricing", "/features/swarm" },
                WordCount: 1520,
                Status: PagePublishStatus.Generated,
                PublishedAtUtc: DateTime.UtcNow
            );

            _pages.Add(page);
            created.Add(page);
        }

        return created;
    }

    public List<ProgrammaticPage> ListPages()
    {
        return _pages.OrderByDescending(p => p.PublishedAtUtc).ToList();
    }

    public List<BacklinkOpportunity> ListBacklinkOpportunities()
    {
        return _backlinks.OrderByDescending(b => b.DomainAuthority).ToList();
    }

    public IndexNowSubmissionResult SubmitToIndexNow(List<string> pageUrls)
    {
        return new IndexNowSubmissionResult(
            Engine: "IndexNow-Mesh (Bing/Yandex/Seznam)",
            UrlsSubmitted: pageUrls.Count,
            ResponseCode: "202 Accepted",
            SubmittedAtUtc: DateTime.UtcNow
        );
    }
}
