using FluentAssertions;
using SGrowing.Api.Modules.BioTech.TargetedAlphaEmitterDosimetry;
using Xunit;

namespace SGrowing.Tests.BioTech;

public sealed class TargetedAlphaEmitterDosimetryTests
{
    private readonly TargetedAlphaEmitterDosimetryEngine _engine = new();

    [Fact]
    public void AnalyzeAlphaMicrodosimetry_CurativePlan_ReturnsOptimalRegime()
    {
        var telemetry = new TargetedAlphaDosimetryTelemetry(
            TreatmentPlanId: "PLAN-AC225-PSMA-01",
            Radionuclide: "Ac-225",
            InjectedActivityMegabecquerels: 8.0,
            TumorTargetMassGrams: 40.0,
            TumorUptakeFraction: 0.20,
            KidneyCortexUptakeFraction: 0.025,
            RenalClearanceHalfLifeHours: 28.0,
            DaughterRecoilReleaseFraction: 0.08,
            RenalAminoAcidBlockerInfused: true
        );

        var result = _engine.AnalyzeAlphaMicrodosimetry(telemetry);

        result.ClinicalRegime.Should().Be("CURATIVE_MICRODOSIMETRY_OPTIMAL");
        result.IsTherapeuticSafe.Should().BeTrue();
        result.TumorAbsorbedDoseGray.Should().BeGreaterThan(15.0);
        result.KidneyAbsorbedDoseGray.Should().BeLessThan(23.0);
        result.TherapeuticIndex.Should().BeGreaterThan(3.0);
    }

    [Fact]
    public void AnalyzeAlphaMicrodosimetry_ExcessKidneyDose_ReturnsNephrotoxicityRisk()
    {
        var telemetry = new TargetedAlphaDosimetryTelemetry(
            TreatmentPlanId: "PLAN-AC225-TOXIC-02",
            Radionuclide: "Ac-225",
            InjectedActivityMegabecquerels: 35.0, // High activity
            TumorTargetMassGrams: 50.0,
            TumorUptakeFraction: 0.15,
            KidneyCortexUptakeFraction: 0.40,    // High kidney retention
            RenalClearanceHalfLifeHours: 36.0,
            DaughterRecoilReleaseFraction: 0.08,
            RenalAminoAcidBlockerInfused: false   // No protective amino acid block
        );

        var result = _engine.AnalyzeAlphaMicrodosimetry(telemetry);

        result.ClinicalRegime.Should().Be("RENAL_TUBULAR_NEPHROTOXICITY_RISK");
        result.IsTherapeuticSafe.Should().BeFalse();
        result.KidneyAbsorbedDoseGray.Should().BeGreaterThan(23.0);
    }

    [Fact]
    public void AnalyzeAlphaMicrodosimetry_HighDaughterRecoil_ReturnsHematotoxicityAlert()
    {
        var telemetry = new TargetedAlphaDosimetryTelemetry(
            TreatmentPlanId: "PLAN-AC225-RECOIL-03",
            Radionuclide: "Ac-225",
            InjectedActivityMegabecquerels: 8.0,
            TumorTargetMassGrams: 40.0,
            TumorUptakeFraction: 0.20,
            KidneyCortexUptakeFraction: 0.02,
            RenalClearanceHalfLifeHours: 28.0,
            DaughterRecoilReleaseFraction: 0.42, // High recoil release of Fr-221/Bi-213
            RenalAminoAcidBlockerInfused: true
        );

        var result = _engine.AnalyzeAlphaMicrodosimetry(telemetry);

        result.ClinicalRegime.Should().Be("DAUGHTER_RECOIL_HEMATOTOXICITY_ALERT");
        result.IsTherapeuticSafe.Should().BeFalse();
        result.TreatmentRecommendation.Should().Contain("chelation scavenger");
    }

    [Fact]
    public void AnalyzeAlphaMicrodosimetry_SubtherapeuticActivity_ReturnsDosageWarning()
    {
        var telemetry = new TargetedAlphaDosimetryTelemetry(
            TreatmentPlanId: "PLAN-AC225-LOW-04",
            Radionuclide: "Ac-225",
            InjectedActivityMegabecquerels: 1.0, // Very low activity
            TumorTargetMassGrams: 80.0,
            TumorUptakeFraction: 0.05,
            KidneyCortexUptakeFraction: 0.01,
            RenalClearanceHalfLifeHours: 20.0,
            DaughterRecoilReleaseFraction: 0.05,
            RenalAminoAcidBlockerInfused: true
        );

        var result = _engine.AnalyzeAlphaMicrodosimetry(telemetry);

        result.ClinicalRegime.Should().Be("SUBTHERAPEUTIC_DOSAGE_WARNING");
        result.IsTherapeuticSafe.Should().BeFalse();
        result.TumorAbsorbedDoseGray.Should().BeLessThan(15.0);
    }
}
