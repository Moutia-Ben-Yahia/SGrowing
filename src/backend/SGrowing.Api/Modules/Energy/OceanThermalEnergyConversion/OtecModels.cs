namespace SGrowing.Api.Modules.Energy.OceanThermalEnergyConversion;

public sealed record OtecTelemetry(
    string PlantId,
    double SurfaceSeawaterTemperatureCelsius, // T_warm: e.g. 26.5 C
    double DeepSeawaterTemperatureCelsius, // T_cold: e.g. 4.5 C (at 1000m depth)
    double WarmSeawaterVolumetricFlowRateM3PerSec, // e.g. 30.0 m^3/s
    double ColdSeawaterVolumetricFlowRateM3PerSec, // e.g. 18.0 m^3/s
    double DeepSeawaterPipeLengthMeters, // L: e.g. 1000 m
    double DeepSeawaterPipeDiameterMeters, // D: e.g. 2.5 m
    double WorkingFluidAmmoniaMassFlowRateKgPerSec, // m_dot_NH3: e.g. 220 kg/s
    double TurbineGeneratorIsentropicEfficiencyPercent, // e.g. 85.0%
    double SeawaterPumpingStationMechanicalEfficiencyPercent // e.g. 78.0%
);

public sealed record OtecAnalysisResult(
    string PlantId,
    double OceanTemperatureDifferentialDeltaTCelsius,
    double GrossTurbineElectricPowerMegaWatts,
    double ParasiticSeawaterPumpingPowerMegaWatts,
    double NetExportElectricPowerMegaWatts,
    double NetThermalCycleEfficiencyPercent,
    string OtecOperatingRegime,
    string PlantThermodynamicOptimizationGuidance,
    bool IsOtecNetPositive
);

public interface IOceanThermalEnergyConversionEngine
{
    OtecAnalysisResult AnalyzeOtecPlant(OtecTelemetry telemetry);
}
