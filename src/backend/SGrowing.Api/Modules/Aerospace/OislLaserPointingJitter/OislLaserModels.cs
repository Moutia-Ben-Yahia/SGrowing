namespace SGrowing.Api.Modules.Aerospace.OislLaserPointingJitter;

public record OislLaserTelemetry(
    string LinkId,
    double InterSatelliteDistanceKm,
    double LaserWavelengthNm,
    double TelescopeApertureDiameterMm,
    double TransmitOpticalPowerMilliWatts,
    double PointingJitterRmsMicroRadians,
    double MeasuredReceivedPowerDbm,
    double QpdTrackingErrorMicroRadians);

public record OislLaserAnalysisResult(
    string LinkId,
    double BeamDivergenceMicroRadians,
    double PointingJitterRatio,
    double JitterAttenuationDb,
    bool IsLinkDegradationRiskDetected,
    string TrackingState,
    string FsmCorrectionCommand,
    string LinkHealthSummary);
