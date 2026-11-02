namespace SGrowing.Tests.Quantum;

using FluentAssertions;
using SGrowing.Api.Modules.Quantum.SuperconductingQubitGateArbiter;
using Xunit;

public class SuperconductingQubitGateArbiterTests
{
    private readonly SuperconductingQubitGateArbiterEngine _engine = new();

    [Fact]
    public void SynthesizeGate_HighCoherenceTransmon_AchievesFaultTolerantFidelity()
    {
        var qubit = new QubitPhysicalParameters(
            QubitId: "Q-01",
            FrequencyGhz: 5.15,
            AnharmonicityMhz: -310.0,
            T1RelaxationMicroseconds: 120.0,
            T2DephasingMicroseconds: 95.0,
            ReadoutFidelityPercent: 99.4
        );

        // Low ZZ coupling with spectator qubit (5 kHz)
        var couplings = new List<InterQubitCoupling>
        {
            new("Q-01", "Q-02", StaticZzCouplingKhz: 5.0, ExchangeCouplingJKhz: 15.0)
        };

        var request = new GateSynthesisRequest(qubit, "X_PI", GateDurationNs: 24.0, couplings);
        var result = _engine.SynthesizeGate(request);

        result.QubitId.Should().Be("Q-01");
        result.GateType.Should().Be("X_PI");
        result.PredictedGateFidelityPercent.Should().BeGreaterThan(99.70);
        result.CalibrationApproved.Should().BeTrue();
        result.Pulse.DragCoefficientBeta.Should().NotBe(0.0);
        result.CalibrationAction.Should().Contain("CALIBRATION_CERTIFIED");
    }

    [Fact]
    public void SynthesizeGate_SevereZzCrossTalk_TriggersCalibrationWarning()
    {
        var qubit = new QubitPhysicalParameters("Q-02", 5.25, -300.0, 80.0, 65.0, 99.0);

        // High parasitic ZZ coupling (450 kHz)
        var couplings = new List<InterQubitCoupling>
        {
            new("Q-02", "Q-03", StaticZzCouplingKhz: 450.0, ExchangeCouplingJKhz: 50.0)
        };

        var request = new GateSynthesisRequest(qubit, "X_PI", GateDurationNs: 28.0, couplings);
        var result = _engine.SynthesizeGate(request);

        result.CalibrationApproved.Should().BeFalse();
        result.ResidualZzPhaseErrorRadians.Should().BeGreaterThan(0.02);
        result.CalibrationAction.Should().Contain("ZZ_CROSSTALK_WARNING");
    }

    [Fact]
    public void SynthesizeGate_HalfPiPulse_HalvesMicrowaveDriveAmplitude()
    {
        var qubit = new QubitPhysicalParameters("Q-03");

        var piRequest = new GateSynthesisRequest(qubit, "X_PI", 24.0);
        var halfPiRequest = new GateSynthesisRequest(qubit, "X_HALF_PI", 24.0);

        var piResult = _engine.SynthesizeGate(piRequest);
        var halfPiResult = _engine.SynthesizeGate(halfPiRequest);

        halfPiResult.Pulse.PeakAmplitudeMv.Should().BeApproximately(piResult.Pulse.PeakAmplitudeMv / 2.0, 0.5);
    }

    [Fact]
    public void SynthesizeGate_ComputesOptimalDragBeta_BasedOnAnharmonicity()
    {
        var transmon1 = new QubitPhysicalParameters("Q-1", 5.0, AnharmonicityMhz: -300.0, 90.0, 70.0, 99.0);
        var transmon2 = new QubitPhysicalParameters("Q-2", 5.0, AnharmonicityMhz: -200.0, 90.0, 70.0, 99.0);

        var res1 = _engine.SynthesizeGate(new GateSynthesisRequest(transmon1, "X_PI", 24.0));
        var res2 = _engine.SynthesizeGate(new GateSynthesisRequest(transmon2, "X_PI", 24.0));

        res1.Pulse.DragCoefficientBeta.Should().NotBe(res2.Pulse.DragCoefficientBeta);
        res1.Pulse.LeakageSuppressionDb.Should().BeGreaterThan(30.0);
    }
}
