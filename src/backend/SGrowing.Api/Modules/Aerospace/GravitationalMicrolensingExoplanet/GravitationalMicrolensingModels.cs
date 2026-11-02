namespace SGrowing.Api.Modules.Aerospace.GravitationalMicrolensingExoplanet;

public record MicrolensingEventTelemetry(
    string EventId,
    double CurrentTimeDays,
    double TimeOfClosestApproachDays,
    double ImpactParameterU0,
    double EinsteinCrossingTimeDays,
    double ObservedMagnification,
    double PhotometricErrorMag,
    double SourceAngularRadiusRho,
    double EstimatedMassRatioQ,
    double ProjectedSeparationS);

public record MicrolensingAnalysisResult(
    string EventId,
    double PaczynskiBaselineMagnification,
    double AnomalyResidualFraction,
    bool IsCausticCrossingDetected,
    string CausticTopology,
    double EstimatedCausticCrossingDurationMinutes,
    string RecommendedAlertLevel,
    string FollowUpCadenceRecommendation,
    string ActionableSummary);
