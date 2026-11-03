namespace SGrowing.Api.Modules.Advertising.SignageSsp;

public enum ScreenLocationType
{
    StoreEntrance,
    AisleEndcap,
    SmartCheckoutKiosk,
    WindowDisplay,
    TransitJunction
}

public sealed record SignageDisplayNode(
    string NodeId,
    string VenueStoreName,
    ScreenLocationType LocationType,
    int ResolutionWidth,
    int ResolutionHeight,
    double CurrentAmbientFootfallDensityMultiplier,
    double BaseSlotFloorCpmUsd,
    bool IsOnline,
    string HardwareAttestationThumbprint
);

public sealed record DynamicSlotAuction(
    string AuctionId,
    string NodeId,
    int SlotDurationSeconds,
    string WinningAdvertiserCampaignId,
    double WinningBidCpmUsd,
    double EffectiveSlotRevenueUsd,
    DateTime AuctionTimestampUtc,
    string CreativeAssetUri
);

public sealed record ProofOfPlayAttestation(
    string AttestationId,
    string AuctionId,
    string NodeId,
    DateTime PlayoutStartedAtUtc,
    DateTime PlayoutEndedAtUtc,
    string HardwareRenderHashSha256,
    int VerifiedAudienceImpressions,
    bool IsAttestedValid
);

public sealed record SignageRevenueSettlement(
    string SettlementId,
    string VenueStoreName,
    double GrossProgrammaticRevenueUsd,
    double VenueRevShareUsd,
    double PlatformRevShareUsd,
    DateTime PeriodEndedAtUtc
);

public sealed record SignageSspOverview(
    int ActiveDisplayNodesCount,
    int TotalAuctionsCompletedToday,
    double AverageWinningCpmUsd,
    double TotalAttestedPlayoutsCount,
    double DailyGrossProgrammaticRevenueUsd,
    IReadOnlyList<SignageDisplayNode> DisplayNodes,
    IReadOnlyList<DynamicSlotAuction> RecentAuctions
);
