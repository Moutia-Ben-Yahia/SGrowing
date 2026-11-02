namespace SGrowing.Api.Modules.BioTech.MicrobialElectrosynthesis;

public record MicrobialElectrosynthesisTelemetry(
    string ReactorId,
    string BacterialStrain,
    double CathodePotentialVoltsVsShe,
    double CathodeCurrentDensityAmpsPerM2,
    double DissolvedCo2MilliMolar,
    double AcetateProductionRateGramsPerLiterDay,
    double OffGasHydrogenFraction,
    double BiofilmCoveragePercent,
    double ElectrodeSurfaceAreaM2);

public record MicrobialElectrosynthesisAnalysisResult(
    string ReactorId,
    string BacterialStrain,
    double AcetateFaradaicEfficiencyPercent,
    double AbioticHydrogenLossPercent,
    bool IsBiofilmDetachmentRiskDetected,
    string BiocathodeOperatingRegime,
    string CathodePotentialCommand,
    string BioelectrochemicalSummary);
