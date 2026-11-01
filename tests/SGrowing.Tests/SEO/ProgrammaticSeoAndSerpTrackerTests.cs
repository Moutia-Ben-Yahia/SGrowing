using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.SEO;
using Xunit;

namespace SGrowing.Tests.SEO;

public class ProgrammaticSeoAndSerpTrackerTests
{
    private readonly ProgrammaticSeoGeneratorService _generator = new();
    private readonly SchemaOrgJsonLdGeneratorService _schemaGenerator = new();

    private static SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    #region 1. Programmatic SEO Permutations & Token Engine Tests

    [Fact]
    public void GenerateBatch_WithNxMDimensions_ProducesExactPermutationCount()
    {
        // Arrange: 2 Services x 4 Locations = 8 landing pages
        var request = new PSeoBatchRequest(
            Services: ["Emergency AC Repair", "Furnace Installation"],
            Locations: ["Algiers", "Oran", "Constantine", "Annaba"],
            BaseUrl: "https://myservice.dz"
        );

        // Act
        var result = _generator.GenerateBatch(request);

        // Assert
        result.TotalGenerated.Should().Be(8);
        result.ServicesCount.Should().Be(2);
        result.LocationsCount.Should().Be(4);
        result.Blueprints.Should().HaveCount(8);
    }

    [Fact]
    public void GenerateBatch_ProducesCorrectSlugs_SanitizedAndHyphenated()
    {
        // Arrange
        var request = new PSeoBatchRequest(
            Services: ["Emergency AC Repair!"],
            Locations: ["Algiers & Blida"],
            BaseUrl: "https://myservice.dz"
        );

        // Act
        var result = _generator.GenerateBatch(request);

        // Assert
        result.Blueprints.Should().ContainSingle();
        var blueprint = result.Blueprints[0];
        blueprint.Slug.Should().Be("emergency-ac-repair-algiers-blida");
        blueprint.CanonicalUrl.Should().Be("https://myservice.dz/services/emergency-ac-repair-algiers-blida");
    }

    [Fact]
    public void GenerateBatch_GeneratesLocalizedTitles_WithServiceCityAndIntentHook()
    {
        // Arrange
        var request = new PSeoBatchRequest(
            Services: ["Plumbing Repair"],
            Locations: ["Oran"],
            BaseUrl: "https://myservice.dz",
            TitleTemplate: "{{service}} in {{city}} | 24/7 Fast Response & Certified Experts"
        );

        // Act
        var result = _generator.GenerateBatch(request);

        // Assert
        var blueprint = result.Blueprints.Single();
        blueprint.Title.Should().Be("Plumbing Repair in Oran | 24/7 Fast Response & Certified Experts");
        blueprint.H1Heading.Should().Contain("Plumbing Repair").And.Contain("Oran");
    }

    [Fact]
    public void GenerateBatch_GeneratesMetaDescription_WithCtrBoostTokens()
    {
        // Arrange
        var request = new PSeoBatchRequest(
            Services: ["Furnace Maintenance"],
            Locations: ["Constantine"],
            DefaultPhone: "+213 555 0199",
            Year: 2026
        );

        // Act
        var result = _generator.GenerateBatch(request);

        // Assert
        var bp = result.Blueprints.Single();
        bp.MetaDescription.Should().Contain("Furnace Maintenance");
        bp.MetaDescription.Should().Contain("Constantine");
        bp.MetaDescription.Should().Contain("2026");
        bp.MetaDescription.Should().Contain("+213 555 0199");
    }

    [Fact]
    public void GenerateBatch_GeneratesCanonicalUrls_MatchingBaseUrlAndSlug()
    {
        // Arrange
        var request = new PSeoBatchRequest(
            Services: ["Roofing"],
            Locations: ["Annaba"],
            BaseUrl: "https://algeria-repairs.com/"
        );

        // Act
        var result = _generator.GenerateBatch(request);

        // Assert
        var bp = result.Blueprints.Single();
        bp.CanonicalUrl.Should().Be("https://algeria-repairs.com/services/roofing-annaba");
    }

    [Fact]
    public void GenerateBatch_GeneratesLocalizedFaqs_ReferencingServiceAndCity()
    {
        // Arrange
        var request = new PSeoBatchRequest(
            Services: ["Electrical Troubleshooting"],
            Locations: ["Setif"],
            DefaultPhone: "+213 555 0123",
            Year: 2026
        );

        // Act
        var result = _generator.GenerateBatch(request);

        // Assert
        var bp = result.Blueprints.Single();
        bp.Faqs.Should().HaveCount(4);
        bp.Faqs[0].Question.Should().Contain("Electrical Troubleshooting").And.Contain("Setif");
        bp.Faqs[1].Question.Should().Contain("cost").And.Contain("2026");
        bp.Faqs[3].Answer.Should().Contain("+213 555 0123");
    }

    [Fact]
    public void GenerateBatch_GeneratesLocalizedPricingTable_WithCurrencyAndTiers()
    {
        // Arrange
        var request = new PSeoBatchRequest(
            Services: ["Drain Cleaning"],
            Locations: ["Batna"]
        );

        // Act
        var result = _generator.GenerateBatch(request);

        // Assert
        var bp = result.Blueprints.Single();
        bp.PricingTiers.Should().HaveCount(3);
        bp.PricingTiers[0].Price.Should().Be(49.00m);
        bp.PricingTiers[1].Price.Should().Be(149.00m);
        bp.PricingTiers[2].Price.Should().Be(249.00m);
        bp.PricingTiers[1].Description.Should().Contain("Drain Cleaning").And.Contain("Batna");
    }

    [Fact]
    public void GenerateBatch_WithCustomFaqsAndPricingTemplates_OverridesDefaults()
    {
        // Arrange
        var customFaqs = new List<PSeoFaqTemplate>
        {
            new("Is {{service}} covered by insurance in {{city}}?", "Yes, in {{city}} we assist with direct insurer billing.")
        };
        var customPricing = new List<PSeoPricingTierTemplate>
        {
            new("Commercial {{service}} Plan", 499.00m, "DZD", "Comprehensive package for {{city}} facilities.", ["All-inclusive", "Priority SLA"])
        };

        var request = new PSeoBatchRequest(
            Services: ["HVAC Duct Sanitization"],
            Locations: ["Tlemcen"],
            CustomFaqTemplates: customFaqs,
            CustomPricingTemplates: customPricing
        );

        // Act
        var result = _generator.GenerateBatch(request);

        // Assert
        var bp = result.Blueprints.Single();
        bp.Faqs.Should().ContainSingle();
        bp.Faqs[0].Question.Should().Be("Is HVAC Duct Sanitization covered by insurance in Tlemcen?");
        bp.Faqs[0].Answer.Should().Be("Yes, in Tlemcen we assist with direct insurer billing.");

        bp.PricingTiers.Should().ContainSingle();
        bp.PricingTiers[0].TierName.Should().Be("Commercial HVAC Duct Sanitization Plan");
        bp.PricingTiers[0].Currency.Should().Be("DZD");
        bp.PricingTiers[0].Price.Should().Be(499.00m);
    }

    [Fact]
    public void GenerateBatch_WithEmptyOrWhitespaceInputs_SkipsInvalidEntries()
    {
        // Arrange
        var request = new PSeoBatchRequest(
            Services: ["Emergency AC Repair", "", "  "],
            Locations: ["Algiers", "  ", "Oran"]
        );

        // Act
        var result = _generator.GenerateBatch(request);

        // Assert: 1 valid service x 2 valid locations = 2 blueprints
        result.TotalGenerated.Should().Be(2);
    }

    [Fact]
    public void ReplaceTokens_ReplacesServiceCityYearAndPhone_Successfully()
    {
        // Arrange
        var template = "Call {{phone}} for best {{service}} in {{city}} ({{year}})";
        var tokens = new Dictionary<string, string>
        {
            ["phone"] = "+1 800 555 1234",
            ["service"] = "Carpet Cleaning",
            ["city"] = "Austin",
            ["year"] = "2026"
        };

        // Act
        var resolved = _generator.ReplaceTokens(template, tokens);

        // Assert
        resolved.Should().Be("Call +1 800 555 1234 for best Carpet Cleaning in Austin (2026)");
    }

    [Fact]
    public void ReplaceTokens_UsesFallbackTokens_WhenPrimaryTokenMissing()
    {
        // Arrange
        var template = "{{service}} in {{city}} - Hotline: {{phone}}";
        var tokens = new Dictionary<string, string>
        {
            ["service"] = "Towing Service",
            ["city"] = "Denver"
        };
        var fallbackTokens = new Dictionary<string, string>
        {
            ["phone"] = "+1 800 999 8888"
        };

        // Act
        var resolved = _generator.ReplaceTokens(template, tokens, fallbackTokens);

        // Assert
        resolved.Should().Be("Towing Service in Denver - Hotline: +1 800 999 8888");
    }

    [Fact]
    public void ReplaceTokens_StripsUnmatchedTokensCleanly_WithoutLeavingBraces()
    {
        // Arrange
        var template = "{{service}} in {{city}} {{missing_token}}";
        var tokens = new Dictionary<string, string>
        {
            ["service"] = "Locksmith",
            ["city"] = "Chicago"
        };

        // Act
        var resolved = _generator.ReplaceTokens(template, tokens);

        // Assert
        resolved.Should().Be("Locksmith in Chicago");
        resolved.Should().NotContain("{{");
        resolved.Should().NotContain("}}");
    }

    [Fact]
    public void GenerateSlug_HandlesSpecialCharactersAccentsAndMultipleSpaces()
    {
        // Arrange
        var service = "Café & Crêpe    Catering!";
        var city = "Montréal (Downtown)";

        // Act
        var slug = _generator.GenerateSlug(service, city);

        // Assert: accents stripped, non-alphanumeric removed, collapsed to single hyphens
        slug.Should().Be("cafe-crepe-catering-montreal-downtown");
    }

    #endregion

    #region 2. Schema.org JSON-LD Generation & Google Rich Snippets Validation

    [Fact]
    public void SchemaOrg_GenerateLocalBusinessSchema_ProducesValidJsonLdWithRequiredFields()
    {
        // Arrange
        var request = new LocalBusinessSchemaRequest(
            Name: "Algiers Rapid AC Repair",
            Telephone: "+213 555 0199",
            Address: new PostalAddressDto(
                StreetAddress: "14 Didouche Mourad",
                AddressLocality: "Algiers",
                AddressRegion: "Algiers",
                PostalCode: "16000",
                AddressCountry: "DZ"
            ),
            PriceRange: "$$",
            OpeningHours: ["Mo-Su 00:00-24:00"],
            GeoCoordinates: new GeoCoordinatesDto(36.7538, 3.0588),
            BusinessType: "HVACBusiness"
        );

        // Act
        var jsonLd = _schemaGenerator.GenerateLocalBusinessSchema(request);
        var validation = _schemaGenerator.ValidateJsonLd(jsonLd, "LocalBusiness");

        // Assert
        validation.IsValid.Should().BeTrue();
        validation.Errors.Should().BeEmpty();
        jsonLd.Should().Contain("\"@context\": \"https://schema.org\"");
        jsonLd.Should().Contain("\"@type\": \"HVACBusiness\"");
        jsonLd.Should().Contain("\"name\": \"Algiers Rapid AC Repair\"");
        jsonLd.Should().Contain("\"telephone\": \"+213 555 0199\"");
        jsonLd.Should().Contain("\"latitude\": 36.7538");
    }

    [Fact]
    public void SchemaOrg_GenerateFaqSchema_ProducesValidFaqPageJsonLdWithQuestionAnswerArray()
    {
        // Arrange
        var request = new FaqSchemaRequest(
            Questions:
            [
                new FaqItemDto("What are your emergency response hours?", "We operate 24/7 365 days a year."),
                new FaqItemDto("Do you offer free quotes?", "Yes, all diagnostic assessments include written estimates.")
            ]
        );

        // Act
        var jsonLd = _schemaGenerator.GenerateFaqSchema(request);
        var validation = _schemaGenerator.ValidateJsonLd(jsonLd, "FAQPage");

        // Assert
        validation.IsValid.Should().BeTrue();
        validation.Errors.Should().BeEmpty();
        jsonLd.Should().Contain("\"@type\": \"FAQPage\"");
        jsonLd.Should().Contain("\"mainEntity\":");
        jsonLd.Should().Contain("\"acceptedAnswer\":");
    }

    [Fact]
    public void SchemaOrg_GenerateProductSchema_ProducesValidProductJsonLdWithInStockOfferAndRating()
    {
        // Arrange
        var request = new ProductSchemaRequest(
            Name: "Emergency AC Compressor Replacement",
            Description: "Full service compressor repair with 1-year parts warranty.",
            Offer: new ProductOfferDto(
                Price: 249.00m,
                PriceCurrency: "USD",
                Availability: "InStock",
                Url: "https://myservice.dz/services/ac-repair"
            ),
            AggregateRating: new AggregateRatingDto(
                RatingValue: 4.9,
                ReviewCount: 88,
                BestRating: 5.0,
                WorstRating: 1.0
            ),
            Brand: "SGrowing Climate Care",
            Sku: "HVAC-COMP-01"
        );

        // Act
        var jsonLd = _schemaGenerator.GenerateProductSchema(request);
        var validation = _schemaGenerator.ValidateJsonLd(jsonLd, "Product");

        // Assert
        validation.IsValid.Should().BeTrue();
        validation.Errors.Should().BeEmpty();
        jsonLd.Should().Contain("\"@type\": \"Product\"");
        jsonLd.Should().Contain("\"price\": 249");
        jsonLd.Should().Contain("\"https://schema.org/InStock\"");
        jsonLd.Should().Contain("\"ratingValue\": 4.9");
    }

    [Fact]
    public void SchemaOrg_GenerateBreadcrumbSchema_ProducesSequentialBreadcrumbListStartingAtPositionOne()
    {
        // Arrange
        var request = new BreadcrumbSchemaRequest(
            Items:
            [
                new BreadcrumbItemDto(1, "Home", "https://myservice.dz"),
                new BreadcrumbItemDto(2, "Services", "https://myservice.dz/services"),
                new BreadcrumbItemDto(3, "AC Repair in Algiers", "https://myservice.dz/services/emergency-ac-repair-algiers")
            ]
        );

        // Act
        var jsonLd = _schemaGenerator.GenerateBreadcrumbSchema(request);
        var validation = _schemaGenerator.ValidateJsonLd(jsonLd, "BreadcrumbList");

        // Assert
        validation.IsValid.Should().BeTrue();
        validation.Errors.Should().BeEmpty();
        jsonLd.Should().Contain("\"@type\": \"BreadcrumbList\"");
        jsonLd.Should().Contain("\"position\": 1");
        jsonLd.Should().Contain("\"position\": 2");
        jsonLd.Should().Contain("\"position\": 3");
    }

    [Fact]
    public void SchemaOrg_ValidateJsonLd_FailsWhenRequiredFieldIsMissing()
    {
        // Arrange: Missing name for LocalBusiness
        var invalidJson = """
        {
          "@context": "https://schema.org",
          "@type": "LocalBusiness",
          "telephone": "+123456789"
        }
        """;

        // Act
        var validation = _schemaGenerator.ValidateJsonLd(invalidJson, "LocalBusiness");

        // Assert
        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(e => e.Contains("name"));
    }

    [Fact]
    public void SchemaOrg_ValidateJsonLd_FailsOnMalformedJson()
    {
        // Arrange
        var malformedJson = "{ @context: https://schema.org broken json }";

        // Act
        var validation = _schemaGenerator.ValidateJsonLd(malformedJson, "LocalBusiness");

        // Assert
        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(e => e.Contains("Malformed JSON-LD"));
    }

    #endregion

    #region 3. SERP Position Tracking, Delta Displacement & Volatility Tests

    [Fact]
    public void SerpTracker_ComputeDisplacementDelta_CalculatesPositiveGainOnImprovement()
    {
        using var db = CreateDbContext();
        var tracker = new SerpPositionTrackerService(db);

        // Day 1: Rank #14, Day 2: Rank #4 -> Improvement of 10 positions
        var delta = tracker.ComputeDisplacementDelta(previousRank: 14, currentRank: 4);

        delta.Should().Be(10);
    }

    [Fact]
    public void SerpTracker_ComputeDisplacementDelta_CalculatesNegativeDropOnDecline()
    {
        using var db = CreateDbContext();
        var tracker = new SerpPositionTrackerService(db);

        // Day 1: Rank #4, Day 2: Rank #11 -> Drop of 7 positions
        var delta = tracker.ComputeDisplacementDelta(previousRank: 4, currentRank: 11);

        delta.Should().Be(-7);
    }

    [Fact]
    public void SerpTracker_ComputeDisplacementDelta_ReturnsZeroWhenNoPreviousRank()
    {
        using var db = CreateDbContext();
        var tracker = new SerpPositionTrackerService(db);

        var delta = tracker.ComputeDisplacementDelta(previousRank: 0, currentRank: 5);

        delta.Should().Be(0);
    }

    [Fact]
    public void SerpTracker_DetectMilestones_FiresEnteredTopThree_WhenRankCrossesThreshold()
    {
        using var db = CreateDbContext();
        var tracker = new SerpPositionTrackerService(db);

        var triggers = tracker.DetectMilestones(previousRank: 6, currentRank: 2);

        triggers.Should().Contain(t => t.MilestoneType == "EnteredTopThree");
    }

    [Fact]
    public void SerpTracker_DetectMilestones_FiresEnteredTopTen_WhenRankCrossesThreshold()
    {
        using var db = CreateDbContext();
        var tracker = new SerpPositionTrackerService(db);

        var triggers = tracker.DetectMilestones(previousRank: 14, currentRank: 7);

        triggers.Should().Contain(t => t.MilestoneType == "EnteredTopTen");
    }

    [Fact]
    public void SerpTracker_DetectMilestones_FiresDroppedOutOfTopTen_WhenRankDrops()
    {
        using var db = CreateDbContext();
        var tracker = new SerpPositionTrackerService(db);

        var triggers = tracker.DetectMilestones(previousRank: 8, currentRank: 13);

        triggers.Should().Contain(t => t.MilestoneType == "DroppedOutOfTopTen");
    }

    [Fact]
    public void SerpTracker_DetectMilestones_FiresReachedNumberOne_WhenRankBecomesOne()
    {
        using var db = CreateDbContext();
        var tracker = new SerpPositionTrackerService(db);

        var triggers = tracker.DetectMilestones(previousRank: 3, currentRank: 1);

        triggers.Should().Contain(t => t.MilestoneType == "ReachedNumberOne");
    }

    [Fact]
    public void SerpTracker_DetectMilestones_FiresSignificantRankDrop_WhenDropIsGreaterThanOrEqualToFive()
    {
        using var db = CreateDbContext();
        var tracker = new SerpPositionTrackerService(db);

        // Previous 3 -> Current 10 = Drop of 7
        var triggers = tracker.DetectMilestones(previousRank: 3, currentRank: 10);

        triggers.Should().Contain(t => t.MilestoneType == "SignificantRankDrop");
    }

    [Fact]
    public void SerpTracker_CalculateVolatility_ReturnsZeroForConstantRank()
    {
        using var db = CreateDbContext();
        var tracker = new SerpPositionTrackerService(db);

        var sequence = new List<int> { 3, 3, 3, 3, 3 };
        var analysis = tracker.CalculateVolatility(sequence);

        analysis.VolatilityIndex.Should().Be(0.0);
        analysis.MeanDisplacement.Should().Be(0.0);
        analysis.StabilityGrade.Should().Be("Rock-Solid");
    }

    [Fact]
    public void SerpTracker_CalculateVolatility_ReturnsHighVolatilityForFluctuatingRanks()
    {
        using var db = CreateDbContext();
        var tracker = new SerpPositionTrackerService(db);

        // Highly volatile sequence: 14 -> 3 -> 22 -> 4 -> 19
        var sequence = new List<int> { 14, 3, 22, 4, 19 };
        var analysis = tracker.CalculateVolatility(sequence);

        analysis.VolatilityIndex.Should().BeGreaterThan(50.0);
        analysis.MeanDisplacement.Should().BeGreaterThan(10.0);
        analysis.StabilityGrade.Should().BeOneOf("High Volatility", "Turbulent");
    }

    [Fact]
    public void SerpTracker_BenchmarkCompetitors_CalculatesAverageDaAndIdentifiesTitleLengths()
    {
        using var db = CreateDbContext();
        var tracker = new SerpPositionTrackerService(db);

        var competitors = new List<SerpCompetitorEntry>
        {
            new(1, "https://comp1.dz", "comp1.dz", 65, "Emergency AC Repair in Algiers | 24/7 Certified Experts", 54),
            new(2, "https://comp2.dz", "comp2.dz", 45, "Algiers AC & Heating Cooling Pro Technicians Available", 54),
            new(3, "https://comp3.dz", "comp3.dz", 70, "HVAC Services", 13) // short title
        };

        var benchmark = tracker.BenchmarkCompetitors(competitors);

        benchmark.AverageDomainAuthority.Should().Be(60.0); // (65+45+70)/3 = 60.0
        benchmark.MinDomainAuthority.Should().Be(45);
        benchmark.MaxDomainAuthority.Should().Be(70);
        benchmark.OptimalTitleCount.Should().Be(2); // comp1 and comp2 are 54 chars (between 50 and 60)
        benchmark.Insights.Should().NotBeEmpty();
    }

    [Fact]
    public void SerpTracker_BenchmarkCompetitors_HandlesEmptyListGracefully()
    {
        using var db = CreateDbContext();
        var tracker = new SerpPositionTrackerService(db);

        var benchmark = tracker.BenchmarkCompetitors([]);

        benchmark.AverageDomainAuthority.Should().Be(0.0);
        benchmark.OptimalTitleCount.Should().Be(0);
        benchmark.TopCompetitors.Should().BeEmpty();
    }

    [Fact]
    public void SerpTracker_GenerateRecommendations_WarnsOnCriticalRankDropGreaterThanFivePositions()
    {
        using var db = CreateDbContext();
        var tracker = new SerpPositionTrackerService(db);

        var recommendations = tracker.GenerateRecommendations(
            keyword: "emergency ac repair",
            currentRank: 12,
            previousRank: 4,
            volatilityIndex: 40.0,
            competitorBenchmark: null
        );

        recommendations.Should().Contain(r => r.Contains("dropped 8 positions") && r.Contains("canonical"));
    }

    [Fact]
    public void SerpTracker_GenerateRecommendations_WarnsOnHighVolatility()
    {
        using var db = CreateDbContext();
        var tracker = new SerpPositionTrackerService(db);

        var recommendations = tracker.GenerateRecommendations(
            keyword: "furnace repair",
            currentRank: 5,
            previousRank: 5,
            volatilityIndex: 58.0,
            competitorBenchmark: null
        );

        recommendations.Should().Contain(r => r.Contains("Elevated SERP volatility"));
    }

    [Fact]
    public async Task SerpTracker_TrackKeywordAsync_PersistsRecordAndReturnsAnalysis()
    {
        // Arrange
        using var db = CreateDbContext();
        var tracker = new SerpPositionTrackerService(db);
        var tenantId = Guid.NewGuid();

        // Track day 1: Rank #14
        await tracker.TrackKeywordAsync(new TrackKeywordRankCommand(
            TenantId: tenantId,
            Keyword: "ac repair algiers",
            TargetUrl: "https://myservice.dz/services/ac-repair-algiers",
            Position: 14
        ));

        // Track day 2: Rank #2 (improvement entering top 3 and top 10)
        var cmd = new TrackKeywordRankCommand(
            TenantId: tenantId,
            Keyword: "ac repair algiers",
            TargetUrl: "https://myservice.dz/services/ac-repair-algiers",
            Position: 2,
            Competitors:
            [
                new SerpCompetitorEntry(1, "https://c1.dz", "c1.dz", 55, "Emergency AC in Algiers", 23)
            ]
        );

        // Act
        var result = await tracker.TrackKeywordAsync(cmd);

        // Assert
        result.Should().NotBeNull();
        result.CurrentPosition.Should().Be(2);
        result.PreviousPosition.Should().Be(14);
        result.DeltaDisplacement.Should().Be(12);
        result.TriggeredMilestones.Should().Contain(m => m.MilestoneType == "EnteredTopTen");
        result.TriggeredMilestones.Should().Contain(m => m.MilestoneType == "EnteredTopThree");
        result.TriggeredMilestones.Should().Contain(m => m.MilestoneType == "SignificantRankJump");
        result.CompetitorBenchmark.Should().NotBeNull();

        // Verify DB persistence
        var dbRecords = await db.SerpRankHistoryRecords
            .Where(r => r.TenantId == tenantId)
            .ToListAsync();
        dbRecords.Should().HaveCount(2);
    }

    [Fact]
    public async Task SerpTracker_GetKeywordHistoryAsync_ReturnsChronologicalRecordsAndMilestones()
    {
        // Arrange
        using var db = CreateDbContext();
        var tracker = new SerpPositionTrackerService(db);
        var tenantId = Guid.NewGuid();
        var keywordId = Guid.NewGuid();

        // Track 3 sequential days: #14 -> #11 -> #4
        await tracker.TrackKeywordAsync(new TrackKeywordRankCommand(tenantId, "furnace installation", "https://myservice.dz/furnace", 14, KeywordId: keywordId));
        await tracker.TrackKeywordAsync(new TrackKeywordRankCommand(tenantId, "furnace installation", "https://myservice.dz/furnace", 11, KeywordId: keywordId));
        await tracker.TrackKeywordAsync(new TrackKeywordRankCommand(tenantId, "furnace installation", "https://myservice.dz/furnace", 4, KeywordId: keywordId));

        // Act
        var historyReport = await tracker.GetKeywordHistoryAsync(tenantId, keywordId, days: 30);

        // Assert
        historyReport.History.Should().HaveCount(3);
        historyReport.BestPosition.Should().Be(4);
        historyReport.CurrentPosition.Should().Be(4);
        historyReport.Milestones.Should().Contain(m => m.MilestoneType == "EnteredTopTen");
    }

    [Fact]
    public async Task SerpTracker_GetVolatilityReportAsync_AggregatesTenantKeywordsAndIdentifiesTopMovers()
    {
        // Arrange
        using var db = CreateDbContext();
        var tracker = new SerpPositionTrackerService(db);
        var tenantId = Guid.NewGuid();

        // Keyword 1: Climber (+8)
        await tracker.TrackKeywordAsync(new TrackKeywordRankCommand(tenantId, "ac maintenance", "https://myservice.dz/ac", 12));
        await tracker.TrackKeywordAsync(new TrackKeywordRankCommand(tenantId, "ac maintenance", "https://myservice.dz/ac", 4));

        // Keyword 2: Dropper (-6)
        await tracker.TrackKeywordAsync(new TrackKeywordRankCommand(tenantId, "boiler repair", "https://myservice.dz/boiler", 3));
        await tracker.TrackKeywordAsync(new TrackKeywordRankCommand(tenantId, "boiler repair", "https://myservice.dz/boiler", 9));

        // Act
        var report = await tracker.GetVolatilityReportAsync(tenantId, windowDays: 14);

        // Assert
        report.TotalKeywordsTracked.Should().Be(2);
        report.TopRisers.Should().Contain(r => r.Keyword == "ac maintenance" && r.Delta == 8);
        report.TopFallers.Should().Contain(f => f.Keyword == "boiler repair" && f.Delta == -6);
        report.ActionableRecommendations.Should().NotBeEmpty();
    }

    #endregion
}
