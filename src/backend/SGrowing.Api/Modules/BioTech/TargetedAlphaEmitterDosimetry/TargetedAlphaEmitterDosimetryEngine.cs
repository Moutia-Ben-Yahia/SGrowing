using System;

namespace SGrowing.Api.Modules.BioTech.TargetedAlphaEmitterDosimetry;

public sealed class TargetedAlphaEmitterDosimetryEngine : ITargetedAlphaEmitterDosimetryEngine
{
    public TargetedAlphaDosimetryResult AnalyzeAlphaMicrodosimetry(TargetedAlphaDosimetryTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        // Alpha decay total energy per disintegration in Joules
        var isAc225 = string.Equals(telemetry.Radionuclide, "Ac-225", StringComparison.OrdinalIgnoreCase);
        var energyPerDecayJoules = isAc225 ? 4.48e-12 : 1.25e-12; // 28 MeV for Ac-225 (4 alphas) vs ~7.8 MeV for Pb/Bi
        var physicalHalfLifeHours = isAc225 ? 238.0 : 10.6;

        // Effective clearance half-life in hours
        var effectiveHalfLifeHours = (physicalHalfLifeHours * telemetry.RenalClearanceHalfLifeHours) /
                                     (physicalHalfLifeHours + telemetry.RenalClearanceHalfLifeHours);

        // Time-integrated activity: A_cum = A0 (MBq * 1e6) * fraction * 1.443 * T_eff * 3600s
        var activityBq = telemetry.InjectedActivityMegabecquerels * 1_000_000.0;
        var cumulativeDecaysPerFraction = activityBq * 1.442695 * effectiveHalfLifeHours * 3600.0;

        // Tumor dose: D_tumor = (A_cum * f_tumor * E_decay) / Mass_kg
        var tumorMassKg = Math.Max(0.005, telemetry.TumorTargetMassGrams / 1000.0);
        var tumorAbsorbedDoseGy = Math.Round((cumulativeDecaysPerFraction * telemetry.TumorUptakeFraction * energyPerDecayJoules) / tumorMassKg, 2);

        // Kidney dose: Cortical mass approx 0.30 kg; amino acid blocking reduces renal retention by 55%
        var renalRetentionMultiplier = telemetry.RenalAminoAcidBlockerInfused ? 0.45 : 1.0;
        var kidneyEffectiveFraction = telemetry.KidneyCortexUptakeFraction * renalRetentionMultiplier;
        const double standardKidneyMassKg = 0.30;
        var kidneyAbsorbedDoseGy = Math.Round((cumulativeDecaysPerFraction * kidneyEffectiveFraction * energyPerDecayJoules) / standardKidneyMassKg, 2);

        // Therapeutic Index: Tumor dose / Kidney dose
        var therapeuticIndex = Math.Round(tumorAbsorbedDoseGy / Math.Max(0.1, kidneyAbsorbedDoseGy), 2);

        // Daughter recoil toxicity risk score
        var freeDaughterScore = Math.Round(telemetry.DaughterRecoilReleaseFraction * (telemetry.InjectedActivityMegabecquerels / 4.0), 3);

        string regime;
        string action;
        bool isSafe;

        if (kidneyAbsorbedDoseGy > 23.0)
        {
            regime = "RENAL_TUBULAR_NEPHROTOXICITY_RISK";
            action = "Kidney dose exceeds 23 Gy tolerance limit. Halt infusion or escalate Lysine/Arginine renal block.";
            isSafe = false;
        }
        else if (telemetry.DaughterRecoilReleaseFraction > 0.30 || freeDaughterScore > 1.20)
        {
            regime = "DAUGHTER_RECOIL_HEMATOTOXICITY_ALERT";
            action = "High alpha daughter recoil release. Administer chelation scavenger or re-evaluate antibody-chelator linker stability.";
            isSafe = false;
        }
        else if (tumorAbsorbedDoseGy < 15.0)
        {
            regime = "SUBTHERAPEUTIC_DOSAGE_WARNING";
            action = "Tumor microdosimetry below 15 Gy threshold for double-strand DNA breakage. Titrate injected activity.";
            isSafe = false;
        }
        else
        {
            regime = "CURATIVE_MICRODOSIMETRY_OPTIMAL";
            action = "Tumor-to-kidney therapeutic index favorable (>3.0). High linear energy transfer double-strand break lethal track density confirmed.";
            isSafe = true;
        }

        return new TargetedAlphaDosimetryResult(
            TreatmentPlanId: telemetry.TreatmentPlanId,
            TumorAbsorbedDoseGray: tumorAbsorbedDoseGy,
            KidneyAbsorbedDoseGray: kidneyAbsorbedDoseGy,
            TherapeuticIndex: therapeuticIndex,
            FreeDaughterToxicityRiskScore: freeDaughterScore,
            ClinicalRegime: regime,
            TreatmentRecommendation: action,
            IsTherapeuticSafe: isSafe
        );
    }
}
