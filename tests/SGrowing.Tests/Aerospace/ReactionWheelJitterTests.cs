using System;
using SGrowing.Api.Modules.Aerospace.ReactionWheelMicroVibrationJitter;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public sealed class ReactionWheelJitterTests
{
    private readonly ReactionWheelJitterEngine _engine = new();

    [Fact]
    public void AnalyzeWheelJitter_BalancedRotor_ReturnsQuietCruise()
    {
        var telemetry = new ReactionWheelJitterTelemetry
        {
            WheelAssemblyId = "RWA-TELESCOPE-01",
            SpacecraftPlatformName = "Space-Observatory-JWST",
            RotorSpeedRpm = 2800.0,
            StaticMassImbalanceGCm = 0.15, // <= 0.5
            DynamicMassImbalanceGCm2 = 0.45, // <= 1.2
            IsolationPlatformDampingRatio = 0.25,
            StructuralResonanceProximityPercent = 25.0, // Clear of resonance
            BearingHarmonicDisturbanceFactor = 1.05,
            MaxAllowableLosJitterNrad = 15.0
        };

        var result = _engine.AnalyzeWheelJitter(telemetry);

        Assert.Equal("RWA-TELESCOPE-01", result.WheelAssemblyId);
        Assert.True(result.IsStaticImbalanceWithinSpec);
        Assert.True(result.IsDynamicImbalanceWithinSpec);
        Assert.True(result.IsLosJitterRequirementMet);
        Assert.False(result.IsSpeedNearStructuralResonance);
        Assert.True(result.PredictedPayloadLosJitterNrad <= 15.0);
        Assert.Equal("ULTRA_STABLE_MICRO_VIBRATION_QUIET_CRUISE", result.WheelJitterStabilityRegime);
    }

    [Fact]
    public void AnalyzeWheelJitter_ResonanceProximity_TriggersResonanceAmplification()
    {
        var telemetry = new ReactionWheelJitterTelemetry
        {
            WheelAssemblyId = "RWA-RESONANCE-02",
            SpacecraftPlatformName = "Earth-Imaging-Sat",
            RotorSpeedRpm = 3600.0,
            StaticMassImbalanceGCm = 0.20,
            DynamicMassImbalanceGCm2 = 0.50,
            IsolationPlatformDampingRatio = 0.20,
            StructuralResonanceProximityPercent = 3.5, // Dangerous proximity < 10%
            MaxAllowableLosJitterNrad = 15.0
        };

        var result = _engine.AnalyzeWheelJitter(telemetry);

        Assert.True(result.IsSpeedNearStructuralResonance);
        Assert.Equal("STRUCTURAL_RESONANCE_JITTER_AMPLIFICATION", result.WheelJitterStabilityRegime);
        Assert.Contains("dangerously close", result.AttitudeControlMitigationPlan);
    }

    [Fact]
    public void AnalyzeWheelJitter_ExcessiveMassImbalance_TriggersImbalanceAlert()
    {
        var telemetry = new ReactionWheelJitterTelemetry
        {
            WheelAssemblyId = "RWA-IMBALANCE-03",
            SpacecraftPlatformName = "Comms-Constellation",
            RotorSpeedRpm = 4500.0,
            StaticMassImbalanceGCm = 0.85, // Excessive static imbalance > 0.5
            DynamicMassImbalanceGCm2 = 2.4, // Excessive dynamic imbalance > 1.2
            IsolationPlatformDampingRatio = 0.20,
            StructuralResonanceProximityPercent = 20.0,
            MaxAllowableLosJitterNrad = 15.0
        };

        var result = _engine.AnalyzeWheelJitter(telemetry);

        Assert.False(result.IsStaticImbalanceWithinSpec);
        Assert.False(result.IsDynamicImbalanceWithinSpec);
        Assert.Equal("ROTOR_MASS_IMBALANCE_OUT_OF_SPECIFICATION", result.WheelJitterStabilityRegime);
        Assert.Contains("Perform two-plane precision balance trim", result.AttitudeControlMitigationPlan);
    }

    [Fact]
    public void AnalyzeWheelJitter_HighJitterExceeded_TriggersLosJitterExceeded()
    {
        var telemetry = new ReactionWheelJitterTelemetry
        {
            WheelAssemblyId = "RWA-JITTER-04",
            SpacecraftPlatformName = "Spy-Satellite",
            RotorSpeedRpm = 5500.0, // High speed
            StaticMassImbalanceGCm = 0.45,
            DynamicMassImbalanceGCm2 = 1.10,
            IsolationPlatformDampingRatio = 0.06, // Poor damping
            StructuralResonanceProximityPercent = 15.0,
            MaxAllowableLosJitterNrad = 5.0 // Very tight target
        };

        var result = _engine.AnalyzeWheelJitter(telemetry);

        Assert.False(result.IsLosJitterRequirementMet);
        Assert.Equal("PAYLOAD_LINE_OF_SIGHT_JITTER_EXCEEDED", result.WheelJitterStabilityRegime);
        Assert.Contains("exceeds payload specification", result.AttitudeControlMitigationPlan);
    }

    [Fact]
    public void AnalyzeWheelJitter_NegativeSpeedOrZeroJitter_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeWheelJitter(null!));

        var invalidTelemetry = new ReactionWheelJitterTelemetry
        {
            WheelAssemblyId = "INV-WHEEL",
            RotorSpeedRpm = -100.0
        };

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeWheelJitter(invalidTelemetry));
    }
}
