namespace SGrowing.Api.Modules.Advertising.DoohBidding;

public record DoohBillboardPlacement(
    string PlacementId,
    string VenueName,
    string GeoLocationDma, // NYC_TIMES_SQUARE, SFO_AIRPORT_T2, LON_PICCADILLY, TYO_SHIBUYA
    string ScreenResolution, // 4K_UHD, 8K_SURROUND, 1080P_LED
    int LoopDurationSeconds,
    double PedestrianHourlyFlow,
    double DynamicAudienceMultiplier, // 1.0 to 3.5x
    decimal ClearingCpmUsd,
    string Status // AUCTION_OPEN, BID_WON, SERVING
);

public record MobileFootTrafficAttribution(
    string AttributionId,
    string PlacementId,
    string HashedMaidDeviceGroup,
    string PhysicalStoreLocation,
    int AttributedStoreVisits,
    double LiftOverBaselinePct,
    DateTime MeasuredAt
);

public record DoohBiddingOverview(
    int ActiveScreensCount,
    double AverageAudienceMultiplier,
    int TotalFootTrafficVisitsAttributed,
    decimal TotalDoohSpendUsd,
    List<DoohBillboardPlacement> ActivePlacements,
    List<MobileFootTrafficAttribution> RecentAttributions
);

public record SubmitDoohBidRequest(
    string VenueName,
    string GeoLocationDma,
    string ScreenResolution,
    int LoopDurationSeconds,
    decimal MaxBidCpmUsd
);

public record RecordFootTrafficRequest(
    string PlacementId,
    string PhysicalStoreLocation,
    int AttributedStoreVisits,
    double LiftOverBaselinePct
);
