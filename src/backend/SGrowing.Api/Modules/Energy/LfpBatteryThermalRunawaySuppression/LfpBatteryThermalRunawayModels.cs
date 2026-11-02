namespace SGrowing.Api.Modules.Energy.LfpBatteryThermalRunawaySuppression;

public sealed record LfpBatteryThermalRunawayTelemetry(
    string RackEnclosureId,
    double MaxCellSurfaceTemperatureCelsius, // T_cell: e.g. 85.0 C
    double TemperatureRateOfRiseCelsiusPerSecond, // dT/dt: e.g. 0.25 C/s
    double OffGasCarbonMonoxidePpm, // CO: e.g. 35.0 ppm
    double OffGasHydrogenPpm, // H2: e.g. 60.0 ppm
    double LowerExplosiveLimitContainerAtmospherePercent, // LEL%: e.g. 8.5%
    bool HighVoltageContactorClosed, // true
    bool SuppressionDelugeArmed // true
);

public sealed record LfpBatteryThermalRunawayAnalysisResult(
    string RackEnclosureId,
    double OffGasToxicityCompositeIndex,
    double CriticalTemperatureMarginCelsius,
    string BessSafetyRegime,
    string EmergencyMitigationGuidance,
    bool IsEnclosureSafe,
    bool ShouldTripHvContactor,
    bool ShouldDeploySuppressionDeluge
);

public interface ILfpBatteryThermalRunawaySuppressionEngine
{
    LfpBatteryThermalRunawayAnalysisResult AnalyzeBatterySafety(LfpBatteryThermalRunawayTelemetry telemetry);
}
