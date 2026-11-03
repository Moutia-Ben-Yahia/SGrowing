namespace SGrowing.Api.Modules.Advertising.InCarAds;

public record InCarAdSession(
    string SessionId,
    string VehiclePlatform, // ANDROID_AUTOMOTIVE, APPLE_CARPLAY, OEM_PROPRIETARY
    string DestinationPoiCategory, // EV_CHARGING_STATION, QUICK_SERVICE_RESTAURANT, RETAIL_PARK, HOTEL
    double VehicleSpeedKmh,
    double BatteryLevelPct,
    string AdFormatAllowed, // AUDIO_ONLY, AUDIO_AND_PARK_DISPLAY
    string SponsorBrand,
    decimal ClearingCpmUsd,
    string Status // BID_WON, SERVING, COMPLETED
);

public record InCarImpressionMetric(
    string MetricId,
    string SessionId,
    string SponsorBrand,
    int AudioDurationSeconds,
    bool DriverEngagedVoiceResponse,
    bool NavigationalDetourAccepted,
    DateTime Timestamp
);

public record InCarAdOverview(
    int ActiveVehicleSessionsCount,
    double NonDistractionCompliancePct, // e.g. 100.0%
    int TotalDetoursAcceptedCount,
    decimal TotalInCarAdSpendUsd,
    List<InCarAdSession> ActiveSessions,
    List<InCarImpressionMetric> RecentMetrics
);

public record SubmitInCarBidRequest(
    string VehiclePlatform,
    string DestinationPoiCategory,
    double VehicleSpeedKmh,
    double BatteryLevelPct,
    string SponsorBrand,
    decimal MaxBidCpmUsd
);

public record RecordInCarImpressionRequest(
    string SessionId,
    string SponsorBrand,
    int AudioDurationSeconds,
    bool DriverEngagedVoiceResponse,
    bool NavigationalDetourAccepted
);
