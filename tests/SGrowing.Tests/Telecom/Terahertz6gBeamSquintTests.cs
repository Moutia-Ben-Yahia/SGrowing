using FluentAssertions;
using SGrowing.Api.Modules.Telecom.Terahertz6gBeamSquint;
using Xunit;

namespace SGrowing.Tests.Telecom;

public class Terahertz6gBeamSquintTests
{
    private readonly Terahertz6gBeamSquintEngine _engine = new();

    [Fact]
    public void EvaluateBeamSquint_WidebandEdgeSubcarrier_CalculatesSquintAndRecommendsTtd()
    {
        // 220 GHz carrier with 235 GHz edge subcarrier steered to 45 degrees on 1024-element array
        var cfg = new UmMimoArrayConfig(
            ArrayId: "THZ-ARRAY-1024-01",
            TotalAntennaElements: 1024,
            CarrierFrequencyGigaHertz: 220.0,
            TransmissionBandwidthGigaHertz: 30.0,
            TargetSteeringAngleDegrees: 45.0,
            TrueTimeDelayUnitsCount: 16,
            SubcarrierFrequencyGigaHertz: 235.0
        );

        var limits = new BeamSquintToleranceLimits(MaxAllowableBeamDriftDegrees: 2.0, MaxAllowableArrayGainLossDb: 3.0);
        var request = new BeamSquintEvaluationRequest(cfg, limits);
        var plan = _engine.EvaluateBeamSquint(request);

        plan.ArrayId.Should().Be("THZ-ARRAY-1024-01");
        plan.UncompensatedBeamDriftDegrees.Should().BeGreaterThan(2.0);
        plan.EdgeSubcarrierArrayGainLossDb.Should().BeGreaterThan(3.0);
        plan.BeamSquintExceedsTolerance.Should().BeTrue();
        plan.PrecodingArchitectureState.Should().Be("WIDEBAND_BEAM_SQUINT_DEGRADATION");
        plan.CompensatedBeamDriftDegrees.Should().BeLessThan(plan.UncompensatedBeamDriftDegrees);
        plan.OptimalTtdSubarraysCount.Should().BeGreaterThan(1);
        plan.RecommendedPrecodingDirective.Should().Contain("ACTIVATE_TTD_DELAY_LINES");
    }

    [Fact]
    public void EvaluateBeamSquint_BoresightSteering_ExperiencesNegligibleSquint()
    {
        // Steered directly at boresight (0 degrees): sin(0) = 0, so no squint occurs
        var cfg = new UmMimoArrayConfig(
            ArrayId: "THZ-BORESIGHT-ARRAY",
            TargetSteeringAngleDegrees: 0.0,
            SubcarrierFrequencyGigaHertz: 230.0
        );

        var request = new BeamSquintEvaluationRequest(cfg, new BeamSquintToleranceLimits());
        var plan = _engine.EvaluateBeamSquint(request);

        plan.UncompensatedBeamDriftDegrees.Should().Be(0.0);
        plan.EdgeSubcarrierArrayGainLossDb.Should().Be(0.0);
        plan.BeamSquintExceedsTolerance.Should().BeFalse();
        plan.PrecodingArchitectureState.Should().Be("SPATIAL_COHERENCE_MAINTAINED");
        plan.RecommendedPrecodingDirective.Should().Contain("NOMINAL_PRECODING");
    }

    [Fact]
    public void EvaluateBeamSquint_NarrowbandSubcarrier_MaintainsCoherence()
    {
        // Subcarrier very close to carrier frequency (220.5 GHz vs 220.0 GHz)
        var cfg = new UmMimoArrayConfig(
            ArrayId: "THZ-NARROW-BAND",
            TargetSteeringAngleDegrees: 30.0,
            SubcarrierFrequencyGigaHertz: 220.5
        );

        var request = new BeamSquintEvaluationRequest(cfg, new BeamSquintToleranceLimits());
        var plan = _engine.EvaluateBeamSquint(request);

        plan.BeamSquintExceedsTolerance.Should().BeFalse();
        plan.EdgeSubcarrierArrayGainLossDb.Should().BeLessThan(1.0);
    }

    [Fact]
    public void EvaluateBeamSquint_CompensatedDriftDecreasesWithTtdCount()
    {
        var cfg = new UmMimoArrayConfig(
            ArrayId: "THZ-TTD-SCALING",
            TargetSteeringAngleDegrees: 50.0,
            TrueTimeDelayUnitsCount: 32,
            SubcarrierFrequencyGigaHertz: 235.0
        );

        var request = new BeamSquintEvaluationRequest(cfg, new BeamSquintToleranceLimits());
        var plan = _engine.EvaluateBeamSquint(request);

        plan.CompensatedBeamDriftDegrees.Should().BeLessThan(plan.UncompensatedBeamDriftDegrees / 10.0);
    }
}
