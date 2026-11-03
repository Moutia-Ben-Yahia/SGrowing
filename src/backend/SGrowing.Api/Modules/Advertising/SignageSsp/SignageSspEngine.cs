using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Advertising.SignageSsp;

public interface ISignageSspEngine
{
    SignageSspOverview GetOverview();
    SignageDisplayNode RegisterDisplayNode(string venueName, ScreenLocationType locationType, double baseFloorCpm);
    DynamicSlotAuction AuctionSlot(string nodeId, int durationSeconds, double minBidThreshold);
    ProofOfPlayAttestation VerifyProofOfPlay(string auctionId, string nodeId, int durationSeconds, int audienceImpressions);
    SignageRevenueSettlement SettleRevenue(string venueName, double grossRevenue);
}

public sealed class SignageSspEngine : ISignageSspEngine
{
    private readonly ConcurrentDictionary<string, SignageDisplayNode> _nodes = new();
    private readonly ConcurrentDictionary<string, DynamicSlotAuction> _auctions = new();
    private readonly ConcurrentDictionary<string, ProofOfPlayAttestation> _attestations = new();

    public SignageSspEngine()
    {
        SeedNodes();
        SeedInitialAuctions();
    }

    private void SeedNodes()
    {
        var node1 = new SignageDisplayNode(
            "SIGN-NODE-001",
            "Target Supercenter Flagship - Downtown Chicago",
            ScreenLocationType.SmartCheckoutKiosk,
            3840,
            2160,
            1.85,
            12.50,
            true,
            "THUMB-TPM2-99B4F1A0"
        );
        _nodes.TryAdd(node1.NodeId, node1);

        var node2 = new SignageDisplayNode(
            "SIGN-NODE-002",
            "Nordstrom Plaza - Seattle WA",
            ScreenLocationType.StoreEntrance,
            1920,
            1080,
            2.10,
            18.00,
            true,
            "THUMB-TPM2-88C3E201"
        );
        _nodes.TryAdd(node2.NodeId, node2);
    }

    private void SeedInitialAuctions()
    {
        var auc1 = new DynamicSlotAuction(
            "AUC-SIGN-001",
            "SIGN-NODE-001",
            15,
            "CAMP-BEVERAGE-ESPRESSO",
            24.50,
            0.3675,
            DateTime.UtcNow.AddMinutes(-12),
            "https://cdn.sgrowing.io/creatives/espresso-boost-4k.mp4"
        );
        _auctions.TryAdd(auc1.AuctionId, auc1);

        var att1 = new ProofOfPlayAttestation(
            "ATT-PLAY-001",
            auc1.AuctionId,
            auc1.NodeId,
            DateTime.UtcNow.AddMinutes(-12),
            DateTime.UtcNow.AddMinutes(-12).AddSeconds(15),
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            142,
            true
        );
        _attestations.TryAdd(att1.AttestationId, att1);
    }

    public SignageSspOverview GetOverview()
    {
        var nodes = _nodes.Values.ToList();
        var auctions = _auctions.Values.OrderByDescending(a => a.AuctionTimestampUtc).Take(10).ToList();

        return new SignageSspOverview(
            ActiveDisplayNodesCount: nodes.Count,
            TotalAuctionsCompletedToday: 1845,
            AverageWinningCpmUsd: 21.80,
            TotalAttestedPlayoutsCount: 1829,
            DailyGrossProgrammaticRevenueUsd: 4892.40,
            DisplayNodes: nodes,
            RecentAuctions: auctions
        );
    }

    public SignageDisplayNode RegisterDisplayNode(string venueName, ScreenLocationType locationType, double baseFloorCpm)
    {
        var thumbprint = $"THUMB-TPM2-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var node = new SignageDisplayNode(
            $"SIGN-NODE-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            venueName,
            locationType,
            3840,
            2160,
            CurrentAmbientFootfallDensityMultiplier: 1.5,
            BaseSlotFloorCpmUsd: baseFloorCpm,
            IsOnline: true,
            HardwareAttestationThumbprint: thumbprint
        );
        _nodes.TryAdd(node.NodeId, node);
        return node;
    }

    public DynamicSlotAuction AuctionSlot(string nodeId, int durationSeconds, double minBidThreshold)
    {
        if (!_nodes.TryGetValue(nodeId, out var node))
        {
            node = RegisterDisplayNode("Default Retail Venue", ScreenLocationType.AisleEndcap, 15.0);
        }

        double winningCpm = Math.Max(minBidThreshold, node.BaseSlotFloorCpmUsd * node.CurrentAmbientFootfallDensityMultiplier + 4.25);
        double slotRev = (winningCpm / 1000.0) * (durationSeconds / 15.0) * 10.0;

        var auction = new DynamicSlotAuction(
            $"AUC-SIGN-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            nodeId,
            durationSeconds,
            "CAMP-RETAIL-PREMIUM-01",
            Math.Round(winningCpm, 2),
            Math.Round(slotRev, 4),
            DateTime.UtcNow,
            "https://cdn.sgrowing.io/creatives/signage-hero-4k.mp4"
        );

        _auctions.TryAdd(auction.AuctionId, auction);
        return auction;
    }

    public ProofOfPlayAttestation VerifyProofOfPlay(string auctionId, string nodeId, int durationSeconds, int audienceImpressions)
    {
        var rawString = $"{auctionId}:{nodeId}:{durationSeconds}:{audienceImpressions}:{DateTime.UtcNow.Ticks}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawString))).ToLowerInvariant();

        var attestation = new ProofOfPlayAttestation(
            $"ATT-PLAY-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            auctionId,
            nodeId,
            DateTime.UtcNow.AddSeconds(-durationSeconds),
            DateTime.UtcNow,
            hash,
            audienceImpressions,
            IsAttestedValid: true
        );

        _attestations.TryAdd(attestation.AttestationId, attestation);
        return attestation;
    }

    public SignageRevenueSettlement SettleRevenue(string venueName, double grossRevenue)
    {
        double venueShare = Math.Round(grossRevenue * 0.70, 2); // 70% venue owner rev share
        double platformShare = Math.Round(grossRevenue * 0.30, 2); // 30% Growth OS fee

        return new SignageRevenueSettlement(
            $"SETTLE-REV-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            venueName,
            grossRevenue,
            venueShare,
            platformShare,
            DateTime.UtcNow
        );
    }
}
