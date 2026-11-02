using System;
using SGrowing.Api.Modules.Aerospace.RotatingDetonationRocketCombustor;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public sealed class RotatingDetonationRocketCombustorTests
{
    private readonly RotatingDetonationRocketCombustorEngine _engine = new();

    [Fact]
    public void AnalyzeDetonationCombustor_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeDetonationCombustor(null!));
    }

    [Fact]
    public void AnalyzeDetonationCombustor_InvalidGap_ThrowsArgumentException()
    {
        var telemetry = new RotatingDetonationTelemetry(
            EngineId: "RDRE-PROTO-1",
            CombustorOuterDiameterMillimeters: 100.0,
            AnnularGapWidthMillimeters: 120.0, // Gap exceeds diameter!
            ChamberTotalPressureBar: 40.0,
            DetonationWaveVelocityMetersPerSecond: 2400.0,
            TheoreticalCjVelocityMetersPerSecond: 2500.0,
            ObservedWaveCount: 2,
            CounterRotatingWaveIntensityRatio: 0.05,
            TotalPropellantMassFlowRateKgPerSec: 10.0
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeDetonationCombustor(telemetry));
    }

    [Fact]
    public void AnalyzeDetonationCombustor_StableDetonation_ReturnsStableSupersonicRegime()
    {
        var telemetry = new RotatingDetonationTelemetry(
            EngineId: "RDRE-LOX-METHANE-01",
            CombustorOuterDiameterMillimeters: 150.0,
            AnnularGapWidthMillimeters: 8.0,
            ChamberTotalPressureBar: 55.0,
            DetonationWaveVelocityMetersPerSecond: 2450.0,
            TheoreticalCjVelocityMetersPerSecond: 2550.0, // Deficit ~3.9%
            ObservedWaveCount: 2,
            CounterRotatingWaveIntensityRatio: 0.04,
            TotalPropellantMassFlowRateKgPerSec: 15.0
        );

        var result = _engine.AnalyzeDetonationCombustor(telemetry);

        Assert.NotNull(result);
        Assert.Equal("RDRE-LOX-METHANE-01", result.EngineId);
        Assert.True(result.CjVelocityDeficitPercent < 8.0);
        Assert.True(result.EstimatedPressureGainRatio > 1.05);
        Assert.True(result.SpecificImpulseEstimatedSeconds > 315.0);
        Assert.True(result.IsPressureGainCombustionStable);
        Assert.Equal("STABLE_SUPERSONIC_CONTINUOUS_DETONATION_REGIME", result.DetonationRegime);
    }

    [Fact]
    public void AnalyzeDetonationCombustor_ChaoticCounterWaves_ReturnsCounterRotatingHazard()
    {
        var telemetry = new RotatingDetonationTelemetry(
            EngineId: "RDRE-CHAOTIC-02",
            CombustorOuterDiameterMillimeters: 120.0,
            AnnularGapWidthMillimeters: 10.0,
            ChamberTotalPressureBar: 35.0,
            DetonationWaveVelocityMetersPerSecond: 2200.0,
            TheoreticalCjVelocityMetersPerSecond: 2500.0,
            ObservedWaveCount: 3,
            CounterRotatingWaveIntensityRatio: 0.45, // High counter wave!
            TotalPropellantMassFlowRateKgPerSec: 8.0
        );

        var result = _engine.AnalyzeDetonationCombustor(telemetry);

        Assert.NotNull(result);
        Assert.Equal("CHAOTIC_COUNTER_ROTATING_MODE_COLLISION_HAZARD", result.DetonationRegime);
        Assert.False(result.IsPressureGainCombustionStable);
        Assert.Contains("injector injection momentum angle", result.CombustorControlGuidance);
    }

    [Fact]
    public void AnalyzeDetonationCombustor_QuenchedFlame_ReturnsExtinction()
    {
        var telemetry = new RotatingDetonationTelemetry(
            EngineId: "RDRE-EXTINCT-03",
            CombustorOuterDiameterMillimeters: 150.0,
            AnnularGapWidthMillimeters: 8.0,
            ChamberTotalPressureBar: 5.0,
            DetonationWaveVelocityMetersPerSecond: 100.0,
            TheoreticalCjVelocityMetersPerSecond: 2500.0,
            ObservedWaveCount: 0, // No wave
            CounterRotatingWaveIntensityRatio: 0.0,
            TotalPropellantMassFlowRateKgPerSec: 1.0
        );

        var result = _engine.AnalyzeDetonationCombustor(telemetry);

        Assert.NotNull(result);
        Assert.Equal("DETONATION_QUENCHED_FLAME_EXTINCTION", result.DetonationRegime);
        Assert.False(result.IsPressureGainCombustionStable);
        Assert.Contains("spark plug re-ignition", result.CombustorControlGuidance);
    }
}
