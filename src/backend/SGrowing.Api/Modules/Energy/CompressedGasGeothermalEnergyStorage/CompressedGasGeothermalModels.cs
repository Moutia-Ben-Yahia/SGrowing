namespace SGrowing.Api.Modules.Energy.CompressedGasGeothermalEnergyStorage;

public sealed record CompressedGasGeothermalTelemetry(
    string StorageAquiferWellId,
    double AquiferDepthMeters, // e.g. 2100.0 m
    double AmbientGeothermalTemperatureCelsius, // T_rock: e.g. 115.0 C
    double InjectedFluidTemperatureCelsius, // T_inj: e.g. 45.0 C
    double InjectionCompressorPowerMegawatts, // W_comp: e.g. 25.0 MW
    double ExpansionTurbinePowerMegawatts, // W_turb: e.g. 31.5 MW
    double CurrentAquiferPorePressureBar, // P_pore: e.g. 220.0 bar
    double FormationFracturePressureBar // P_frac: e.g. 300.0 bar
);

public sealed record CompressedGasGeothermalAnalysisResult(
    string StorageAquiferWellId,
    double RoundTripExergyEfficiencyPercent,
    double CaprockFractureSafetyMarginPercent,
    double GeothermalThermalBoostCelsius,
    string GeoStorageRegime,
    string SubsurfaceReservoirGuidance,
    bool IsSubsurfaceStorageSafe
);

public interface ICompressedGasGeothermalEnergyStorageEngine
{
    CompressedGasGeothermalAnalysisResult AnalyzeGeothermalStorage(CompressedGasGeothermalTelemetry telemetry);
}
