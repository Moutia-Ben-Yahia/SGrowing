using System;
using SGrowing.Api.Modules.Quantum.SuperconductingCrosstalkCancellation;
using Xunit;

namespace SGrowing.Tests.Quantum;

public sealed class SuperconductingCrosstalkCancellationTests
{
    private readonly SuperconductingCrosstalkEngine _engine = new();

    [Fact]
    public void AnalyzeCrosstalk_OptimalProcessor_ReturnsHighFidelityGateReady()
    {
        var telemetry = new SuperconductingCrosstalkTelemetry
        {
            ProcessorId = "Q-PROC-54Q-01",
            ActiveQubitsCount = 54,
            MaxMutualFluxCrosstalkCoefficient = 0.035, // low off-diagonal
            ResidualStaticZzCouplingKhz = 12.0, // suppressed ZZ
            TwoQubitExchangeCouplingMhz = 8.0,
            QubitAnharmonicityMhz = -220.0,
            QubitQubitDetuningMhz = 220.0,
            SkinDepthFluxPulseDistortionPercent = 0.8,
            TargetCzGateFidelityPercent = 99.85
        };

        var result = _engine.AnalyzeCrosstalk(telemetry);

        Assert.Equal("Q-PROC-54Q-01", result.ProcessorId);
        Assert.True(result.IsFluxMatrixWellConditioned);
        Assert.True(result.IsResidualZzCouplingSuppressed);
        Assert.True(result.IsCzGateFidelityTargetMet);
        Assert.True(result.ProjectedTwoQubitCzFidelityPercent >= 99.80);
        Assert.Equal("ORTHOGONALIZED_FLUX_CONTROL_HIGH_FIDELITY_GATE_READY", result.CrosstalkSuppressionRegime);
    }

    [Fact]
    public void AnalyzeCrosstalk_HighMutualCoupling_TriggersIllConditionedMatrix()
    {
        var telemetry = new SuperconductingCrosstalkTelemetry
        {
            ProcessorId = "Q-PROC-ILLCOND-02",
            ActiveQubitsCount = 28,
            MaxMutualFluxCrosstalkCoefficient = 0.44, // Very strong crosstalk -> condition number > 2.5
            ResidualStaticZzCouplingKhz = 15.0,
            TwoQubitExchangeCouplingMhz = 10.0,
            QubitAnharmonicityMhz = -220.0,
            QubitQubitDetuningMhz = 200.0,
            SkinDepthFluxPulseDistortionPercent = 1.0
        };

        var result = _engine.AnalyzeCrosstalk(telemetry);

        Assert.False(result.IsFluxMatrixWellConditioned);
        Assert.Equal("ILL_CONDITIONED_FLUX_MUTUAL_COUPLING_MATRIX", result.CrosstalkSuppressionRegime);
        Assert.Contains("Condition number", result.CalibrationRecommendation);
    }

    [Fact]
    public void AnalyzeCrosstalk_ExcessiveResidualZz_TriggersStaticZzDephasing()
    {
        var telemetry = new SuperconductingCrosstalkTelemetry
        {
            ProcessorId = "Q-PROC-ZZ-03",
            ActiveQubitsCount = 36,
            MaxMutualFluxCrosstalkCoefficient = 0.04,
            ResidualStaticZzCouplingKhz = 85.0, // High ZZ coupling > 25 kHz
            TwoQubitExchangeCouplingMhz = 15.0,
            QubitAnharmonicityMhz = -220.0,
            QubitQubitDetuningMhz = 120.0,
            SkinDepthFluxPulseDistortionPercent = 1.0
        };

        var result = _engine.AnalyzeCrosstalk(telemetry);

        Assert.False(result.IsResidualZzCouplingSuppressed);
        Assert.Equal("PARASITIC_STATIC_ZZ_INTERACTION_DEPHASING", result.CrosstalkSuppressionRegime);
        Assert.Contains("Residual static ZZ coupling", result.CalibrationRecommendation);
    }

    [Fact]
    public void AnalyzeCrosstalk_SeverePulseDistortion_TriggersCzInfidelity()
    {
        var telemetry = new SuperconductingCrosstalkTelemetry
        {
            ProcessorId = "Q-PROC-DISTORT-04",
            ActiveQubitsCount = 16,
            MaxMutualFluxCrosstalkCoefficient = 0.04,
            ResidualStaticZzCouplingKhz = 20.0,
            TwoQubitExchangeCouplingMhz = 10.0,
            QubitAnharmonicityMhz = -220.0,
            QubitQubitDetuningMhz = 200.0,
            SkinDepthFluxPulseDistortionPercent = 12.0 // Heavy skin depth distortion
        };

        var result = _engine.AnalyzeCrosstalk(telemetry);

        Assert.False(result.IsCzGateFidelityTargetMet);
        Assert.Equal("PULSE_DISTORTION_CZ_INFIDELITY_ELEVATED", result.CrosstalkSuppressionRegime);
        Assert.Contains("pre-distorted multi-exponential deconvolution", result.CalibrationRecommendation);
    }

    [Fact]
    public void AnalyzeCrosstalk_ActiveQubitsOneOrLess_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeCrosstalk(null!));

        var invalidTelemetry = new SuperconductingCrosstalkTelemetry
        {
            ProcessorId = "INV-Q",
            ActiveQubitsCount = 1
        };

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeCrosstalk(invalidTelemetry));
    }
}
