namespace SGrowing.Api.Modules.Energy.McfcCarbonCapturePolygeneration;

public sealed record McfcCaptureTelemetry(
    string PlantStackId,
    double CellOperatingTemperatureCelsius,
    double FlueGasInletCo2MoleFraction,
    double FlueGasInletFlowRateNm3PerHour,
    double CurrentDensityMilliampsPerCm2,
    double MeasuredCellVoltageVolts,
    double AnodeCo2PurityPercent,
    double ElectrolyteMatrixCorrosionRateMicronsPerYear,
    bool AnodeTailGasCatalyticOxidizerActive
);

public sealed record McfcCaptureAnalysisResult(
    string PlantStackId,
    double CarbonCaptureRatePercent,
    double NetElectricPowerOutputMegawatts,
    double ElectrochemicalCo2FluxMolesPerSecond,
    string PolygenerationRegime,
    string StackBalancingAction,
    bool IsStackElectrochemicalSound
);

public interface IMcfcCarbonCapturePolygenerationEngine
{
    McfcCaptureAnalysisResult AnalyzeCarbonCaptureCell(McfcCaptureTelemetry telemetry);
}
