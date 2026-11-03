using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Advertising.RetailCoopDsp;

public sealed record RetailerCleanRoomPartnerRecord(
    string CleanRoomPartnerId,
    string RetailNetworkName,
    string CleanRoomProvider, // Snowflake, AWS Clean Rooms, Habu
    int VerifiedShopperIdentitiesCount,
    double OverlapAccuracyPct,
    string Status
);

public sealed record OffSiteCoopCampaignRecord(
    string CampaignId,
    string BrandName,
    string RetailPartnerId,
    decimal TotalCoopBudgetUsd,
    decimal BrandContributionUsd,
    decimal RetailerCoopCreditUsd,
    string TargetAudienceSegment,
    string Channels, // CTV, Open Web RTB, Social
    double ProjectedRoasMultiplier,
    string Status
);

public sealed record CleanRoomMatchReceipt(
    string MatchReceiptId,
    string RetailPartnerId,
    int HashedIdsProcessed,
    int MatchedHighPropensityShoppers,
    double MatchRatePct,
    string CryptographicMatchProof,
    DateTime Timestamp
);

public sealed record RetailCoopDspOverview(
    int ConnectedRetailCleanRoomsCount,
    int ActiveCoopCampaignsCount,
    decimal TotalCoopSpendUnderManagementUsd,
    double BlendedClosedLoopRoas,
    IReadOnlyList<RetailerCleanRoomPartnerRecord> CleanRoomPartners,
    IReadOnlyList<OffSiteCoopCampaignRecord> ActiveCampaigns
);

public sealed record LaunchCoopCampaignRequest(
    string BrandName,
    string RetailPartnerId,
    decimal BrandBudgetUsd,
    decimal RetailerContributionPct,
    string TargetAudienceSegment,
    string Channels
);

public sealed record ExecuteCleanRoomMatchRequest(
    string RetailPartnerId,
    int HashedCandidateIdentifiersCount
);
