namespace SGrowing.Api.Modules.Energy.GeothermalMicroSeismicity;

public record InjectionWell(
    string WellId,
    string ReservoirName,
    double DepthMeters,
    double CurrentInjectionRateLitersPerSec,
    double WellheadPressureBar,
    double MaxAllowedPressureBar);

public record MicroSeismicEvent(
    string EventId,
    double MagnitudeLocal,
    double HypocenterDepthMeters,
    double DistanceFromWellMeters,
    DateTimeOffset TimestampUtc);

public record GeothermalSeismicAnalysisRequest(
    List<InjectionWell> Wells,
    List<MicroSeismicEvent> RecentEvents,
    double AmberThresholdMagnitude = 1.0,
    double RedThresholdMagnitude = 2.0);

public record WellControlCommand(
    string WellId,
    string TrafficLightStatus,
    double RecommendedFlowRateLitersPerSec,
    double PressureAdjustmentBar,
    string ActionReason);

public record GeothermalSeismicAssessment(
    string ReservoirName,
    int TotalEventsAudited,
    double MaxMagnitudeRecorded,
    double GutenbergRichterBValue,
    List<WellControlCommand> Commands,
    string ReservoirSafetyPosture);
