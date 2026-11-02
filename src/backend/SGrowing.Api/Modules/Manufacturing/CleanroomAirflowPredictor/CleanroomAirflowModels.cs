namespace SGrowing.Api.Modules.Manufacturing.CleanroomAirflowPredictor;

public record CleanroomZone(
    string ZoneId,
    string BayName,
    double VolumeCubicMeters,
    int TargetIsoClass,
    int FfuCount,
    double NominalAirVelocityMps);

public record ParticleSensorTelemetry(
    string SensorId,
    string ZoneId,
    double ParticlesPerM3Gte01Um,
    double ParticlesPerM3Gte05Um,
    double AirVelocityMps,
    double DifferentialPressurePascals,
    DateTimeOffset TimestampUtc);

public record CleanroomAnalysisRequest(
    List<CleanroomZone> Zones,
    List<ParticleSensorTelemetry> SensorReadings,
    double ElectricityCostEurPerKwh = 0.22);

public record ZoneCleanlinessAudit(
    string ZoneId,
    string BayName,
    int TargetIsoClass,
    int AchievedIsoClass,
    double ParticleCount05Um,
    double AirChangesPerHour,
    double FfuSpeedAdjustmentPercent,
    bool InSpec,
    string OptimizationRecommendation);

public record CleanroomFacilityReport(
    int TotalBaysAudited,
    bool AllBaysCompliant,
    double TotalFfuPowerConsumptionKw,
    decimal DailyEnergyCostEur,
    List<ZoneCleanlinessAudit> BayAudits,
    string OverallAirflowQuality);
