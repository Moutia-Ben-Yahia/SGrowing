using System;

namespace SGrowing.Api.Modules.BioTech.EngineeredExosomeCargoLoading;

public sealed class EngineeredExosomeCargoLoadingEngine : IEngineeredExosomeCargoLoadingEngine
{
    public EngineeredExosomeAnalysisResult AnalyzeExosomeBatch(EngineeredExosomeTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.ExosomeBatchId))
        {
            throw new ArgumentException("Exosome batch ID must be provided.", nameof(telemetry));
        }

        if (telemetry.MeanHydrodynamicDiameterNanometers <= 0 || telemetry.PolydispersityIndex < 0 ||
            telemetry.TotalParticleConcentrationParticlesPerMl <= 0)
        {
            throw new ArgumentException("Diameter, PDI, and particle concentration must be positive.", nameof(telemetry));
        }

        var ligandRatio = telemetry.NanobodiesPerExosomeEstimate / 50.0;
        var rnaRatio = telemetry.RnaCargoEncapsulationEfficiencyPercent / 60.0;
        var escapeRatio = telemetry.EndosomalEscapeEfficiencyPercent / 25.0;

        var compositePotency = ligandRatio * rnaRatio * escapeRatio;

        // Colloidal stability: higher score when zeta potential is between -18 and -35 mV and PDI is low
        var zetaMargin = Math.Max(0.0, -telemetry.ZetaPotentialMillivolts); // want > 15
        var pdiFactor = Math.Max(0.0, 1.0 - (telemetry.PolydispersityIndex / 0.4));
        var stabilityScore = Math.Clamp((zetaMargin / 30.0) * pdiFactor * 100.0, 0.0, 100.0);

        string regime;
        string guidance;
        bool isPotent = compositePotency >= 1.0 &&
                        telemetry.PolydispersityIndex <= 0.25 &&
                        telemetry.ZetaPotentialMillivolts <= -15.0;

        if (telemetry.PolydispersityIndex > 0.35 || telemetry.ZetaPotentialMillivolts > -10.0)
        {
            regime = "COLLOIDAL_AGGREGATION_SIZE_HETEROGENEITY_HAZARD";
            guidance = $"Elevated PDI ({telemetry.PolydispersityIndex:F2}) or low zeta potential ({telemetry.ZetaPotentialMillivolts:F1} mV) indicates particle agglomeration. Implement tangential flow filtration (TFF) and trehalose cryoprotectant buffering.";
            isPotent = false;
        }
        else if (telemetry.NanobodiesPerExosomeEstimate < 20.0)
        {
            regime = "INSUFFICIENT_SURFACE_HOMING_LIGAND_DENSITY";
            guidance = $"Surface nanobody display density ({telemetry.NanobodiesPerExosomeEstimate:F1} copies/EV) is below targeted receptor avidity threshold. Optimize PTGFRN scaffold transfection in producer cells.";
            isPotent = false;
        }
        else if (telemetry.RnaCargoEncapsulationEfficiencyPercent < 25.0)
        {
            regime = "POOR_LUMINAL_CARGO_LOADING_EFFICIENCY";
            guidance = $"RNA encapsulation ({telemetry.RnaCargoEncapsulationEfficiencyPercent:F1}%) is suboptimal. Review MCP-MS2 hairpin stoichiometry or switch to active microfluidic electroporation.";
            isPotent = false;
        }
        else if (compositePotency >= 1.5 && telemetry.PolydispersityIndex <= 0.20)
        {
            regime = "HIGH_POTENCY_TARGETED_EXOSOME_DELIVERY_REGIME";
            guidance = $"Exceptional targeted delivery profile (Potency Index = {compositePotency:F2}, Stability = {stabilityScore:F0}%). Dense nanobody homing coupled to robust endosomal escape.";
        }
        else
        {
            regime = "NOMINAL_EXTRACELLULAR_VESICLE_CARGO_REGIME";
            guidance = $"Batch meets standard EV release criteria. Composite potency index is {compositePotency:F2}.";
        }

        return new EngineeredExosomeAnalysisResult(
            telemetry.ExosomeBatchId,
            Math.Round(compositePotency, 2),
            Math.Round(stabilityScore, 1),
            regime,
            guidance,
            isPotent
        );
    }
}
