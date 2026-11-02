namespace SGrowing.Api.Modules.BioTech.TargetedAlphaEmitterDosimetry;

public sealed record TargetedAlphaDosimetryTelemetry(
    string TreatmentPlanId,
    string Radionuclide,
    double InjectedActivityMegabecquerels,
    double TumorTargetMassGrams,
    double TumorUptakeFraction,
    double KidneyCortexUptakeFraction,
    double RenalClearanceHalfLifeHours,
    double DaughterRecoilReleaseFraction,
    bool RenalAminoAcidBlockerInfused
);

public sealed record TargetedAlphaDosimetryResult(
    string TreatmentPlanId,
    double TumorAbsorbedDoseGray,
    double KidneyAbsorbedDoseGray,
    double TherapeuticIndex,
    double FreeDaughterToxicityRiskScore,
    string ClinicalRegime,
    string TreatmentRecommendation,
    bool IsTherapeuticSafe
);

public interface ITargetedAlphaEmitterDosimetryEngine
{
    TargetedAlphaDosimetryResult AnalyzeAlphaMicrodosimetry(TargetedAlphaDosimetryTelemetry telemetry);
}
