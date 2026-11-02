using System;

namespace SGrowing.Api.Modules.BioTech.LentiviralVectorIntegrationOncogenesis;

public sealed class LentiviralVectorIntegrationOncogenesisEngine : ILentiviralVectorIntegrationOncogenesisEngine
{
    public LentiviralIntegrationAnalysisResult AnalyzeIntegrationProfile(LentiviralIntegrationTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.TherapyBatchId))
        {
            throw new ArgumentException("Therapy batch ID must be provided.", nameof(telemetry));
        }

        if (telemetry.AverageVectorCopyNumber <= 0 || telemetry.TotalUniqueIntegrationSites < 0 ||
            telemetry.ShannonClonalityEntropy < 0)
        {
            throw new ArgumentException("VCN, sites, and entropy must be non-negative.", nameof(telemetry));
        }

        var totalSites = Math.Max(1, telemetry.TotalUniqueIntegrationSites);
        var oncogeneRiskPercent = ((double)telemetry.IntegrationsWithin50kbOfOncogeneTss / totalSites) * 100.0;

        string clonalityProfile = telemetry.ShannonClonalityEntropy >= 3.5
            ? "POLYCLONAL_HEALTHY_DISTRIBUTION"
            : telemetry.ShannonClonalityEntropy >= 2.0
                ? "BROAD_OLIGOCLONAL_SPECTRUM"
                : "RESTRICTED_MONOCLONAL_EXPANSION";

        string regime;
        string guidance;
        bool isSafe = telemetry.SelfInactivatingSinArchitecturePresent &&
                      telemetry.AverageVectorCopyNumber <= 4.5 &&
                      telemetry.ShannonClonalityEntropy >= 2.5 &&
                      telemetry.DominantCloneAbundancePercent < 20.0;

        if (!telemetry.SelfInactivatingSinArchitecturePresent)
        {
            regime = "CRITICAL_NON_SIN_LTR_PROMOTER_INSERTIONAL_HAZARD";
            guidance = "Vector lacks Self-Inactivating (SIN) deletions in 3' LTR. Full-length intact viral promoters create severe insertional proto-oncogene transactivation risks; batch rejected.";
            isSafe = false;
        }
        else if (telemetry.DominantCloneAbundancePercent >= 30.0 || telemetry.ShannonClonalityEntropy < 2.0)
        {
            regime = "OLIGOCLONAL_EXPANSION_PRE_LEUKEMIC_DOMINANCE_HAZARD";
            guidance = $"Clonal dominance detected: top clone represents {telemetry.DominantCloneAbundancePercent:F1}% of all integrations (Entropy H = {telemetry.ShannonClonalityEntropy:F2} < 2.0). Quarantine batch and verify absence of insertional mutagenesis at proto-oncogene loci.";
            isSafe = false;
        }
        else if (telemetry.AverageVectorCopyNumber > 5.0)
        {
            regime = "EXCESSIVE_VCN_GENOMIC_INSTABILITY_WARNING";
            guidance = $"Vector Copy Number ({telemetry.AverageVectorCopyNumber:F2} copies/cell) exceeds the FDA recommended 5.0 threshold. Risk of multi-copy transactivation; optimize transduction MOI.";
            isSafe = false;
        }
        else if (telemetry.AverageVectorCopyNumber <= 4.0 && telemetry.ShannonClonalityEntropy >= 3.5 && telemetry.DominantCloneAbundancePercent < 10.0)
        {
            regime = "SAFE_POLYCLONAL_INTEGRATION_PROFILE_REGIME";
            guidance = $"Batch displays a safe, highly polyclonal genomic insertion profile (H = {telemetry.ShannonClonalityEntropy:F2}, VCN = {telemetry.AverageVectorCopyNumber:F2}). Certified safe for clinical patient infusion.";
        }
        else
        {
            regime = "MONITORED_OLIGOCLONAL_EQUILIBRIUM_REGIME";
            guidance = $"Moderate clonal diversity. VCN is {telemetry.AverageVectorCopyNumber:F2} copies/cell. Continue routine post-infusion integration site monitoring.";
        }

        return new LentiviralIntegrationAnalysisResult(
            telemetry.TherapyBatchId,
            Math.Round(oncogeneRiskPercent, 4),
            clonalityProfile,
            regime,
            guidance,
            isSafe
        );
    }
}
