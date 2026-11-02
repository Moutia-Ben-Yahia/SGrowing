namespace SGrowing.Api.Modules.Aerospace.AerogelTpsAblationRecession;

public sealed record AerogelTpsTelemetry(
    string VehicleId,
    double FlightMachNumber,
    double DynamicPressureKiloPascals,
    double StagnationHeatFluxMegawattsPerSquareMeter,
    double AerogelLayerThicknessMillimeters,
    double AerogelBulkDensityKgPerCubicMeter,
    double HeatOfAblationMegaJoulesPerKg,
    double SubstructureBondlineTemperatureKelvin,
    double AngleOfAttackDegrees
);

public sealed record AerogelTpsAnalysisResult(
    string VehicleId,
    double SurfaceRecessionRateMillimetersPerSec,
    double ProjectedBondlinePeakTemperatureKelvin,
    double ThermalShockStressRatio,
    string AblationRegime,
    string ThermalControlAction,
    bool IsTpsNominal
);

public interface IAerogelTpsAblationRecessionEngine
{
    AerogelTpsAnalysisResult AnalyzeTpsAblationRecession(AerogelTpsTelemetry telemetry);
}
