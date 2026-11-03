namespace SGrowing.Api.Modules.Advertising.VehicleAudioDsp;

public enum CommuteContextType
{
    MorningRushCongestion,
    HighwayCruising,
    DriveThruProximity,
    EveningCommuteReturn,
    InclementWeatherCaution
}

public sealed record VehicleAdSlotBid(
    string BidId,
    string VehicleOemMakeModel,
    CommuteContextType ContextType,
    double CurrentVehicleSpeedKmh,
    double Latitude,
    double Longitude,
    string MatchedCampaignId,
    double WinningBidCpmUsd,
    string AudioCreativeAssetUri,
    DateTime AuctionTimestampUtc
);

public sealed record VehiclePlayoutAttestation(
    string AttestationId,
    string BidId,
    string VehicleHardwareId,
    int DurationSeconds,
    double AudioVolumeNormalizedDb,
    bool PassengerPresenceDetected,
    bool IsPlayoutVerified
);

public sealed record VehicleAudioDspOverview(
    int ConnectedVehiclesActiveCount,
    int TotalAudioImpressionsDeliveredToday,
    double AverageInCabinCpmUsd,
    double CommuteConversionLiftPct,
    IReadOnlyList<VehicleAdSlotBid> RecentBids,
    IReadOnlyList<VehiclePlayoutAttestation> RecentAttestations
);
