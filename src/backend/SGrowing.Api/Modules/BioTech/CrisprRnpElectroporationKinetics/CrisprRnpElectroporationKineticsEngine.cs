using System;

namespace SGrowing.Api.Modules.BioTech.CrisprRnpElectroporationKinetics;

public sealed class CrisprRnpElectroporationKineticsEngine : ICrisprRnpElectroporationKineticsEngine
{
    public CrisprRnpElectroporationAnalysisResult AnalyzeElectroporation(CrisprRnpElectroporationTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var eField = Math.Max(200.0, telemetry.ElectricPulseFieldStrengthVoltsPerCm);
        var pulseTauMs = Math.Max(0.5, telemetry.PulseDurationMilliseconds);
        var rnpConcUm = Math.Max(0.1, telemetry.IntracellularRnpMolarConcentrationMicromolar);
        var halfLifeH = Math.Max(1.0, telemetry.IntracellularRnpHalfLifeHours);
        var offTargetSites = Math.Max(0.0, telemetry.HighSequenceHomologyOffTargetSitesCount);

        // Membrane permeabilization fraction: depends on electric field exceeding critical threshold (~1200 V/cm)
        var eFieldNorm = Math.Clamp((eField - 800.0) / 1000.0, 0.05, 1.5);
        var pulseNorm = Math.Sqrt(pulseTauMs / 10.0);
        var permeabilizationPercent = Math.Clamp(eFieldNorm * pulseNorm * 92.0, 5.0, 99.5);

        // Post-pulse cell viability: irreversible electroporation loss as E and tau increase
        var eLoss = Math.Max(0.0, (eField - 1200.0) / 100.0) * 2.2;
        var tauLoss = Math.Max(0.0, (pulseTauMs - 5.0)) * 1.1;
        var viabilityPercent = Math.Clamp(98.0 - eLoss - tauLoss, 5.0, 98.0);

        // On-target editing efficiency
        var rnpSatFactor = rnpConcUm / (rnpConcUm + 0.6);
        var onTargetEfficiency = Math.Clamp((permeabilizationPercent / 100.0) * 88.0 * rnpSatFactor, 2.0, 96.0);

        // Cumulative off-target cleavage: suppressed by transient RNP kinetics
        var offTargetPerSite = 0.08 * (halfLifeH / 16.0) * (rnpConcUm / 2.0);
        var cumulativeOffTargetPercent = Math.Clamp(offTargetSites * offTargetPerSite, 0.01, 15.0);

        // Specificity ratio: On-Target / Off-Target
        var specificityRatio = onTargetEfficiency / Math.Max(0.01, cumulativeOffTargetPercent);

        string regime;
        string guidance;
        bool isSuccessful;

        if (viabilityPercent < 60.0)
        {
            regime = "IRREVERSIBLE_MEMBRANE_LYSIS_HAZARD";
            guidance = "Electric pulse field strength or duration induced irreversible membrane pore expansion, dropping post-pulse cell viability below 60%. Reduce pulse voltage or shorten pulse duration.";
            isSuccessful = false;
        }
        else if (permeabilizationPercent < 50.0)
        {
            regime = "INSUFFICIENT_VOLTAGE_DELIVERY_FAILURE";
            guidance = "Electric field strength below threshold for robust membrane permeabilization (< 50%). Cas-gRNA RNP uptake impaired. Increase pulse voltage to > 1400 V/cm.";
            isSuccessful = false;
        }
        else if (cumulativeOffTargetPercent > 2.5)
        {
            regime = "ELEVATED_OFF_TARGET_CLEAVAGE_WARNING";
            guidance = "High intracellular RNP persistence or high homology off-target loci creating elevated non-specific genomic cuts (> 2.5%). Titrate down RNP concentration or use high-fidelity Cas9-HF1.";
            isSuccessful = false;
        }
        else
        {
            regime = "OPTIMAL_HIGH_VIABILITY_RNP_DELIVERY_REGIME";
            guidance = "Transient square-wave pulse achieved high cell membrane permeabilization while preserving cell viability. Rapid RNP clearance restricts off-target activity.";
            isSuccessful = true;
        }

        return new CrisprRnpElectroporationAnalysisResult(
            BatchId: telemetry.BatchId,
            PredictedMembranePermeabilizationFractionPercent: permeabilizationPercent,
            PredictedPostPulseCellViabilityPercent: viabilityPercent,
            OnTargetEditingEfficiencyPercent: onTargetEfficiency,
            CumulativeOffTargetCleavageRatePercent: cumulativeOffTargetPercent,
            CleavageSpecificityRatio: specificityRatio,
            ElectroporationKineticsRegime: regime,
            DeliveryOptimizationGuidance: guidance,
            IsElectroporationSuccessful: isSuccessful
        );
    }
}
