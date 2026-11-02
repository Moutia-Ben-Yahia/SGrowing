namespace SGrowing.Api.Modules.BioTech.CrisprPrimeEditingFlap;

public class PrimeEditingFlapEngine
{
    public PrimeEditingAnalysisResult AnalyzeDesign(PrimeEditingDesignTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.DesignId))
        {
            throw new ArgumentException("Design ID cannot be null or empty.", nameof(telemetry));
        }

        if (telemetry.PegRnaPbsLengthNt <= 0 || telemetry.PegRnaRttLengthNt <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "PBS and RTT lengths must be positive.");
        }

        if (telemetry.Pe3SecondaryNickDistanceBp < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Secondary nick distance cannot be negative.");
        }

        // Flap hybridization free energy advantage: ddG = dG_edited - dG_unedited
        double deltaDeltaG = telemetry.EditedFlapFreeEnergyKcalMol - telemetry.UneditedFlapFreeEnergyKcalMol;

        // Base PE efficiency calculation
        double baseEfficiency = 45.0;

        // PBS Tm penalty (optimal ~31-33 °C)
        double tmDeviation = Math.Abs(telemetry.PbsMeltingTempCelsius - 32.0);
        double tmPenalty = Math.Min(20.0, tmDeviation * 2.5);

        // Flap stability boost: favorable ddG (e.g. -4.0 kcal/mol) enhances 3' flap retention over 5' unedited flap
        double flapBonus = deltaDeltaG < 0.0 ? Math.Min(18.0, Math.Abs(deltaDeltaG) * 3.5) : -Math.Min(20.0, deltaDeltaG * 4.0);

        // Secondary nick distance modulation
        double nickModifier;
        double indelRate;

        if (telemetry.Pe3SecondaryNickDistanceBp >= 40 && telemetry.Pe3SecondaryNickDistanceBp <= 90)
        {
            nickModifier = 12.0; // Optimal PE3 spacing
            indelRate = 0.9;
        }
        else if (telemetry.Pe3SecondaryNickDistanceBp > 0 && telemetry.Pe3SecondaryNickDistanceBp < 35)
        {
            nickModifier = 5.0;
            indelRate = 4.8; // High concurrent double-strand break indel hazard
        }
        else
        {
            nickModifier = 0.0; // Distant or absent secondary nick
            indelRate = 0.5;
        }

        // MMR Evasion effect
        double mmrMultiplier = 0.5 + (0.5 * Math.Clamp(telemetry.MmrEvasionScore, 0.0, 1.0));

        double predictedEfficiency = Math.Clamp((baseEfficiency - tmPenalty + flapBonus + nickModifier) * mmrMultiplier, 5.0, 92.0);
        double editedToIndelRatio = Math.Round(predictedEfficiency / Math.Max(indelRate, 0.1), 1);

        bool isApproved = predictedEfficiency >= 35.0 && indelRate <= 1.8;

        string state;
        string recommendation;
        string summary;

        if (indelRate > 2.5)
        {
            state = "EXCESSIVE_INDEL_HAZARD";
            recommendation = "Reposition PE3 secondary nick to 50-80 bp downstream to prevent coincident double-strand breaks and high indel rates.";
            summary = $"HIGH INDEL RISK: Secondary nick at {telemetry.Pe3SecondaryNickDistanceBp} bp causes elevated indel byproduct rate ({indelRate:F1}%), suppressing edited-to-indel ratio to {editedToIndelRatio}:1.";
        }
        else if (telemetry.MmrEvasionScore < 0.45)
        {
            state = "SUBOPTIMAL_MMR_REVERSION_RISK";
            recommendation = "Incorporate silent third-codon position bystander substitutions in RTT to evade MLH1/MSH2 mismatch repair recognition.";
            summary = $"MMR SENSITIVITY: Low MMR evasion score ({telemetry.MmrEvasionScore:F2}) reduces prime editing efficiency to {predictedEfficiency:F1}%. Flap is vulnerable to mismatch repair excision.";
        }
        else if (isApproved)
        {
            state = "OPTIMAL_PE5_TRANSDUCTION";
            recommendation = "Design validated for synthesis and therapeutic prime editing vector packaging.";
            summary = $"HIGH-PRECISION EDITING: Predicted efficiency {predictedEfficiency:F1}% with minimal {indelRate:F1}% indels (Edited/Indel Ratio: {editedToIndelRatio}:1). Robust 3' flap thermodynamic capture (ddG={deltaDeltaG:F2} kcal/mol).";
        }
        else
        {
            state = "MARGINAL_FLAP_RESOLUTION";
            recommendation = "Tune PBS length to adjust Tm closer to 32°C and optimize RTT sequence hybridization stability.";
            summary = $"Sub-threshold efficiency ({predictedEfficiency:F1}%). Refine PBS/RTT hybridization thermodynamics.";
        }

        return new PrimeEditingAnalysisResult(
            telemetry.DesignId,
            telemetry.TargetGeneName,
            Math.Round(predictedEfficiency, 1),
            Math.Round(indelRate, 1),
            editedToIndelRatio,
            isApproved,
            state,
            recommendation,
            summary);
    }
}
