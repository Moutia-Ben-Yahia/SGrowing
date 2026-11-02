using FluentAssertions;
using SGrowing.Api.Modules.Energy.FusionDivertorDetachment;
using Xunit;

namespace SGrowing.Tests.Energy;

public class FusionDivertorDetachmentTests
{
    private readonly FusionDivertorDetachmentEngine _engine = new();

    [Fact]
    public void EvaluateDetachment_StableDetachedPlasma_MaintainsPuffFlowRate()
    {
        var tel = new DivertorTargetTelemetry(
            DivertorId: "ITER-OUTER-DIVERTOR-01",
            TargetHeatFluxMegaWattsPerM2: 3.8, // Safely below 10 MW/m2
            TargetElectronTemperatureElectronVolts: 2.8, // Safely below 5 eV
            IonSaturationCurrentAmpsPerCm2: 8.0,
            UpstreamSolDensityM3: 6.0e19,
            CoreRadiatedPowerFraction: 0.30,
            SeededImpurityGas: "Nitrogen",
            CurrentGasPuffFlowRateBarLitersPerSec: 15.0
        );

        var request = new DivertorDetachmentRequest(tel, new DivertorProtectionLimits());
        var decision = _engine.EvaluateDetachment(request);

        decision.DivertorId.Should().Be("ITER-OUTER-DIVERTOR-01");
        decision.DetachmentAchieved.Should().BeTrue();
        decision.CoreRadiativeCollapseRiskDetected.Should().BeFalse();
        decision.PlasmaExhaustRegime.Should().Be("STABLE_PARTIAL_DETACHMENT");
        decision.RecommendedPuffFlowRateBarLitersPerSec.Should().Be(15.0);
        decision.ActuatorDirective.Should().Contain("DETACHMENT_MAINTAINED");
    }

    [Fact]
    public void EvaluateDetachment_AttachedHotSheathHazard_BoostsImpuritySeeding()
    {
        // Attached regime: high electron temp (18.5 eV > 5.0 eV) and heat flux (16.2 MW/m2 > 10 MW/m2)
        var tel = new DivertorTargetTelemetry(
            DivertorId: "TOKAMAK-ATTACHED-MONOBLOCK",
            TargetHeatFluxMegaWattsPerM2: 16.2, // Exceeds 10 MW/m2 limit
            TargetElectronTemperatureElectronVolts: 18.5, // High erosion risk!
            IonSaturationCurrentAmpsPerCm2: 60.0,
            UpstreamSolDensityM3: 3.0e19,
            CoreRadiatedPowerFraction: 0.20,
            SeededImpurityGas: "Argon",
            CurrentGasPuffFlowRateBarLitersPerSec: 10.0
        );

        var request = new DivertorDetachmentRequest(tel, new DivertorProtectionLimits());
        var decision = _engine.EvaluateDetachment(request);

        decision.DetachmentAchieved.Should().BeFalse();
        decision.PlasmaExhaustRegime.Should().Be("ATTACHED_HOT_SHEATH_HAZARD");
        decision.RecommendedPuffFlowRateBarLitersPerSec.Should().BeGreaterThan(10.0);
        decision.ActuatorDirective.Should().Contain("BOOST_DIVERTOR_COOLING");
    }

    [Fact]
    public void EvaluateDetachment_CoreRadiativeCollapse_ThrottlesImpurityPuff()
    {
        // Core radiation exceeds 45% (reaches 52%)
        var tel = new DivertorTargetTelemetry(
            DivertorId: "TOKAMAK-CORE-COLLAPSE-RISK",
            TargetHeatFluxMegaWattsPerM2: 2.0,
            TargetElectronTemperatureElectronVolts: 2.0,
            IonSaturationCurrentAmpsPerCm2: 5.0,
            UpstreamSolDensityM3: 5.0e19,
            CoreRadiatedPowerFraction: 0.52, // Core radiation too high!
            SeededImpurityGas: "Neon",
            CurrentGasPuffFlowRateBarLitersPerSec: 25.0
        );

        var request = new DivertorDetachmentRequest(tel, new DivertorProtectionLimits(MaxCoreRadiatedFraction: 0.45));
        var decision = _engine.EvaluateDetachment(request);

        decision.CoreRadiativeCollapseRiskDetected.Should().BeTrue();
        decision.PlasmaExhaustRegime.Should().Be("CORE_RADIATIVE_COLLAPSE_WARNING");
        decision.RecommendedPuffFlowRateBarLitersPerSec.Should().BeLessThan(25.0);
        decision.ActuatorDirective.Should().Contain("THROTTLE_IMPURITY_SEEDING");
    }

    [Fact]
    public void EvaluateDetachment_CalculatesDegreeOfDetachmentSensibly()
    {
        var tel = new DivertorTargetTelemetry("DOD-TEST-DIVERTOR", UpstreamSolDensityM3: 4.0e19, IonSaturationCurrentAmpsPerCm2: 10.0);
        var request = new DivertorDetachmentRequest(tel, new DivertorProtectionLimits());
        var decision = _engine.EvaluateDetachment(request);

        decision.DegreeOfDetachment.Should().BeGreaterThan(0.5);
    }
}
