namespace SGrowing.Api.Modules.Energy.SoecCoElectrolysis;

public record SoecCoElectrolysisTelemetry(
    string StackId,
    double OperatingTemperatureCelsius,
    double InletSteamFlowRateNlMin,
    double InletCo2FlowRateNlMin,
    double CurrentDensityAmpsPerCm2,
    double CellVoltageVolts,
    double CoConcentrationMolarFraction,
    double Co2ConcentrationMolarFraction);

public record SoecCoElectrolysisAnalysisResult(
    string StackId,
    double SteamToCarbonRatio,
    double ThermoneutralVoltageVolts,
    string ThermalOperatingRegime,
    double CarbonActivityCoefficient,
    bool IsCarbonCokingRiskDetected,
    string OperatingState,
    string RecommendedControlAction,
    string DiagnosticSummary);
