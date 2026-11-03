using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Advertising.Uid2Tokenizer;
using Xunit;

namespace SGrowing.Tests.Advertising;

public class Uid2TokenizerTests
{
    private readonly Uid2TokenizerEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsStitchedIdentitiesAndCompliance()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.TotalStitchedIdentities.Should().Be(3);
        overview.ActiveAdvertisingTokensCount.Should().Be(3);
        overview.TokenRefreshComplianceRatePct.Should().BeGreaterThan(99.0);
        overview.DailyStitchedTouchpoints.Should().BeGreaterThan(5000);
        overview.Identities.Should().HaveCount(3);
    }

    [Fact]
    public void GenerateToken_ProducesValidEncryptedUid2Token()
    {
        var command = new GenerateTokenCommand("Enterprise.Buyer@Company.org", "+15550192834", "SALT-KEY-2026");
        var result = _engine.GenerateToken(command);

        result.Should().NotBeNull();
        result.AdvertisingToken.Should().StartWith("UID2-TOKEN-AD-");
        result.RefreshToken.Should().StartWith("UID2-REFRESH-");
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        result.IdentityHash.Should().HaveLength(64); // SHA-256 hex string
        result.IsEuidSupported.Should().BeTrue();
    }

    [Fact]
    public void StitchIdentity_MergesAnonymousCookieWithVerifiedUid2Profile()
    {
        var command = new StitchIdentityCommand("FP-COOKIE-9912", "cfo@fintechglobal.com", "IDFA-8812-99");
        var result = _engine.StitchIdentity(command);

        result.Should().NotBeNull();
        result.IdentityGraphId.Should().StartWith("GRAPH-ID-");
        result.MergedSessionCount.Should().BeGreaterThan(0);
        result.ActiveUid2Token.Should().StartWith("UID2-TOKEN-AD-");
        result.StitchReceipt.Should().Contain("RECEIPT-STITCH");
    }
}
