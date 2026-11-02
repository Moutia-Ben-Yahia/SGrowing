using System;

namespace SGrowing.Api.Modules.BioTech.AavCapsidDirectedEvolution;

public sealed class AavCapsidDirectedEvolutionEngine : IAavCapsidDirectedEvolutionEngine
{
    public AavCapsidAnalysisResult AnalyzeCapsidEvolution(AavCapsidTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.CapsidVariantId))
        {
            throw new ArgumentException("Capsid variant ID must be provided.", nameof(telemetry));
        }

        if (telemetry.PackagingYieldViralGenomesPerMl < 0 || telemetry.TargetTissueVectorGenomesPerCell < 0 ||
            telemetry.LiverBiodistributionVectorGenomesPerCell < 0)
        {
            throw new ArgumentException("Vector genome concentrations and yields cannot be negative.", nameof(telemetry));
        }

        var liverVg = Math.Max(0.01, telemetry.LiverBiodistributionVectorGenomesPerCell);
        var deTargetingRatio = telemetry.TargetTissueVectorGenomesPerCell / liverVg;

        var transcytosisFrac = Math.Clamp(telemetry.TranscytosisEndothelialEfficiencyPercent / 100.0, 0.0, 1.0);
        var nabEvasionClamped = Math.Clamp(telemetry.NeutralizingAntibodyEvasionTiterRatio, 0.1, 10.0);

        var compositeDeliveryScore = telemetry.TargetTissueVectorGenomesPerCell * transcytosisFrac * nabEvasionClamped;

        string regime;
        string guidance;
        bool isViable = deTargetingRatio >= 10.0 &&
                        telemetry.PackagingYieldViralGenomesPerMl >= 5.0e12 &&
                        telemetry.NeutralizingAntibodyEvasionTiterRatio >= 1.5;

        if (telemetry.PackagingYieldViralGenomesPerMl < 1.0e12)
        {
            regime = "CRITICAL_CAPSID_ASSEMBLY_PACKAGING_FAILURE";
            guidance = $"Capsid yield ({telemetry.PackagingYieldViralGenomesPerMl:E2} vg/mL) falls below the 1.0e12 threshold, indicating structural destabilization from the peptide insertion at {telemetry.InsertionVariableRegion}.";
            isViable = false;
        }
        else if (telemetry.LiverBiodistributionVectorGenomesPerCell > telemetry.TargetTissueVectorGenomesPerCell)
        {
            regime = "SEVERE_LIVER_SEQUESTRATION_HEPATOTOXIC_HAZARD";
            guidance = $"Liver sequestration ({telemetry.LiverBiodistributionVectorGenomesPerCell:F1} vg/cell) exceeds target tissue uptake ({telemetry.TargetTissueVectorGenomesPerCell:F1} vg/cell). High clinical risk of hepatotoxicity; iterate peptide motif.";
            isViable = false;
        }
        else if (deTargetingRatio >= 15.0 && telemetry.NeutralizingAntibodyEvasionTiterRatio >= 2.0)
        {
            regime = "OPTIMAL_NEUROTROPIC_HEPATIC_DETARGETED_REGIME";
            guidance = $"High target-to-liver selectivity ratio ({deTargetingRatio:F1}x) and robust NAb evasion ({telemetry.NeutralizingAntibodyEvasionTiterRatio:F1}x). Candidate variant is primed for in vivo translational development.";
        }
        else if (deTargetingRatio >= 5.0)
        {
            regime = "PARTIALLY_DETARGETED_TRANSLATIONAL_CANDIDATE";
            guidance = $"Favorable target biodistribution ({deTargetingRatio:F1}x over liver), but further peptide optimization could enhance humoral neutralization evasion.";
        }
        else
        {
            regime = "SUBOPTIMAL_BIOAVAILABILITY_REGIME";
            guidance = $"Marginal de-targeting ratio ({deTargetingRatio:F1}x). Vector distribution remains diluted across non-target peripheral organs.";
            isViable = false;
        }

        return new AavCapsidAnalysisResult(
            telemetry.CapsidVariantId,
            Math.Round(deTargetingRatio, 2),
            Math.Round(compositeDeliveryScore, 2),
            regime,
            guidance,
            isViable
        );
    }
}
