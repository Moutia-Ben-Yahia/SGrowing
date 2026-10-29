using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using SGrowing.Api.Data;
using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.Documents;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;
using Xunit;

namespace SGrowing.Tests.Documents;

public class ProposalPortalTelemetryTests
{
    private readonly SGrowingDbContext _db;
    private readonly Mock<IHubContext<OmniHub, IOmniClient>> _mockHubContext;
    private readonly Mock<IHubClients<IOmniClient>> _mockHubClients;
    private readonly Mock<IOmniClient> _mockClient;
    private readonly Mock<IOutbox> _mockOutbox;
    private readonly ProposalPortalService _portalService;

    public ProposalPortalTelemetryTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);

        _mockHubContext = new Mock<IHubContext<OmniHub, IOmniClient>>();
        _mockHubClients = new Mock<IHubClients<IOmniClient>>();
        _mockClient = new Mock<IOmniClient>();

        _mockHubClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClient.Object);
        _mockHubContext.Setup(h => h.Clients).Returns(_mockHubClients.Object);

        _mockOutbox = new Mock<IOutbox>();

        _portalService = new ProposalPortalService(_db, _mockHubContext.Object, _mockOutbox.Object);
    }

    [Fact]
    public async Task CreateShareToken_AndLookup_ReturnsPublicProposalWithForensicWatermark()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var envelopeId = Guid.NewGuid();

        var request = new CreateProposalShareTokenRequest(
            TenantId: tenantId,
            EnvelopeId: envelopeId,
            Title: "Enterprise Cloud Acceleration Proposal",
            RecipientName: "Sarah Connor",
            RecipientEmail: "sarah@cyberdyne.io",
            TotalPages: 4,
            PricingPageNumber: 3,
            Passcode: null,
            ValidDays: 14,
            SalesRepName: "Marcus Wright",
            SalesRepEmail: "marcus.wright@sgrowing.io",
            DealValue: 75000m,
            Currency: "USD"
        );

        // Act
        var token = await _portalService.CreateProposalShareTokenAsync(request);
        var proposal = await _portalService.GetPublicProposalByTokenAsync(token);

        // Assert
        Assert.NotNull(token);
        Assert.StartsWith("prop_sec_", token);
        Assert.NotNull(proposal);
        Assert.Equal(envelopeId, proposal.EnvelopeId);
        Assert.Equal(tenantId, proposal.TenantId);
        Assert.Equal("Enterprise Cloud Acceleration Proposal", proposal.Title);
        Assert.Equal("Sarah Connor", proposal.RecipientName);
        Assert.Equal("sarah@cyberdyne.io", proposal.RecipientEmail);
        Assert.Equal(4, proposal.TotalPages);
        Assert.Equal(3, proposal.PricingPageNumber);
        Assert.Equal(75000m, proposal.DealValue);
        Assert.False(proposal.IsPasswordProtected);
        Assert.False(proposal.IsExpired);

        // Assert Watermark Config
        Assert.NotNull(proposal.WatermarkConfig);
        Assert.True(proposal.WatermarkConfig.Enabled);
        Assert.Equal("sarah@cyberdyne.io", proposal.WatermarkConfig.RecipientEmail);
        Assert.Contains("sarah@cyberdyne.io", proposal.WatermarkConfig.WatermarkText);

        // Assert Generated Pages Structure
        Assert.Equal(4, proposal.Pages.Count);
        Assert.True(proposal.Pages[2].IsPricingPage); // Index 2 is Page 3
        Assert.False(proposal.Pages[0].IsPricingPage);
    }

    [Fact]
    public async Task TokenLookup_WithPasswordProtection_RequiresAndValidatesPasscode()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var envelopeId = Guid.NewGuid();

        var request = new CreateProposalShareTokenRequest(
            TenantId: tenantId,
            EnvelopeId: envelopeId,
            Title: "Confidential Series B Advisory Mandate",
            RecipientName: "Gordon Gekko",
            RecipientEmail: "gekko@wallstreet.com",
            Passcode: "GreedIsGood2026!",
            ValidDays: 7
        );

        var token = await _portalService.CreateProposalShareTokenAsync(request);

        // Act & Assert 1: Access without passcode throws UnauthorizedAccessException
        var exNoPass = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _portalService.GetPublicProposalByTokenAsync(token, null)
        );
        Assert.Contains("Password required", exNoPass.Message);

        // Act & Assert 2: Access with invalid passcode throws UnauthorizedAccessException
        var exWrongPass = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _portalService.GetPublicProposalByTokenAsync(token, "WrongPassword")
        );
        Assert.Contains("Invalid access password", exWrongPass.Message);

        // Act & Assert 3: Access with correct passcode succeeds
        var proposal = await _portalService.GetPublicProposalByTokenAsync(token, "GreedIsGood2026!");
        Assert.NotNull(proposal);
        Assert.Equal("Gordon Gekko", proposal.RecipientName);
        Assert.True(proposal.IsPasswordProtected);
    }

    [Fact]
    public async Task TokenLookup_WithExpiredLink_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var envelopeId = Guid.NewGuid();

        var request = new CreateProposalShareTokenRequest(
            TenantId: tenantId,
            EnvelopeId: envelopeId,
            Title: "Time-Sensitive Flash Deal",
            RecipientName: "Marty McFly",
            RecipientEmail: "marty@hillvalley.org",
            ValidDays: -1 // Expired yesterday
        );

        var token = await _portalService.CreateProposalShareTokenAsync(request);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _portalService.GetPublicProposalByTokenAsync(token)
        );
        Assert.Contains("expired", ex.Message);
    }

    [Fact]
    public async Task TokenLookup_WithInvalidOrMissingToken_ThrowsAppropriateException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _portalService.GetPublicProposalByTokenAsync("")
        );

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _portalService.GetPublicProposalByTokenAsync("prop_sec_unknown_nonexistent_token_999")
        );
    }

    [Fact]
    public async Task TelemetryBeacon_AggregatesDwell_CalculatesAttentionIndexAndDropOffFunnel()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var envelopeId = Guid.NewGuid();

        var token = await _portalService.CreateProposalShareTokenAsync(new CreateProposalShareTokenRequest(
            TenantId: tenantId,
            EnvelopeId: envelopeId,
            Title: "Multi-Page Enterprise Pitch",
            RecipientName: "Alex Mercer",
            RecipientEmail: "alex@gentek.com",
            TotalPages: 4,
            PricingPageNumber: 3
        ));

        // Simulate Reader 1: Reads Page 1 (20s), Page 2 (20s), Page 3 (20s)
        await _portalService.RecordViewerTelemetryBeaconAsync(token, new ProposalBeaconDto(
            PageNumber: 1,
            SecondsDwell: 20.0,
            ScrollDepthPercent: 100,
            ViewerIp: "192.168.1.50",
            ViewerEmail: "alex@gentek.com"
        ));
        await _portalService.RecordViewerTelemetryBeaconAsync(token, new ProposalBeaconDto(
            PageNumber: 2,
            SecondsDwell: 20.0,
            ScrollDepthPercent: 90,
            ViewerIp: "192.168.1.50",
            ViewerEmail: "alex@gentek.com"
        ));
        await _portalService.RecordViewerTelemetryBeaconAsync(token, new ProposalBeaconDto(
            PageNumber: 3,
            SecondsDwell: 20.0,
            ScrollDepthPercent: 100,
            ViewerIp: "192.168.1.50",
            ViewerEmail: "alex@gentek.com"
        ));

        // Simulate Reader 2: Reads Page 1 (15s), Page 2 (15s) and drops off before Page 3
        await _portalService.RecordViewerTelemetryBeaconAsync(token, new ProposalBeaconDto(
            PageNumber: 1,
            SecondsDwell: 15.0,
            ScrollDepthPercent: 80,
            ViewerIp: "10.0.0.100",
            ViewerEmail: "colleague@gentek.com"
        ));
        var finalAnalytics = await _portalService.RecordViewerTelemetryBeaconAsync(token, new ProposalBeaconDto(
            PageNumber: 2,
            SecondsDwell: 15.0,
            ScrollDepthPercent: 75,
            ViewerIp: "10.0.0.100",
            ViewerEmail: "colleague@gentek.com"
        ));

        // Assert Overview Metrics
        Assert.NotNull(finalAnalytics);
        Assert.Equal(envelopeId, finalAnalytics.EnvelopeId);
        Assert.Equal(90.0, finalAnalytics.TotalReadingDurationSeconds); // (20*3) + (15*2) = 90
        Assert.Equal(5, finalAnalytics.TotalBeaconEvents);
        Assert.Equal(2, finalAnalytics.UniqueViewers);

        // Assert Page Metrics
        Assert.Equal(4, finalAnalytics.PageMetrics.Count);

        var p1 = finalAnalytics.PageMetrics.First(p => p.PageNumber == 1);
        Assert.Equal(35.0, p1.TotalDwellSeconds); // 20 + 15
        Assert.Equal(2, p1.ViewCount);
        Assert.Equal(17.5, p1.AverageDwellSeconds);
        Assert.Equal(Math.Round(35.0 / 90.0 * 100.0, 1), p1.AttentionConcentrationIndex);

        var p2 = finalAnalytics.PageMetrics.First(p => p.PageNumber == 2);
        Assert.Equal(35.0, p2.TotalDwellSeconds);
        Assert.Equal(2, p2.ViewCount);

        var p3 = finalAnalytics.PageMetrics.First(p => p.PageNumber == 3);
        Assert.Equal(20.0, p3.TotalDwellSeconds);
        Assert.Equal(1, p3.ViewCount);
        Assert.True(p3.IsPricingPage);
        Assert.False(p3.HasHighInterestAlert); // 20s < 45s threshold, 1 visit <= 2

        var p4 = finalAnalytics.PageMetrics.First(p => p.PageNumber == 4);
        Assert.Equal(0.0, p4.TotalDwellSeconds);
        Assert.Equal(0, p4.ViewCount);

        // Assert Drop-Off Retention Funnel
        Assert.Equal(4, finalAnalytics.DropOffFunnel.Count);

        var funnel1 = finalAnalytics.DropOffFunnel.First(f => f.PageNumber == 1);
        Assert.Equal(2, funnel1.ViewersReached);
        Assert.Equal(100.0, funnel1.RetentionRatePercent);
        Assert.Equal(0.0, funnel1.DropOffRatePercent);

        var funnel2 = finalAnalytics.DropOffFunnel.First(f => f.PageNumber == 2);
        Assert.Equal(2, funnel2.ViewersReached);
        Assert.Equal(100.0, funnel2.RetentionRatePercent);

        var funnel3 = finalAnalytics.DropOffFunnel.First(f => f.PageNumber == 3);
        Assert.Equal(1, funnel3.ViewersReached);
        Assert.Equal(50.0, funnel3.RetentionRatePercent);
        Assert.Equal(50.0, funnel3.DropOffRatePercent);

        var funnel4 = finalAnalytics.DropOffFunnel.First(f => f.PageNumber == 4);
        Assert.Equal(0, funnel4.ViewersReached);
        Assert.Equal(0.0, funnel4.RetentionRatePercent);
        Assert.Equal(100.0, funnel4.DropOffRatePercent);
    }

    [Fact]
    public async Task PricingPage_DwellExceeding45s_EmitsHighInterestAlertAndSignalREvent()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var envelopeId = Guid.NewGuid();

        var token = await _portalService.CreateProposalShareTokenAsync(new CreateProposalShareTokenRequest(
            TenantId: tenantId,
            EnvelopeId: envelopeId,
            Title: "High-Intent Enterprise Suite License",
            RecipientName: "Diana Prince",
            RecipientEmail: "diana@themyscira.gov",
            TotalPages: 4,
            PricingPageNumber: 3,
            SalesRepName: "Bruce Wayne",
            SalesRepEmail: "bruce@wayne-enterprises.com"
        ));

        // Act: Reader dwells 55.0s on Pricing Page (Page 3)
        var analytics = await _portalService.RecordViewerTelemetryBeaconAsync(token, new ProposalBeaconDto(
            PageNumber: 3,
            SecondsDwell: 55.0,
            ScrollDepthPercent: 100,
            ViewerIp: "72.14.201.1",
            ViewerEmail: "diana@themyscira.gov"
        ));

        // Assert Telemetry Summary
        Assert.True(analytics.HasPricingHighInterestAlert);
        Assert.NotEmpty(analytics.HighInterestAlerts);

        var alert = analytics.HighInterestAlerts.First();
        Assert.Equal(3, alert.PageNumber);
        Assert.Contains("Pricing", alert.AlertType);
        Assert.Equal("bruce@wayne-enterprises.com", alert.NotifiedRepEmail);
        Assert.Contains("55.0s", alert.TriggerReason);

        // Assert SignalR Real-Time Event was emitted to Tenant Group
        _mockClient.Verify(c => c.OnHighIntentProposalViewed(It.Is<HighIntentProposalViewedEventDto>(e =>
            e.TenantId == tenantId &&
            e.ProposalId == envelopeId &&
            e.PageNumber == 3 &&
            e.DwellSeconds >= 55.0 &&
            e.ViewerEmail == "diana@themyscira.gov"
        )), Times.Once);

        // Assert Outbox Event was Enqueued
        _mockOutbox.Verify(o => o.EnqueueAsync(
            tenantId,
            "Proposal.HighIntentViewed",
            It.IsAny<ProposalHighInterestAlertDto>(),
            default
        ), Times.Once);
    }

    [Fact]
    public async Task PricingPage_RevisitedMoreThanTwice_TriggersRevisitSpikeAlert()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var envelopeId = Guid.NewGuid();

        var token = await _portalService.CreateProposalShareTokenAsync(new CreateProposalShareTokenRequest(
            TenantId: tenantId,
            EnvelopeId: envelopeId,
            Title: "Repeat Interest Pricing Schedule",
            RecipientName: "Tony Stark",
            RecipientEmail: "tony@starkindustries.com",
            TotalPages: 4,
            PricingPageNumber: 3
        ));

        // Visit 1: Dwells 10s on Pricing (no alert yet, visitCount = 1, dwell = 10s < 45s)
        await _portalService.RecordViewerTelemetryBeaconAsync(token, new ProposalBeaconDto(3, 10.0, 50));

        // Visit 2: Dwells 10s on Pricing (no alert yet, visitCount = 2, dwell = 20s < 45s)
        await _portalService.RecordViewerTelemetryBeaconAsync(token, new ProposalBeaconDto(3, 10.0, 60));

        // Visit 3: Dwells 10s on Pricing (visitCount = 3 > 2 threshold! Alert should trigger!)
        var analytics = await _portalService.RecordViewerTelemetryBeaconAsync(token, new ProposalBeaconDto(3, 10.0, 70));

        // Assert
        Assert.True(analytics.HasPricingHighInterestAlert);
        Assert.NotEmpty(analytics.HighInterestAlerts);

        var alert = analytics.HighInterestAlerts.First();
        Assert.Equal(3, alert.PageNumber);
        Assert.Equal(3, alert.VisitCount);
        Assert.Contains("Revisit", alert.AlertType);

        // Verify SignalR event emitted
        _mockClient.Verify(c => c.OnHighIntentProposalViewed(It.Is<HighIntentProposalViewedEventDto>(e =>
            e.ProposalId == envelopeId &&
            e.PageNumber == 3 &&
            e.VisitCount == 3
        )), Times.Once);
    }

    [Fact]
    public async Task NonPricingPage_DwellExceeding45s_DoesNotTriggerPricingHighIntentAlert()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var envelopeId = Guid.NewGuid();

        var token = await _portalService.CreateProposalShareTokenAsync(new CreateProposalShareTokenRequest(
            TenantId: tenantId,
            EnvelopeId: envelopeId,
            Title: "Standard Commercial Terms",
            RecipientName: "Arthur Dent",
            RecipientEmail: "arthur@earth.co.uk",
            TotalPages: 4,
            PricingPageNumber: 3
        ));

        // Act: Reader spends 90 seconds on Page 1 (Cover / Intro), NOT pricing page
        var analytics = await _portalService.RecordViewerTelemetryBeaconAsync(token, new ProposalBeaconDto(
            PageNumber: 1,
            SecondsDwell: 90.0,
            ScrollDepthPercent: 100
        ));

        // Assert: Page 1 metrics reflect high dwell, but NO high-interest pricing alert
        Assert.False(analytics.HasPricingHighInterestAlert);
        Assert.Empty(analytics.HighInterestAlerts);

        _mockClient.Verify(c => c.OnHighIntentProposalViewed(It.IsAny<HighIntentProposalViewedEventDto>()), Times.Never);
    }

    [Fact]
    public async Task SignOrAcceptProposal_ValidRequest_UpdatesEnvelopeToCompletedAndGeneratesCryptographicDigest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var envelopeId = Guid.NewGuid();

        var token = await _portalService.CreateProposalShareTokenAsync(new CreateProposalShareTokenRequest(
            TenantId: tenantId,
            EnvelopeId: envelopeId,
            Title: "Global SaaS Master Services Agreement",
            RecipientName: "John Connor",
            RecipientEmail: "john@tech-resistance.com"
        ));

        var acceptRequest = new AcceptProposalDto(
            SignerName: "John Connor",
            SignerEmail: "john@tech-resistance.com",
            SignatureData: "data:image/svg+xml;utf8,<svg><text>John Connor</text></svg>",
            ClientIp: "203.0.113.195",
            UserAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
        );

        // Act
        var result = await _portalService.SignOrAcceptProposalAsync(token, acceptRequest);

        // Assert Result
        Assert.NotNull(result);
        Assert.Equal(envelopeId, result.EnvelopeId);
        Assert.Equal("Completed", result.Status);
        Assert.Equal("John Connor", result.SignerName);
        Assert.Equal("john@tech-resistance.com", result.SignerEmail);
        Assert.False(string.IsNullOrWhiteSpace(result.CryptographicDigestSha256));
        Assert.Equal(64, result.CryptographicDigestSha256.Length); // Hex-encoded SHA-256

        // Assert Database Envelope State
        var envelopeInDb = await _db.DocumentEnvelopes.Include(e => e.Signers).FirstOrDefaultAsync(e => e.Id == envelopeId);
        Assert.NotNull(envelopeInDb);
        Assert.Equal(EnvelopeStatus.Completed, envelopeInDb.Status);
        Assert.NotNull(envelopeInDb.CompletedAtUtc);

        var signerInDb = envelopeInDb.Signers.First(s => s.Email == "john@tech-resistance.com");
        Assert.Equal(SignerStatus.Signed, signerInDb.Status);
        Assert.Equal("203.0.113.195", signerInDb.IpAddress);
        Assert.NotNull(signerInDb.SignedAtUtc);

        // Assert Audit Certificate was Generated and attached
        Assert.False(string.IsNullOrWhiteSpace(envelopeInDb.AuditCertificateJson));
        Assert.Contains(result.CryptographicDigestSha256, envelopeInDb.AuditCertificateJson);

        // Assert Outbox Event Enqueued
        _mockOutbox.Verify(o => o.EnqueueAsync(
            tenantId,
            "Proposal.EnvelopeCompleted",
            It.IsAny<object>(),
            default
        ), Times.Once);
    }

    [Fact]
    public async Task SignOrAcceptProposal_MissingNameOrEmail_ThrowsArgumentException()
    {
        // Arrange
        var token = await _portalService.CreateProposalShareTokenAsync(new CreateProposalShareTokenRequest(
            TenantId: Guid.NewGuid(),
            EnvelopeId: Guid.NewGuid(),
            Title: "Invalid Signer Test",
            RecipientName: "Placeholder",
            RecipientEmail: "placeholder@test.com"
        ));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _portalService.SignOrAcceptProposalAsync(token, new AcceptProposalDto("", "valid@email.com", "sig"))
        );

        await Assert.ThrowsAsync<ArgumentException>(
            () => _portalService.SignOrAcceptProposalAsync(token, new AcceptProposalDto("Valid Name", "", "sig"))
        );
    }

    [Fact]
    public async Task FallbackLookup_ByEnvelopeSignerVerificationToken_Succeeds()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var envelopeId = Guid.NewGuid();
        var verificationToken = "custom_signer_token_" + Guid.NewGuid().ToString("N")[..12];

        var envelope = DocumentEnvelope.Create(
            tenantId,
            "Direct Db Envelope Proposal",
            "/storage/direct.pdf",
            "Direct-Content"
        );
        envelope.Id = envelopeId;
        _db.DocumentEnvelopes.Add(envelope);

        var signer = EnvelopeSigner.Create(
            tenantId,
            envelopeId,
            "Legacy Client",
            "legacy@client.com",
            SignerRole.Signer
        );
        signer.VerificationToken = verificationToken;
        _db.EnvelopeSigners.Add(signer);

        await _db.SaveChangesAsync();

        // Act
        var proposal = await _portalService.GetPublicProposalByTokenAsync(verificationToken);

        // Assert
        Assert.NotNull(proposal);
        Assert.Equal(envelopeId, proposal.EnvelopeId);
        Assert.Equal("Direct Db Envelope Proposal", proposal.Title);
        Assert.Equal("Legacy Client", proposal.RecipientName);
        Assert.Equal("legacy@client.com", proposal.RecipientEmail);
    }
}
