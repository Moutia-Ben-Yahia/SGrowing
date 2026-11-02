namespace SGrowing.Api.Modules.Telecom.SubseaAcousticEarlyWarning;

public record SubseaCableSegment(
    string SegmentId,
    string CableSystemName,
    double StartKm,
    double EndKm,
    double WaterDepthMeters,
    string GeoZone,
    double OpticalAttenuationDbPerKm);

public record DistributedAcousticReading(
    string SegmentId,
    double PositionKm,
    double StrainMicrostrains,
    double FrequencyHz,
    DateTimeOffset TimestampUtc,
    double AcousticEnergyJoules);

public record SeismicWavefront(
    string EventId,
    double EpicenterLat,
    double EpicenterLon,
    double MagnitudeRichter,
    double DepthKm,
    DateTimeOffset OriginUtc);

public record SubseaAcousticAnalysisRequest(
    List<SubseaCableSegment> CableSegments,
    List<DistributedAcousticReading> AcousticReadings,
    List<SeismicWavefront>? ActiveSeismicEvents = null,
    double AlertStrainThresholdMicrostrains = 150.0);

public record CableThreatAlert(
    string AlertId,
    string SegmentId,
    double PositionKm,
    string ThreatCategory,
    double RiskScore,
    string RecommendedMitigation);

public record SubseaEarlyWarningReport(
    int TotalSegmentsMonitored,
    int TotalReadingsAnalyzed,
    List<CableThreatAlert> ActiveThreats,
    double PeakStrainObserved,
    bool OpticalRerouteRequired,
    string NetworkHealthStatus);
