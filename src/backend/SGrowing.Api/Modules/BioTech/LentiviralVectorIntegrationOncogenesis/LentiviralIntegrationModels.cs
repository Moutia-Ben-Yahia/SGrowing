namespace SGrowing.Api.Modules.BioTech.LentiviralVectorIntegrationOncogenesis;

public sealed record LentiviralIntegrationTelemetry(
    string TherapyBatchId,
    double AverageVectorCopyNumber, // VCN: e.g. 2.1 copies/cell (FDA limit <= 5.0)
    int TotalUniqueIntegrationSites, // e.g. 14,500 sites
    int IntegrationsWithin50kbOfOncogeneTss, // e.g. 2
    double DominantCloneAbundancePercent, // e.g. 4.5%
    double ShannonClonalityEntropy, // H: e.g. 4.2 (polyclonal > 3.5)
    bool SelfInactivatingSinArchitecturePresent // true
);

public sealed record LentiviralIntegrationAnalysisResult(
    string TherapyBatchId,
    double OncogeneProximityRiskPercent,
    string ClonalityProfile,
    string OncogenesisRiskRegime,
    string ClinicalReleaseGuidance,
    bool IsBatchSafeForInfusion
);

public interface ILentiviralVectorIntegrationOncogenesisEngine
{
    LentiviralIntegrationAnalysisResult AnalyzeIntegrationProfile(LentiviralIntegrationTelemetry telemetry);
}
