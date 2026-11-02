namespace SGrowing.Api.Modules.Pharma.HotMeltExtrusionPat;

public record ExtruderZoneTelemetry(
    int ZoneIndex,
    double BarrelTemperatureCelsius,
    double TargetTemperatureCelsius,
    double MotorTorqueNm,
    double ScrewSpeedRpm);

public record NearInfraredSpectra(
    string ReadingId,
    double ApiConcentrationWeightPercent,
    double TargetApiPercent,
    double TolerancePercent,
    double CrystallinityIndex,
    DateTimeOffset TimestampUtc);

public record HmeBatchMonitoringRequest(
    string BatchId,
    string DrugProductCode,
    List<ExtruderZoneTelemetry> BarrelZones,
    NearInfraredSpectra NirReading,
    double MeltPressureBar,
    double DieTemperatureCelsius);

public record HmeDivertAction(
    bool DivertGateActuated,
    string DivertReason,
    string CorrectiveActionCommand);

public record HmeBatchMonitoringResult(
    string BatchId,
    bool ProcessInStatisticalControl,
    double MeanBarrelTemperatureCelsius,
    double ApiPurityConcentrationPercent,
    HmeDivertAction DivertStatus,
    string QualityReleaseVerdict);
