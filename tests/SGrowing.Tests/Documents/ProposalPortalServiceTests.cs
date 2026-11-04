using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Documents;
using Xunit;

namespace SGrowing.Tests.Documents;

public class ProposalPortalServiceTests
{
    private static SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"ProposalPortalDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task CreateProposalShareTokenAsync_GeneratesSecureTokenAndRegistersProposal()
    {
        using var db = CreateDbContext();
        var service = new ProposalPortalService(db);
        var tenantId = Guid.NewGuid();
        var envelopeId = Guid.NewGuid();

        var request = new CreateProposalShareTokenRequest(
            TenantId: tenantId,
            EnvelopeId: envelopeId,
            Title: "Enterprise SaaS Expansion Proposal",
            RecipientName: "Alex Vance",
            RecipientEmail: "alex@vance.io",
            TotalPages: 5,
            PricingPageNumber: 4,
            Passcode: "SecretPass2026",
            ValidDays: 14,
            SalesRepName: "David Miller",
            SalesRepEmail: "david@sgrowing.io",
            DealValue: 50000m,
            Currency: "USD"
        );

        var token = await service.CreateProposalShareTokenAsync(request);

        token.Should().NotBeNullOrWhiteSpace();
        token.Should().StartWith("prop_sec_");

        // Retrieve proposal with correct password
        var proposal = await service.GetPublicProposalByTokenAsync(token, accessKey: "SecretPass2026");
        proposal.Should().NotBeNull();
        proposal.Title.Should().Be("Enterprise SaaS Expansion Proposal");
        proposal.RecipientEmail.Should().Be("alex@vance.io");
        proposal.DealValue.Should().Be(50000m);
        proposal.TotalPages.Should().Be(5);
        proposal.PricingPageNumber.Should().Be(4);
        proposal.IsPasswordProtected.Should().BeTrue();
        proposal.WatermarkConfig.Enabled.Should().BeTrue();
        proposal.WatermarkConfig.RecipientEmail.Should().Be("alex@vance.io");
    }

    [Fact]
    public async Task GetPublicProposalByTokenAsync_InvalidOrMissingToken_ThrowsKeyNotFoundException()
    {
        using var db = CreateDbContext();
        var service = new ProposalPortalService(db);

        var act = async () => await service.GetPublicProposalByTokenAsync("non_existent_token_999");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetPublicProposalByTokenAsync_IncorrectPasscode_ThrowsUnauthorizedAccessException()
    {
        using var db = CreateDbContext();
        var service = new ProposalPortalService(db);

        var token = await service.CreateProposalShareTokenAsync(new CreateProposalShareTokenRequest(
            TenantId: Guid.NewGuid(),
            EnvelopeId: Guid.NewGuid(),
            Title: "Confidential Pitch",
            RecipientName: "Jane Doe",
            RecipientEmail: "jane@doe.com",
            Passcode: "RealPasscode"
        ));

        var act = async () => await service.GetPublicProposalByTokenAsync(token, accessKey: "WrongPasscode");
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Password*");
    }

    [Fact]
    public async Task RecordViewerTelemetryBeaconAsync_TriggersHighInterestAlertOnPricingPage()
    {
        using var db = CreateDbContext();
        var service = new ProposalPortalService(db);
        var envelopeId = Guid.NewGuid();

        var token = await service.CreateProposalShareTokenAsync(new CreateProposalShareTokenRequest(
            TenantId: Guid.NewGuid(),
            EnvelopeId: envelopeId,
            Title: "Quarterly Enterprise Renewal",
            RecipientName: "Mark Prospect",
            RecipientEmail: "mark@prospect.com",
            TotalPages: 4,
            PricingPageNumber: 3,
            SalesRepEmail: "rep@sgrowing.io"
        ));

        // Send beacon for pricing page (page 3) exceeding 45 seconds dwell
        var analytics = await service.RecordViewerTelemetryBeaconAsync(token, new ProposalBeaconDto(
            PageNumber: 3,
            SecondsDwell: 65.0,
            ScrollDepthPercent: 95.0,
            ViewerIp: "192.168.1.55",
            UserAgent: "Mozilla/5.0",
            ViewerEmail: "mark@prospect.com"
        ));

        analytics.Should().NotBeNull();
        analytics.TotalBeaconEvents.Should().BeGreaterThanOrEqualTo(1);
        analytics.HasPricingHighInterestAlert.Should().BeTrue();
        analytics.HighInterestAlerts.Should().Contain(a => a.PageNumber == 3 && a.AlertType == "PricingDwellThresholdExceeded");

        var pageMetric = analytics.PageMetrics.FirstOrDefault(m => m.PageNumber == 3);
        pageMetric.Should().NotBeNull();
        pageMetric!.IsPricingPage.Should().BeTrue();
        pageMetric.HasHighInterestAlert.Should().BeTrue();
    }

    [Fact]
    public async Task SignOrAcceptProposalAsync_ValidSignature_SignsProposalAndGeneratesDigest()
    {
        using var db = CreateDbContext();
        var service = new ProposalPortalService(db);
        var envelopeId = Guid.NewGuid();

        var token = await service.CreateProposalShareTokenAsync(new CreateProposalShareTokenRequest(
            TenantId: Guid.NewGuid(),
            EnvelopeId: envelopeId,
            Title: "Digital Marketing Master Service Agreement",
            RecipientName: "Sara CEO",
            RecipientEmail: "sara@company.com"
        ));

        var acceptDto = new AcceptProposalDto(
            SignerName: "Sara CEO",
            SignerEmail: "sara@company.com",
            SignatureData: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=",
            ClientIp: "10.0.0.99",
            UserAgent: "Chrome 122"
        );

        var result = await service.SignOrAcceptProposalAsync(token, acceptDto);

        result.Should().NotBeNull();
        result.EnvelopeId.Should().Be(envelopeId);
        result.SignerName.Should().Be("Sara CEO");
        result.SignerEmail.Should().Be("sara@company.com");
        result.Status.Should().Be("Completed");
        result.CryptographicDigestSha256.Should().NotBeNullOrWhiteSpace();
        result.CertificateUrl.Should().Contain(envelopeId.ToString());
    }

    [Fact]
    public async Task RecordViewerTelemetryBeaconAsync_ThrowsArgumentException_WhenTokenIsMissing()
    {
        using var db = CreateDbContext();
        var service = new ProposalPortalService(db);

        var act = async () => await service.RecordViewerTelemetryBeaconAsync("", new ProposalBeaconDto(1, 10.0, 50.0));
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*token*");
    }

    [Fact]
    public async Task RecordViewerTelemetryBeaconAsync_ThrowsKeyNotFoundException_WhenTokenNotFound()
    {
        using var db = CreateDbContext();
        var service = new ProposalPortalService(db);

        var act = async () => await service.RecordViewerTelemetryBeaconAsync("prop_unknown_000", new ProposalBeaconDto(1, 10.0, 50.0));
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SignOrAcceptProposalAsync_ThrowsArgumentException_WhenSignerCredentialsMissing()
    {
        using var db = CreateDbContext();
        var service = new ProposalPortalService(db);

        var token = await service.CreateProposalShareTokenAsync(new CreateProposalShareTokenRequest(
            TenantId: Guid.NewGuid(),
            EnvelopeId: Guid.NewGuid(),
            Title: "Test Proposal",
            RecipientName: "Recipient",
            RecipientEmail: "recip@test.com"
        ));

        var missingNameDto = new AcceptProposalDto("", "recip@test.com", "sig");
        var actName = async () => await service.SignOrAcceptProposalAsync(token, missingNameDto);
        await actName.Should().ThrowAsync<ArgumentException>().WithMessage("*legal name*");

        var missingEmailDto = new AcceptProposalDto("Name", "", "sig");
        var actEmail = async () => await service.SignOrAcceptProposalAsync(token, missingEmailDto);
        await actEmail.Should().ThrowAsync<ArgumentException>().WithMessage("*email*");
    }

    [Fact]
    public async Task GetProposalAnalyticsAsync_AggregatesPageMetricsCorrectly()
    {
        using var db = CreateDbContext();
        var service = new ProposalPortalService(db);
        var envelopeId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var token = await service.CreateProposalShareTokenAsync(new CreateProposalShareTokenRequest(
            TenantId: tenantId,
            EnvelopeId: envelopeId,
            Title: "Analytics Benchmark Proposal",
            RecipientName: "Buyer VIP",
            RecipientEmail: "vip@buyer.com",
            TotalPages: 3,
            PricingPageNumber: 2
        ));

        // Send two beacons on page 1
        await service.RecordViewerTelemetryBeaconAsync(token, new ProposalBeaconDto(1, 20.0, 80.0, ViewerEmail: "vip@buyer.com"));
        await service.RecordViewerTelemetryBeaconAsync(token, new ProposalBeaconDto(1, 30.0, 100.0, ViewerEmail: "vip@buyer.com"));

        var analytics = await service.GetProposalTelemetryAnalyticsAsync(tenantId, envelopeId);

        analytics.Should().NotBeNull();
        analytics.EnvelopeId.Should().Be(envelopeId);
        analytics.TotalBeaconEvents.Should().Be(2);
        analytics.TotalReadingDurationSeconds.Should().Be(50.0);
    }
}
