using FluentAssertions;
using SGrowing.Api.Modules.Security.CvQkdCoherentState;
using Xunit;

namespace SGrowing.Tests.Security;

public class CvQkdCoherentStateTests
{
    private readonly CvQkdCoherentStateEngine _engine = new();

    [Fact]
    public void AssessQuantumSecurity_NominalMetropolitanLink_YieldsSecureKeyRate()
    {
        var channel = new QuantumChannelParameters(
            LinkId: "CV-QKD-METRO-01",
            FiberLengthKm: 15.0,
            FiberAttenuationDbPerKm: 0.20,
            ExcessNoiseSnu: 0.003,
            DetectionQuantumEfficiency: 0.70,
            ElectronicNoiseSnu: 0.010,
            ModulationVarianceSnu: 5.0
        );

        var thresh = new CvQkdSecurityThresholds();
        var request = new CvQkdEvaluationRequest(channel, thresh);
        var assessment = _engine.AssessQuantumSecurity(request);

        assessment.LinkId.Should().Be("CV-QKD-METRO-01");
        assessment.QuantumKeyExchangeSecure.Should().BeTrue();
        assessment.SecurityState.Should().Be("QUANTUM_SECURE_COHERENT_LINK");
        assessment.AsymptoticSecretKeyRateBitsPerPulse.Should().BeGreaterThan(0.01);
        assessment.MutualInformationAliceBobBits.Should().BeGreaterThan(assessment.HolevoBoundEveBits);
        assessment.ChannelTransmissionFraction.Should().BeInRange(0.40, 0.60);
    }

    [Fact]
    public void AssessQuantumSecurity_SevereExcessNoise_AbortsDueToEavesdropping()
    {
        // Excess noise 0.025 SNU exceeds the 0.012 SNU maximum threshold
        var channel = new QuantumChannelParameters(
            LinkId: "CV-QKD-TAP-ATTACK",
            FiberLengthKm: 20.0,
            FiberAttenuationDbPerKm: 0.20,
            ExcessNoiseSnu: 0.025, // Eavesdropping noise!
            DetectionQuantumEfficiency: 0.60,
            ElectronicNoiseSnu: 0.020,
            ModulationVarianceSnu: 4.0
        );

        var thresh = new CvQkdSecurityThresholds(MaxAllowableExcessNoiseSnu: 0.012);
        var request = new CvQkdEvaluationRequest(channel, thresh);
        var assessment = _engine.AssessQuantumSecurity(request);

        assessment.QuantumKeyExchangeSecure.Should().BeFalse();
        assessment.SecurityState.Should().Be("EAVESDROPPING_ATTACK_DETECTED");
        assessment.RecommendedProtocolDirective.Should().Contain("ABORT_KEY_DISTRIBUTION");
    }

    [Fact]
    public void AssessQuantumSecurity_LongDistanceAttenuation_FlagsLowKeyRateWarning()
    {
        // 80 km standard fiber link (16 dB attenuation)
        var channel = new QuantumChannelParameters(
            LinkId: "CV-QKD-LONG-HAUL",
            FiberLengthKm: 80.0,
            FiberAttenuationDbPerKm: 0.20,
            ExcessNoiseSnu: 0.005,
            DetectionQuantumEfficiency: 0.65,
            ElectronicNoiseSnu: 0.015,
            ModulationVarianceSnu: 4.0
        );

        var thresh = new CvQkdSecurityThresholds(MinAllowableSecretKeyRateBitsPerPulse: 0.05);
        var request = new CvQkdEvaluationRequest(channel, thresh);
        var assessment = _engine.AssessQuantumSecurity(request);

        assessment.ChannelTransmissionFraction.Should().BeLessThan(0.05);
        assessment.QuantumKeyExchangeSecure.Should().BeFalse();
    }

    [Fact]
    public void AssessQuantumSecurity_CalculatesTransmissionAccurately()
    {
        // 10 km at 0.2 dB/km = 2 dB loss -> T = 10^(-0.2) = 0.63095
        var channel = new QuantumChannelParameters("CV-QKD-MATH-CHECK", FiberLengthKm: 10.0, FiberAttenuationDbPerKm: 0.20);
        var request = new CvQkdEvaluationRequest(channel, new CvQkdSecurityThresholds());
        var assessment = _engine.AssessQuantumSecurity(request);

        assessment.ChannelTransmissionFraction.Should().BeInRange(0.62, 0.64);
    }
}
