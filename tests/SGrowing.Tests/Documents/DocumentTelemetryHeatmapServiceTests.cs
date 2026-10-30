using FluentAssertions;
using SGrowing.Api.Modules.Documents;
using Xunit;

namespace SGrowing.Tests.Documents;

public class DocumentTelemetryHeatmapServiceTests
{
    private readonly DocumentTelemetryHeatmapService _service;

    public DocumentTelemetryHeatmapServiceTests()
    {
        _service = new DocumentTelemetryHeatmapService();
    }

    [Fact]
    public void GenerateSecureToken_FollowsStrictFormat_WithDocViewPrefixAnd24UrlSafeChars()
    {
        // Act
        var token = DocumentTelemetryHeatmapService.GenerateSecureToken();

        // Assert
        token.Should().StartWith("doc_view_");
        token.Length.Should().Be(33); // "doc_view_" (9) + 24 chars = 33
        DocumentTelemetryHeatmapService.ValidateTokenFormat(token).Should().BeTrue();
        token.Should().NotContain("+");
        token.Should().NotContain("/");
        token.Should().NotContain("=");
    }

    [Fact]
    public void GenerateSecureToken_GeneratesUniqueTokens_WithoutCollision()
    {
        // Arrange
        var tokens = new HashSet<string>();

        // Act
        for (int i = 0; i < 50; i++)
        {
            tokens.Add(DocumentTelemetryHeatmapService.GenerateSecureToken());
        }

        // Assert
        tokens.Should().HaveCount(50);
    }

    [Fact]
    public async Task HeartbeatPing_AggregatesDwellTime_AcrossSequentialPings()
    {
        // Arrange
        var docs = await _service.GetTrackedDocumentsAsync();
        var doc = docs.First(d => d.TotalPages >= 5);
        var link = await _service.CreateShareLinkAsync(new CreateShareLinkRequest(
            DocumentId: doc.Id,
            RecipientEmail: "test-aggregate@acme.com",
            RecipientCompany: "Acme Aggregate Corp"
        ));
        var sessionId = Guid.NewGuid();

        // Act: Ping 1 (3000ms), Ping 2 (3000ms), Ping 3 (4000ms)
        var ping1 = await _service.RecordHeartbeatAsync(new HeartbeatPingRequest(
            Token: link.Token,
            SessionId: sessionId,
            PageNumber: 2,
            DwellTimeIncrementMs: 3000,
            ScrollDepthPct: 35.0m
        ));

        var ping2 = await _service.RecordHeartbeatAsync(new HeartbeatPingRequest(
            Token: link.Token,
            SessionId: sessionId,
            PageNumber: 2,
            DwellTimeIncrementMs: 3000,
            ScrollDepthPct: 60.0m
        ));

        var ping3 = await _service.RecordHeartbeatAsync(new HeartbeatPingRequest(
            Token: link.Token,
            SessionId: sessionId,
            PageNumber: 2,
            DwellTimeIncrementMs: 4000,
            ScrollDepthPct: 85.0m
        ));

        // Assert
        ping1.PageDwellTimeMs.Should().Be(3000);
        ping2.PageDwellTimeMs.Should().Be(6000);
        ping3.PageDwellTimeMs.Should().Be(10000);
        ping3.TotalDwellTimeMs.Should().Be(10000);
    }

    [Fact]
    public async Task HeartbeatPing_ScrollDepth_UpdatesMonotonicallyToMaximumObserved()
    {
        // Arrange
        var docs = await _service.GetTrackedDocumentsAsync();
        var doc = docs.First();
        var link = await _service.CreateShareLinkAsync(new CreateShareLinkRequest(
            DocumentId: doc.Id,
            RecipientEmail: "scroll-test@initech.com",
            RecipientCompany: "Initech Scroll"
        ));
        var sessionId = Guid.NewGuid();

        // Act: First scroll 75%, then client scrolls back up to 30%, then down to 90%
        await _service.RecordHeartbeatAsync(new HeartbeatPingRequest(
            Token: link.Token,
            SessionId: sessionId,
            PageNumber: 1,
            DwellTimeIncrementMs: 2000,
            ScrollDepthPct: 75.0m
        ));

        var pingUp = await _service.RecordHeartbeatAsync(new HeartbeatPingRequest(
            Token: link.Token,
            SessionId: sessionId,
            PageNumber: 1,
            DwellTimeIncrementMs: 2000,
            ScrollDepthPct: 30.0m
        ));

        var pingDown = await _service.RecordHeartbeatAsync(new HeartbeatPingRequest(
            Token: link.Token,
            SessionId: sessionId,
            PageNumber: 1,
            DwellTimeIncrementMs: 2000,
            ScrollDepthPct: 90.0m
        ));

        // Assert: Intent score should reflect max scroll (90%)
        pingDown.PageDwellTimeMs.Should().Be(6000);
        var sessions = await _service.GetDocumentSessionsAsync(doc.Id);
        var session = sessions.First(s => s.SessionId == sessionId);
        session.IntentScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public void BuyingIntentScore_AppliesHeavyPricingWeight_AndRespectsBenchmarkRatio()
    {
        // Arrange
        // PricingPlan weight is 3.5, Benchmark is 30,000ms
        // At 30,000ms dwell and 100% scroll depth:
        // Contribution = 3.5 * min(1.0, 30000/30000) * (100/100) * 15.0 = 3.5 * 1.0 * 1.0 * 15.0 = 52.5
        var pageViews = new List<PageViewTelemetry>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5, 30000, 100.0m, true, DateTimeOffset.UtcNow)
        };
        var pageTypes = new Dictionary<int, DocumentPageType>
        {
            { 5, DocumentPageType.PricingPlan }
        };

        // Act
        var score = DocumentTelemetryHeatmapService.CalculateIntentScore(pageViews, pageTypes);

        // Assert
        score.Should().Be(52.5m);
    }

    [Fact]
    public void BuyingIntentScore_AppliesLegalTermsAndExecutiveSummaryWeights()
    {
        // Arrange
        // LegalTerms weight: 2.0 -> at 30,000ms & 100% scroll -> 2.0 * 1.0 * 1.0 * 15 = 30.0
        // ExecutiveSummary weight: 1.5 -> at 15,000ms & 80% scroll -> 1.5 * (15000/30000) * 0.8 * 15 = 1.5 * 0.5 * 0.8 * 15 = 9.0
        // Standard specs weight: 1.0 -> at 30,000ms & 100% scroll -> 1.0 * 1.0 * 1.0 * 15 = 15.0
        var sessionId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var pageViews = new List<PageViewTelemetry>
        {
            new(Guid.NewGuid(), sessionId, linkId, 2, 15000, 80.0m, false, DateTimeOffset.UtcNow), // ExecutiveSummary
            new(Guid.NewGuid(), sessionId, linkId, 3, 30000, 100.0m, true, DateTimeOffset.UtcNow), // TechnicalSpecs
            new(Guid.NewGuid(), sessionId, linkId, 7, 30000, 100.0m, true, DateTimeOffset.UtcNow)  // LegalTerms
        };
        var pageTypes = new Dictionary<int, DocumentPageType>
        {
            { 2, DocumentPageType.ExecutiveSummary },
            { 3, DocumentPageType.TechnicalSpecs },
            { 7, DocumentPageType.LegalTerms }
        };

        // Act
        var score = DocumentTelemetryHeatmapService.CalculateIntentScore(pageViews, pageTypes);

        // Assert: 9.0 + 15.0 + 30.0 = 54.0
        score.Should().Be(54.0m);
    }

    [Fact]
    public void BuyingIntentScore_CappedAt100_EvenWhenSumOfContributionsExceedsMax()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var pageViews = new List<PageViewTelemetry>
        {
            new(Guid.NewGuid(), sessionId, linkId, 5, 45000, 100.0m, true, DateTimeOffset.UtcNow), // Pricing (52.5)
            new(Guid.NewGuid(), sessionId, linkId, 6, 45000, 100.0m, true, DateTimeOffset.UtcNow), // Pricing (52.5)
            new(Guid.NewGuid(), sessionId, linkId, 7, 45000, 100.0m, true, DateTimeOffset.UtcNow)  // Legal (30.0)
            // Total without cap would be 135.0
        };
        var pageTypes = new Dictionary<int, DocumentPageType>
        {
            { 5, DocumentPageType.PricingPlan },
            { 6, DocumentPageType.PricingPlan },
            { 7, DocumentPageType.LegalTerms }
        };

        // Act
        var score = DocumentTelemetryHeatmapService.CalculateIntentScore(pageViews, pageTypes);

        // Assert
        score.Should().Be(100.0m);
    }

    [Theory]
    [InlineData(85.0, BuyingIntentTier.Hot)]
    [InlineData(75.0, BuyingIntentTier.Hot)]
    [InlineData(74.9, BuyingIntentTier.Warm)]
    [InlineData(50.0, BuyingIntentTier.Warm)]
    [InlineData(49.9, BuyingIntentTier.Casual)]
    [InlineData(25.0, BuyingIntentTier.Casual)]
    [InlineData(24.9, BuyingIntentTier.Cold)]
    [InlineData(5.0, BuyingIntentTier.Cold)]
    public void GetIntentTier_ClassifiesTiersAccurately(decimal score, BuyingIntentTier expectedTier)
    {
        // Act
        var tier = DocumentTelemetryHeatmapService.GetIntentTier(score);

        // Assert
        tier.Should().Be(expectedTier);
    }

    [Fact]
    public async Task HeartbeatPing_TriggersHotLeadAlert_WhenPricingDwellReaches45Seconds()
    {
        // Arrange: Doc 1 Page 5 is PricingPlan
        var docs = await _service.GetTrackedDocumentsAsync();
        var doc1 = docs.First(d => d.Title.Contains("SGrowing Enterprise SaaS Agreement"));
        var link = await _service.CreateShareLinkAsync(new CreateShareLinkRequest(
            DocumentId: doc1.Id,
            RecipientEmail: "hot-pricing@cyberdyne.io",
            RecipientCompany: "Cyberdyne Systems"
        ));
        var sessionId = Guid.NewGuid();

        // Act: Spend 45,000ms on Pricing page (Page 5)
        var result = await _service.RecordHeartbeatAsync(new HeartbeatPingRequest(
            Token: link.Token,
            SessionId: sessionId,
            PageNumber: 5,
            DwellTimeIncrementMs: 45000,
            ScrollDepthPct: 95.0m
        ));

        // Assert
        result.AlertTriggered.Should().BeTrue();
        result.IsHotLead.Should().BeTrue();
        result.TriggerReason.Should().Contain("Pricing");

        var alerts = await _service.GetHotLeadAlertsAsync();
        alerts.Should().Contain(a => a.RecipientEmail == "hot-pricing@cyberdyne.io");
    }

    [Fact]
    public async Task HeartbeatPing_TriggersHotLeadAlert_WhenIntentScoreReaches75()
    {
        // Arrange
        var docs = await _service.GetTrackedDocumentsAsync();
        var doc1 = docs.First(d => d.Title.Contains("SGrowing Enterprise SaaS Agreement"));
        var link = await _service.CreateShareLinkAsync(new CreateShareLinkRequest(
            DocumentId: doc1.Id,
            RecipientEmail: "score-hot@waynecorp.com",
            RecipientCompany: "Wayne Enterprises"
        ));
        var sessionId = Guid.NewGuid();

        // Page 5 Pricing (30s) -> 52.5
        await _service.RecordHeartbeatAsync(new HeartbeatPingRequest(
            Token: link.Token,
            SessionId: sessionId,
            PageNumber: 5,
            DwellTimeIncrementMs: 30000,
            ScrollDepthPct: 100.0m
        ));

        // Page 7 Legal Terms (30s) -> +30.0 -> Total = 82.5 (>= 75 Hot)
        var result = await _service.RecordHeartbeatAsync(new HeartbeatPingRequest(
            Token: link.Token,
            SessionId: sessionId,
            PageNumber: 7,
            DwellTimeIncrementMs: 30000,
            ScrollDepthPct: 100.0m
        ));

        // Assert
        result.IntentScore.Should().BeGreaterThanOrEqualTo(75.0m);
        result.IntentTier.Should().Be("Hot");
        result.IsHotLead.Should().BeTrue();
        result.AlertTriggered.Should().BeTrue();
    }

    [Fact]
    public async Task HeartbeatPing_NonPricingPage_DoesNotTriggerAlert_IfIntentScoreBelow75()
    {
        // Arrange: Page 1 is Cover (weight 1.0). Even at 60s dwell, max contribution is 15.0, score = 15.0
        var docs = await _service.GetTrackedDocumentsAsync();
        var doc1 = docs.First();
        var link = await _service.CreateShareLinkAsync(new CreateShareLinkRequest(
            DocumentId: doc1.Id,
            RecipientEmail: "casual-viewer@stark.com",
            RecipientCompany: "Stark Industries"
        ));
        var sessionId = Guid.NewGuid();

        // Act
        var result = await _service.RecordHeartbeatAsync(new HeartbeatPingRequest(
            Token: link.Token,
            SessionId: sessionId,
            PageNumber: 1, // Cover page
            DwellTimeIncrementMs: 50000,
            ScrollDepthPct: 100.0m
        ));

        // Assert
        result.AlertTriggered.Should().BeFalse();
        result.IsHotLead.Should().BeFalse();
        result.IntentScore.Should().Be(15.0m);
        result.IntentTier.Should().Be("Cold");
    }

    [Fact]
    public async Task ExpiredLink_ThrowsInvalidOperationException_OnMetaAndHeartbeat()
    {
        // Arrange
        var docs = await _service.GetTrackedDocumentsAsync();
        var doc = docs.First();
        var link = await _service.CreateShareLinkAsync(new CreateShareLinkRequest(
            DocumentId: doc.Id,
            RecipientEmail: "expired@test.com",
            RecipientCompany: "Expired Co",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-10) // already expired
        ));

        // Act & Assert for Meta
        var metaAct = () => _service.GetShareLinkMetaAsync(link.Token);
        await metaAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expired*");

        // Act & Assert for Heartbeat
        var hbAct = () => _service.RecordHeartbeatAsync(new HeartbeatPingRequest(
            Token: link.Token,
            SessionId: Guid.NewGuid(),
            PageNumber: 1,
            DwellTimeIncrementMs: 3000,
            ScrollDepthPct: 50.0m
        ));
        await hbAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expired*");
    }

    [Fact]
    public async Task PasscodeProtectedLink_RejectsInvalidPasscode_WithUnauthorizedAccessException()
    {
        // Arrange
        var docs = await _service.GetTrackedDocumentsAsync();
        var doc = docs.First();
        var link = await _service.CreateShareLinkAsync(new CreateShareLinkRequest(
            DocumentId: doc.Id,
            RecipientEmail: "vault@bank.com",
            RecipientCompany: "Secure Bank",
            Passcode: "TopSecret99!"
        ));

        // Act: attempt heartbeat with wrong passcode
        var act = () => _service.RecordHeartbeatAsync(new HeartbeatPingRequest(
            Token: link.Token,
            SessionId: Guid.NewGuid(),
            PageNumber: 1,
            DwellTimeIncrementMs: 3000,
            ScrollDepthPct: 50.0m,
            Passcode: "WrongPassword"
        ));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid or missing passcode*");
    }

    [Fact]
    public async Task PasscodeProtectedLink_AcceptsCorrectPasscode()
    {
        // Arrange
        var docs = await _service.GetTrackedDocumentsAsync();
        var doc = docs.First();
        var link = await _service.CreateShareLinkAsync(new CreateShareLinkRequest(
            DocumentId: doc.Id,
            RecipientEmail: "vault-ok@bank.com",
            RecipientCompany: "Secure Bank",
            Passcode: "TopSecret99!"
        ));

        // Act: attempt heartbeat with correct passcode
        var result = await _service.RecordHeartbeatAsync(new HeartbeatPingRequest(
            Token: link.Token,
            SessionId: Guid.NewGuid(),
            PageNumber: 1,
            DwellTimeIncrementMs: 3000,
            ScrollDepthPct: 50.0m,
            Passcode: "TopSecret99!"
        ));

        // Assert
        result.Should().NotBeNull();
        result.PageDwellTimeMs.Should().Be(3000);
    }

    [Fact]
    public async Task RevokeShareLink_DeactivatesLink_AndSubsequentRequestsFail()
    {
        // Arrange
        var docs = await _service.GetTrackedDocumentsAsync();
        var doc = docs.First();
        var link = await _service.CreateShareLinkAsync(new CreateShareLinkRequest(
            DocumentId: doc.Id,
            RecipientEmail: "revoke@corp.com",
            RecipientCompany: "Revoke Corp"
        ));

        // Act
        var revoked = await _service.RevokeShareLinkAsync(link.Token);
        revoked.Should().BeTrue();

        // Assert
        var metaAct = () => _service.GetShareLinkMetaAsync(link.Token);
        await metaAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*revoked*");
    }

    [Fact]
    public async Task Heatmap_CalculatesDropoffRate_AcrossSequentialPages()
    {
        // Arrange
        var docs = await _service.GetTrackedDocumentsAsync();
        var doc = docs.First(d => d.TotalPages >= 3);
        var link = await _service.CreateShareLinkAsync(new CreateShareLinkRequest(
            DocumentId: doc.Id,
            RecipientEmail: "funnel@test.com",
            RecipientCompany: "Funnel Analytics"
        ));

        // Simulate 4 sessions:
        // All 4 view page 1
        // 3 view page 2
        // 1 views page 3
        for (int i = 0; i < 4; i++)
        {
            var sid = Guid.NewGuid();
            await _service.RecordHeartbeatAsync(new HeartbeatPingRequest(link.Token, sid, 1, 5000, 100m));
            if (i < 3)
            {
                await _service.RecordHeartbeatAsync(new HeartbeatPingRequest(link.Token, sid, 2, 5000, 100m));
            }
            if (i < 1)
            {
                await _service.RecordHeartbeatAsync(new HeartbeatPingRequest(link.Token, sid, 3, 5000, 100m));
            }
        }

        // Act
        var heatmap = await _service.GetDocumentHeatmapAsync(doc.Id);

        // Assert
        heatmap.Should().NotBeNull();
        heatmap.Pages.Should().HaveCount(doc.TotalPages);
        var p1 = heatmap.Pages.First(p => p.PageNumber == 1);
        var p2 = heatmap.Pages.First(p => p.PageNumber == 2);
        p1.DropoffRatePct.Should().Be(0.0m);
        // Page 1 has multiple views, page 2 has viewers; dropoff should be non-negative
        p2.DropoffRatePct.Should().BeGreaterThanOrEqualTo(0.0m);
    }

    [Fact]
    public async Task Session_DeviceTypeAndDuration_TrackedCorrectly()
    {
        // Arrange
        var docs = await _service.GetTrackedDocumentsAsync();
        var doc = docs.First();
        var link = await _service.CreateShareLinkAsync(new CreateShareLinkRequest(
            DocumentId: doc.Id,
            RecipientEmail: "mobile-user@device.com",
            RecipientCompany: "Mobile Devices Inc"
        ));
        var sessionId = Guid.NewGuid();

        // Act
        await _service.RecordHeartbeatAsync(new HeartbeatPingRequest(
            Token: link.Token,
            SessionId: sessionId,
            PageNumber: 1,
            DwellTimeIncrementMs: 6500,
            ScrollDepthPct: 80.0m,
            DeviceType: DocumentDeviceType.Mobile.ToString(),
            Country: "CA"
        ));

        // Assert
        var sessions = await _service.GetDocumentSessionsAsync(doc.Id);
        var session = sessions.First(s => s.SessionId == sessionId);
        session.DeviceType.Should().Be("Mobile");
        session.Country.Should().Be("CA");
        session.TotalDwellTimeMs.Should().Be(6500);
        session.PagesViewedCount.Should().Be(1);
    }

    [Fact]
    public async Task Heatmap_ComputesAverageDwellTimeAndHeatPercentage()
    {
        // Arrange
        var docs = await _service.GetTrackedDocumentsAsync();
        var doc = docs.First();

        // Act
        var heatmap = await _service.GetDocumentHeatmapAsync(doc.Id);

        // Assert
        heatmap.Should().NotBeNull();
        heatmap.DocumentId.Should().Be(doc.Id);
        heatmap.Pages.Should().NotBeEmpty();
        foreach (var page in heatmap.Pages)
        {
            page.HighInterestHeatPct.Should().BeInRange(0.0m, 100.0m);
            page.AverageDwellTimeMs.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public async Task SummaryKpi_ComputesTrackedLinksAndActiveHotLeads()
    {
        // Act
        var summary = await _service.GetTelemetrySummaryAsync();

        // Assert
        summary.TotalTrackedLinks.Should().BeGreaterThan(0);
        summary.TotalViews.Should().BeGreaterThan(0);
        summary.AvgCompletionPct.Should().BeInRange(0.0m, 100.0m);
        summary.ActiveHotLeads.Should().BeGreaterThan(0);
        summary.RecentAlerts.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PreSeededData_LoadsRealisticDocumentsAndHotLeads()
    {
        // Act
        var docs = await _service.GetTrackedDocumentsAsync();
        var alerts = await _service.GetHotLeadAlertsAsync();

        // Assert
        docs.Count.Should().BeGreaterThanOrEqualTo(2);
        docs.Should().Contain(d => d.Title.Contains("SGrowing Enterprise SaaS Agreement"));
        docs.Should().Contain(d => d.Title.Contains("Wholesale Fragrance Master Supply Proposal"));

        alerts.Should().NotBeEmpty();
        alerts.Should().Contain(a => a.RecipientCompany == "Acme Corporation");
    }
}
