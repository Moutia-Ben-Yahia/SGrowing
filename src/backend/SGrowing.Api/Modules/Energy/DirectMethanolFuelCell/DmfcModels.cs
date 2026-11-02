namespace SGrowing.Api.Modules.Energy.DirectMethanolFuelCell;

public sealed record DmfcTelemetry(
    string CellStackId,
    double AnodeMethanolConcentrationMolarity, // e.g. 1.0 - 2.5 M
    double OperatingTemperatureCelsius, // e.g. 60 - 80 C
    double OperatingCurrentDensityMilliAmpsPerCm2, // e.g. 120 mA/cm^2
    double MembraneThicknessMicrometers, // Nafion 117 ~ 175 um, Nafion 112 ~ 50 um
    double CathodeAirStoichiometricRatio, // e.g. 2.0 - 4.0
    double OpenCircuitVoltageVolts, // Theoretical ~ 1.21 V, actual ~ 0.70 - 0.85 V
    double TotalCellActiveAreaCm2 // e.g. 50 cm^2
);

public sealed record DmfcAnalysisResult(
    string CellStackId,
    double MethanolCrossoverFluxMillimolesPerM2Sec,
    double FuelUtilizationEfficiencyPercent,
    double MeasuredOperatingCellVoltageVolts,
    double ElectricPowerDensityMilliWattsPerCm2,
    double TotalStackElectricPowerWatts,
    string DmfcOperatingRegime,
    string FuelCellControlGuidance,
    bool IsFuelCellPerformanceNominal
);

public interface IDirectMethanolFuelCellEngine
{
    DmfcAnalysisResult AnalyzeFuelCell(DmfcTelemetry telemetry);
}
