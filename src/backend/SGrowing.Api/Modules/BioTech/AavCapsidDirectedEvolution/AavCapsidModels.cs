namespace SGrowing.Api.Modules.BioTech.AavCapsidDirectedEvolution;

public sealed record AavCapsidTelemetry(
    string CapsidVariantId,
    string ParentalSerotype, // e.g. "AAV9"
    string InsertionVariableRegion, // e.g. "VR-VIII"
    string PeptideInsertSequence, // e.g. "TLAVPFK"
    double TargetTissueVectorGenomesPerCell, // vg/cell: e.g. 18.5
    double LiverBiodistributionVectorGenomesPerCell, // vg/cell: e.g. 0.8
    double NeutralizingAntibodyEvasionTiterRatio, // IC50_var / IC50_wt: e.g. 4.2
    double PackagingYieldViralGenomesPerMl, // vg/mL: e.g. 1.5e13
    double TranscytosisEndothelialEfficiencyPercent // e.g. 78.0%
);

public sealed record AavCapsidAnalysisResult(
    string CapsidVariantId,
    double HepaticDeTargetingRatio,
    double CompositeGeneDeliveryScore,
    string CapsidEvolutionRegime,
    string TranslationalGuidance,
    bool IsVariantClinicallyViable
);

public interface IAavCapsidDirectedEvolutionEngine
{
    AavCapsidAnalysisResult AnalyzeCapsidEvolution(AavCapsidTelemetry telemetry);
}
