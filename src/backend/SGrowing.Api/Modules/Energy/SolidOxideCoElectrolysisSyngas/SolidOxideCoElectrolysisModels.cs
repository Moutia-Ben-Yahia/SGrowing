namespace SGrowing.Api.Modules.Energy.SolidOxideCoElectrolysisSyngas;

public sealed record SolidOxideCoElectrolysisTelemetry(
    string StackId,
    double StackOperatingTemperatureCelsius, // T: e.g. 800.0 C
    double InletSteamToCo2MolarRatio, // e.g. 2.0
    double TotalInletFlowRateNlPerMinute, // e.g. 25.0 Nl/min
    double OperatingCellVoltageVolts, // V_cell: e.g. 1.29 V
    double CurrentDensityAmperesPerSquareCm, // j: e.g. 1.05 A/cm^2
    double MeasuredOutletSyngasH2ToCoRatio, // H2/CO: e.g. 2.05
    double AreaSpecificResistanceOhmCm2 // ASR: e.g. 0.22 Ohm cm^2
);

public sealed record SolidOxideCoElectrolysisAnalysisResult(
    string StackId,
    double ThermoneutralVoltageVolts,
    double SyngasRatioDivergenceFromTarget,
    string ThermalOperatingMode,
    string CoElectrolysisRegime,
    string StackThermalGuidance,
    bool IsSyngasProductionOptimal
);

public interface ISolidOxideCoElectrolysisSyngasEngine
{
    SolidOxideCoElectrolysisAnalysisResult AnalyzeCoElectrolysisStack(SolidOxideCoElectrolysisTelemetry telemetry);
}
