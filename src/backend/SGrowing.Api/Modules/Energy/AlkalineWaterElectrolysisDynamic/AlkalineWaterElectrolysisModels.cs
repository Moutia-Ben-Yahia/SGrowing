namespace SGrowing.Api.Modules.Energy.AlkalineWaterElectrolysisDynamic;

public sealed record AlkalineWaterElectrolysisTelemetry(
    string StackId,
    double StackOperatingTemperatureCelsius, // T: e.g. 70.0 C
    double KohElectrolyteConcentrationPercent, // e.g. 30.0 wt%
    double CurrentDensityAmperesPerSquareMeter, // j: e.g. 4500 A/m^2
    double RenewablePowerRampRatePercentPerSecond, // e.g. -15.0 %/s
    double DiaphragmGasCrossoverH2InO2Percent, // e.g. 0.8% H2 in O2 (LEL = 4.0%)
    double MeasuredCellVoltageVolts, // V_cell: e.g. 1.85 V
    double ReverseCurrentProtectionCurrentAmperes // I_polarization: e.g. 15.0 A trickle
);

public sealed record AlkalineWaterElectrolysisAnalysisResult(
    string StackId,
    double ReversibleVoltageVolts,
    double SpecificEnergyConsumptionKwhPerNm3,
    double ExplosionSafetyMarginPercent,
    string ElectrolyzerRegime,
    string StackProtectionGuidance,
    bool IsElectrolyzerStackHealthy
);

public interface IAlkalineWaterElectrolysisDynamicEngine
{
    AlkalineWaterElectrolysisAnalysisResult AnalyzeElectrolysisStack(AlkalineWaterElectrolysisTelemetry telemetry);
}
