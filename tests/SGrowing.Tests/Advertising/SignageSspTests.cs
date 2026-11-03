using SGrowing.Api.Modules.Advertising.SignageSsp;
using Xunit;

namespace SGrowing.Tests.Advertising;

public sealed class SignageSspTests
{
    private readonly SignageSspEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsSeededNodesAndAuctions()
    {
        var overview = _engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActiveDisplayNodesCount >= 2);
        Assert.True(overview.AverageWinningCpmUsd > 15.0);
        Assert.NotEmpty(overview.DisplayNodes);
        Assert.NotEmpty(overview.RecentAuctions);
    }

    [Fact]
    public void AuctionSlot_WithHighFootfall_YieldsPremiumCpm()
    {
        var node = _engine.RegisterDisplayNode("Grand Central Terminal Screen 4", ScreenLocationType.TransitJunction, 20.0);
        var auction = _engine.AuctionSlot(node.NodeId, 15, 25.0);

        Assert.NotNull(auction);
        Assert.StartsWith("AUC-SIGN-", auction.AuctionId);
        Assert.Equal(node.NodeId, auction.NodeId);
        Assert.True(auction.WinningBidCpmUsd >= 25.0);
        Assert.True(auction.EffectiveSlotRevenueUsd > 0.0);
    }

    [Fact]
    public void VerifyProofOfPlay_GeneratesValidHardwareAttestationSha256()
    {
        var attestation = _engine.VerifyProofOfPlay("AUC-SIGN-TEST", "SIGN-NODE-001", 15, 250);

        Assert.NotNull(attestation);
        Assert.StartsWith("ATT-PLAY-", attestation.AttestationId);
        Assert.True(attestation.IsAttestedValid);
        Assert.Equal(64, attestation.HardwareRenderHashSha256.Length); // Valid SHA256 hex
        Assert.Equal(250, attestation.VerifiedAudienceImpressions);
    }
}
