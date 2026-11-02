namespace SGrowing.Api.Modules.BioTech.EngineeredExosomeCargoLoading;

public sealed record EngineeredExosomeTelemetry(
    string ExosomeBatchId,
    double MeanHydrodynamicDiameterNanometers, // e.g. 105.0 nm (target: 80-140 nm)
    double PolydispersityIndex, // PDI: e.g. 0.12 (target <= 0.20)
    double ZetaPotentialMillivolts, // e.g. -24.5 mV (target -18 to -35 mV)
    double NanobodiesPerExosomeEstimate, // e.g. 85.0 copies/EV (target >= 50)
    double RnaCargoEncapsulationEfficiencyPercent, // e.g. 68.5%
    double EndosomalEscapeEfficiencyPercent, // e.g. 34.0%
    double TotalParticleConcentrationParticlesPerMl // e.g. 2.5e11
);

public sealed record EngineeredExosomeAnalysisResult(
    string ExosomeBatchId,
    double CompositeDeliveryPotencyIndex,
    double ColloidalStabilityScore,
    string ExosomeQualityRegime,
    string BioprocessFormulationGuidance,
    bool IsBatchClinicallyPotent
);

public interface IEngineeredExosomeCargoLoadingEngine
{
    EngineeredExosomeAnalysisResult AnalyzeExosomeBatch(EngineeredExosomeTelemetry telemetry);
}
