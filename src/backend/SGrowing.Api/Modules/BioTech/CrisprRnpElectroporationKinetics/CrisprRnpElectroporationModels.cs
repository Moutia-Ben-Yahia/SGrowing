namespace SGrowing.Api.Modules.BioTech.CrisprRnpElectroporationKinetics;

public sealed record CrisprRnpElectroporationTelemetry(
    string BatchId,
    string TargetCellType, // e.g. "PrimaryHumanCD4TCells", "HSC_CD34Plus"
    double ElectricPulseFieldStrengthVoltsPerCm, // E: e.g. 1400 - 2200 V/cm
    double PulseDurationMilliseconds, // tau: e.g. 5.0 - 20.0 ms
    double IntracellularRnpMolarConcentrationMicromolar, // e.g. 2.5 uM
    double CellSuspensionDensityMillionsPerMl, // e.g. 5.0 M/mL
    double PostPulseIncubationHours, // e.g. 24 h
    double IntracellularRnpHalfLifeHours, // t_1/2: e.g. 16.0 h
    double HighSequenceHomologyOffTargetSitesCount // e.g. 4 sites
);

public sealed record CrisprRnpElectroporationAnalysisResult(
    string BatchId,
    double PredictedMembranePermeabilizationFractionPercent,
    double PredictedPostPulseCellViabilityPercent,
    double OnTargetEditingEfficiencyPercent,
    double CumulativeOffTargetCleavageRatePercent,
    double CleavageSpecificityRatio,
    string ElectroporationKineticsRegime,
    string DeliveryOptimizationGuidance,
    bool IsElectroporationSuccessful
);

public interface ICrisprRnpElectroporationKineticsEngine
{
    CrisprRnpElectroporationAnalysisResult AnalyzeElectroporation(CrisprRnpElectroporationTelemetry telemetry);
}
