namespace SGrowing.Api.Modules.Energy.MicrobialFuelCellBioenergy;

public sealed record MicrobialFuelCellTelemetry(
    string ReactorId,
    double AnodeElectrodeSurfaceAreaM2, // e.g. 0.5 m^2 carbon felt
    double InfluentCodMilligramsPerLiter, // COD influent e.g. 1200 mg/L
    double EffluentCodMilligramsPerLiter, // COD effluent e.g. 180 mg/L
    double HydraulicRetentionTimeHours, // e.g. 12 hours
    double MeasuredOperatingCurrentMilliAmperes, // e.g. 45 mA
    double OpenCircuitVoltageVolts, // OCV e.g. 0.78 V
    double TotalInternalResistanceOhms, // R_int e.g. 15.0 Ohms
    double AnodeBiofilmThicknessMicrometers, // Biofilm thickness e.g. 45 um
    double SolutionPh // pH e.g. 7.2
);

public sealed record MicrobialFuelCellAnalysisResult(
    string ReactorId,
    double CodRemovalEfficiencyPercent,
    double CoulombicEfficiencyPercent,
    double MaximumPowerPointDensityMilliWattsPerM2,
    double OperatingPowerOutputMilliWatts,
    string BioenergyRegime,
    string BioreactorOptimizationGuidance,
    bool IsBioenergyNominal
);

public interface IMicrobialFuelCellBioenergyEngine
{
    MicrobialFuelCellAnalysisResult AnalyzeBioenergyProcess(MicrobialFuelCellTelemetry telemetry);
}
