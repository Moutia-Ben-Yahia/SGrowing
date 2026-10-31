using FluentAssertions;
using SGrowing.Api.Modules.Documents.Security;
using Xunit;

namespace SGrowing.Tests.Documents;

public class DocumentSecurityServiceTests
{
    private readonly DocumentSecurityService _service = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _documentId = Guid.NewGuid();

    [Fact]
    public async Task RedactPiiAsync_ShouldMaskCreditCardsAndEmails_WithBlackoutStyle()
    {
        var sampleText = "Please process card 4532-1234-5678-9010 and send receipt to customer@example.com for order #982.";
        var req = new RedactPiiRequest(_tenantId, _documentId, sampleText, RedactionMaskStyle.BlackoutBox);

        var result = await _service.RedactPiiAsync(req);

        result.Should().NotBeNull();
        result.DetectedSpansCount.Should().Be(2);
        result.RedactedText.Should().Contain("[REDACTED:CREDITCARD]");
        result.RedactedText.Should().Contain("[REDACTED:EMAILADDRESS]");
        result.RedactedText.Should().NotContain("4532-1234-5678-9010");
        result.RedactedText.Should().NotContain("customer@example.com");
    }

    [Fact]
    public async Task RedactPiiAsync_ShouldMaskWithAsterisks_PreservingLastFourDigits()
    {
        var sample = "Card number is 4111 2222 3333 4444.";
        var req = new RedactPiiRequest(_tenantId, _documentId, sample, RedactionMaskStyle.Asterisks);

        var result = await _service.RedactPiiAsync(req);

        result.RedactedText.Should().Contain("4444");
        result.RedactedText.Should().Contain("************4444");
    }

    [Fact]
    public async Task RedactPiiAsync_ShouldFilterBySpecifiedCategoriesOnly()
    {
        var sample = "Client CIN: 09876543, Phone: +216 98 123 456, Card: 4000-1234-5678-9999.";
        // Only target CreditCard
        var req = new RedactPiiRequest(
            _tenantId,
            _documentId,
            sample,
            RedactionMaskStyle.BlackoutBox,
            TargetCategories: new List<PiiCategory> { PiiCategory.CreditCard }
        );

        var result = await _service.RedactPiiAsync(req);

        result.DetectedSpansCount.Should().Be(1);
        result.RedactedText.Should().Contain("[REDACTED:CREDITCARD]");
        result.RedactedText.Should().Contain("09876543"); // Not redacted
    }

    [Fact]
    public async Task GenerateWatermarkedPayloadAsync_ShouldEmbedForensicFingerprint()
    {
        var config = new ForensicWatermarkConfig(
            ViewerIdentity: "agent.sami@sgrowing.io",
            ViewerIp: "197.2.14.88",
            Classification: SecurityClassification.RestrictedSecret,
            Opacity: 0.3
        );

        var result = await _service.GenerateWatermarkedPayloadAsync(_tenantId, _documentId, config);

        result.Should().NotBeNull();
        result.WatermarkOverlaySvg.Should().Contain("RESTRICTEDSECRET");
        result.WatermarkOverlaySvg.Should().Contain("agent.sami@sgrowing.io");
        result.FormattedFooter.Should().Contain("Forensic Hash");
        result.ForensicFingerprintSha256.Should().HaveLength(64);
    }

    [Fact]
    public async Task CreateEphemeralShareLinkAsync_ShouldGenerateToken_AndExpireAfterSetMinutes()
    {
        var req = new CreateShareLinkRequest(
            _tenantId,
            _documentId,
            "auditor@deloitte.com",
            AccessPermission.ViewOnly,
            MaxViews: 2,
            ExpirationMinutes: 30
        );

        var link = await _service.CreateEphemeralShareLinkAsync(req);

        link.Should().NotBeNull();
        link.Token.Should().NotBeNullOrWhiteSpace();
        link.MaxViews.Should().Be(2);
        link.ViewCount.Should().Be(0);
        link.IsBurned.Should().BeFalse();
        link.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task AccessDocumentViaLinkAsync_ShouldBurnLink_WhenMaxViewsReached()
    {
        var link = await _service.CreateEphemeralShareLinkAsync(new CreateShareLinkRequest(
            _tenantId, _documentId, "partner@b2b.com", MaxViews: 1, ExpirationMinutes: 60
        ));

        // 1st access
        var accessed = await _service.AccessDocumentViaLinkAsync(new AccessDocumentRequest(
            _tenantId, link.Token, "192.168.1.50", "Mozilla/5.0", DwellSeconds: 45
        ));

        accessed.ViewCount.Should().Be(1);
        accessed.IsBurned.Should().BeTrue();

        // 2nd access should be rejected because it is burned
        var act = () => _service.AccessDocumentViaLinkAsync(new AccessDocumentRequest(
            _tenantId, link.Token, "192.168.1.50", "Mozilla/5.0"
        ));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already been burned*");
    }

    [Fact]
    public async Task AccessDocumentViaLinkAsync_ShouldReject_WhenRevoked()
    {
        var link = await _service.CreateEphemeralShareLinkAsync(new CreateShareLinkRequest(
            _tenantId, _documentId, "user@test.com", MaxViews: 5
        ));

        await _service.RevokeShareLinkAsync(_tenantId, link.LinkId);

        var act = () => _service.AccessDocumentViaLinkAsync(new AccessDocumentRequest(
            _tenantId, link.Token, "127.0.0.1", "Chrome"
        ));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*revoked*");
    }

    [Fact]
    public async Task GetDocumentAuditLedgerAsync_ShouldLogDwellTimeAndUserAgent()
    {
        var link = await _service.CreateEphemeralShareLinkAsync(new CreateShareLinkRequest(
            _tenantId, _documentId, "compliance@bank.com", MaxViews: 3
        ));

        await _service.AccessDocumentViaLinkAsync(new AccessDocumentRequest(
            _tenantId, link.Token, "10.0.0.15", "Safari/605", DwellSeconds: 120
        ));

        var ledger = await _service.GetDocumentAuditLedgerAsync(_tenantId, _documentId);

        ledger.Should().ContainSingle();
        var entry = ledger[0];
        entry.ViewerIdentity.Should().Be("compliance@bank.com");
        entry.ViewerIp.Should().Be("10.0.0.15");
        entry.DwellSeconds.Should().Be(120);
        entry.UserAgent.Should().Be("Safari/605");
    }

    [Fact]
    public async Task GetMetricsAsync_ShouldTrackSecurityStats()
    {
        await _service.RedactPiiAsync(new RedactPiiRequest(_tenantId, _documentId, "Email: test@test.com"));
        await _service.CreateEphemeralShareLinkAsync(new CreateShareLinkRequest(_tenantId, _documentId, "a@b.com"));

        var metrics = await _service.GetMetricsAsync(_tenantId);

        metrics.TotalRedactionsPerformed.Should().BeGreaterThan(0);
        metrics.TotalActiveShareLinks.Should().BeGreaterThan(0);
    }
}
