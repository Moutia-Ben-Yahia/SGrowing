namespace SGrowing.Api.Modules.BioTech.CrisprPrimeEditingFlap;

public record PrimeEditingDesignTelemetry(
    string DesignId,
    string TargetGeneName,
    int PegRnaPbsLengthNt,
    int PegRnaRttLengthNt,
    double PbsMeltingTempCelsius,
    double EditedFlapFreeEnergyKcalMol,
    double UneditedFlapFreeEnergyKcalMol,
    int Pe3SecondaryNickDistanceBp,
    double MmrEvasionScore);

public record PrimeEditingAnalysisResult(
    string DesignId,
    string TargetGeneName,
    double PredictedPrimeEditingEfficiencyPercent,
    double PredictedIndelByproductPercent,
    double EditedToIndelRatio,
    bool IsDesignApproved,
    string FlapResolutionState,
    string OptimizationRecommendation,
    string MolecularMechanisticSummary);
