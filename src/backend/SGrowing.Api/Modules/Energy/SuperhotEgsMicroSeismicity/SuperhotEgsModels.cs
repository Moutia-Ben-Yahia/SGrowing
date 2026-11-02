namespace SGrowing.Api.Modules.Energy.SuperhotEgsMicroSeismicity;

public record SeismicEventRecord(
    long EventId,
    double MagnitudeLocal,
    double DepthKilometers,
    double PeakGroundVelocityMmPerSec,
    long TimestampEpochMs
);

public record EgsWellTelemetry(
    string WellId,
    double WellDepthMeters = 4200.0,
    double BottomHoleTemperatureCelsius = 425.0, // Supercritical EGS (> 374°C)
    double InjectionFlowRateLitersPerSec = 45.0,
    double WellheadPressureBar = 180.0,
    IReadOnlyList<SeismicEventRecord>? RecentSeismicEvents = null
);

public record TrafficLightThresholds(
    double AmberMagnitudeThreshold = 1.0,
    double RedMagnitudeThreshold = 2.0,
    double MaxAllowablePgvMmPerSec = 2.5
);

public record SuperhotEgsEvaluationRequest(
    EgsWellTelemetry Telemetry,
    TrafficLightThresholds Thresholds
);

public record EgsSeismicityMitigationPlan(
    string WellId,
    int TotalRecordedSeismicEvents,
    double MaxObservedMagnitude,
    double GutenbergRichterBValue,
    string TrafficLightStatus, // "GREEN", "AMBER", "RED"
    double RecommendedInjectionFlowRateLitersPerSec,
    double RecommendedWellheadPressureBar,
    string MitigationDirective
);
