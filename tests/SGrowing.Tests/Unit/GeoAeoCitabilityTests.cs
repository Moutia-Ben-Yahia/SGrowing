using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.SEO;
using Xunit;

namespace SGrowing.Tests.Unit;

public class GeoAeoCitabilityTests
{
    private readonly GeoAeoCitabilityService _service = new();

    [Fact]
    public void AuditCitability_WithHighQualityAnswerEngineFormattedContent_AchievesHighScoresAndGrade()
    {
        var content = """
        # SGrowing Autonomous Growth Platform

        SGrowing is an autonomous growth operating system that provides unified omnichannel marketing, CRM pipelines, and COD e-commerce automation.

        ## What is Generative Engine Optimization (GEO)?
        Generative Engine Optimization is the methodology of structuring digital content so that artificial intelligence answer engines like Google Gemini, OpenAI Search, and Perplexity accurately cite and reference primary source data.

        ## Key Capabilities and Performance Metrics
        - Real-Time ROAS calculation across Meta, Google Ads, and TikTok.
        - Outbound cryptographic webhook event delivery mesh.
        - Carrier remittance reconciliation for Cash on Delivery couriers.

        | Capability | Throughput / SLA | Supported Engines |
        |---|---|---|
        | Clickstream Ingestion | 10,000 EPS | Kafka, ClickHouse |
        | Webhook Delivery | < 50ms P99 | HTTP/2, TLS 1.3 |
        """;

        var request = new GeoCitabilityAuditRequest(
            Title: "SGrowing Autonomous Growth Platform",
            Content: content,
            Url: "https://sgrowing.io/platform-overview",
            TargetEngine: "All"
        );

        var result = _service.AuditCitability(request);

        result.Should().NotBeNull();
        result.OverallCitabilityScore.Should().BeGreaterThan(75);
        result.CitabilityGrade.Should().Contain("High");
        result.PassageConcisenessScore.Should().BeGreaterThan(70);
        result.EntityDensityScore.Should().BeGreaterThan(0);
        result.StructuredFormatRatio.Should().BeGreaterThan(0);
        result.QandAAdjacencyScore.Should().BeGreaterThan(0);
        result.FactualClaimsCount.Should().BeGreaterThan(0);
        result.AmbiguousPronounOccurrences.Should().Be(0);
        result.FormattedLlmsSnippet.Should().Contain("SGrowing Autonomous Growth Platform");
    }

    [Fact]
    public void AuditCitability_WithAmbiguousPronounsAndNoStructure_FlagsDefectsAndGeneratesRecommendations()
    {
        var ambiguousContent = """
        It is very cool and it does a lot of things for businesses. 
        When people use this tool, they will see that it helps them make more money because they do not have to worry about all those complicated systems that they usually struggle with every single day when they try to do marketing or sales manually without any good software in place.
        """;

        var request = new GeoCitabilityAuditRequest(
            Title: "Vague Product Description",
            Content: ambiguousContent
        );

        var result = _service.AuditCitability(request);

        result.Should().NotBeNull();
        result.AmbiguousPronounOccurrences.Should().BeGreaterThan(3);
        result.OverallCitabilityScore.Should().BeLessThan(65);
        result.CitabilityGrade.Should().Contain("Needs Optimization");
        result.ActionableRecommendations.Should().Contain(r => r.Contains("ambiguous pronouns", StringComparison.OrdinalIgnoreCase));
        result.ActionableRecommendations.Should().Contain(r => r.Contains("bullet points") || r.Contains("tables"));
    }

    [Fact]
    public void GenerateLlmsTxt_CreatesStandardMarkdownSpecificationAndFullVariant()
    {
        var request = new LlmsTxtGenerationRequest(
            BrandName: "SGrowing OS",
            BrandTagline: "The Autonomous Enterprise Growth Operating System",
            PrimaryDomain: "https://sgrowing.io",
            CoreCapabilities: new List<string>
            {
                "Omnichannel CRM and lead routing",
                "Cryptographic outbound webhook mesh",
                "Autonomous SEO & AEO pipeline"
            },
            KeySections: new List<LlmsSectionDto>
            {
                new("Developer Platform API", "https://sgrowing.io/docs/api", "Full REST and MCP API documentation"),
                new("Webhooks Guide", "https://sgrowing.io/docs/webhooks", "HMAC-SHA256 signature verification and retry semantics")
            },
            OptionalFullDocumentationMarkdown: "# Full System Architecture\nDetails of microservices and streaming mesh."
        );

        var result = _service.GenerateLlmsTxt(request);

        result.Should().NotBeNull();
        result.LlmsTxt.Should().Contain("# SGrowing OS");
        result.LlmsTxt.Should().Contain("> The Autonomous Enterprise Growth Operating System");
        result.LlmsTxt.Should().Contain("## Capabilities");
        result.LlmsTxt.Should().Contain("- Omnichannel CRM and lead routing");
        result.LlmsTxt.Should().Contain("## Key Documentation & Endpoints");
        result.LlmsTxt.Should().Contain("- [Developer Platform API](https://sgrowing.io/docs/api): Full REST and MCP API documentation");

        result.LlmsFullTxt.Should().Contain("# SGrowing OS");
        result.LlmsFullTxt.Should().Contain("# Full System Architecture");
        result.LlmsFullTxt.Should().Contain("Details of microservices and streaming mesh.");
    }
}
