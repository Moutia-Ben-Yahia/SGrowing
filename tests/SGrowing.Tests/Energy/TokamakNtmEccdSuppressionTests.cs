using SGrowing.Api.Modules.Energy.TokamakNtmEccdSuppression;
using Xunit;

namespace SGrowing.Tests.Energy;

public class TokamakNtmEccdSuppressionTests
{
    private readonly TokamakNtmEccdEngine _engine = new();

    [Fact]
    public void AnalyzeDischarge_NominalSuppression_ReturnsPlasmaStable()
    {
        var telemetry = new TokamakNtmEccdTelemetry
        {
            PulseDischargeId = "ITER-PULSE-10024",
            TokamakFacility = "ITER",
            MagneticIslandWidthCm = 2.2,
            CriticalSeedIslandWidthCm = 1.8,
            IslandRotationFrequencyKhz = 8.5,
            PoloidalBetaTheta = 1.30,
            EccdInjectedPowerMegaWatts = 15.0, // Strong stabilization
            EccdDepositionRadialOffsetCm = 0.35, // Well aligned
            EccdDepositionWidthCm = 2.0,
            NormalizedBetaDegradationPercent = 5.0,
            SafetyFactorAtRationalSurfaceQ = 2.0
        };

        var result = _engine.AnalyzeDischarge(telemetry);

        Assert.Equal("ITER-PULSE-10024", result.PulseDischargeId);
        Assert.True(result.IsPlasmaDischargeStable);
        Assert.True(result.IsEccdBeamAligned);
        Assert.True(result.IsIslandWidthControlled);
        Assert.True(result.IsModeLockedDisruptionAverted);
        Assert.True(result.CalculatedIslandGrowthRateInvSec < 0.0); // Suppressing
        Assert.True(result.EccdStabilizationEfficiencyPercent >= 70.0);
        Assert.True(result.ProjectedIslandSuppressionTimeMs > 0.0);
        Assert.Empty(result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeDischarge_LargeIslandSlowingDown_TriggersLockingAlert()
    {
        var telemetry = new TokamakNtmEccdTelemetry
        {
            PulseDischargeId = "PULSE-LOCKING-RISK",
            MagneticIslandWidthCm = 6.2, // Approaching locked mode
            IslandRotationFrequencyKhz = 1.2 // Mode slowed down
        };

        var result = _engine.AnalyzeDischarge(telemetry);

        Assert.False(result.IsModeLockedDisruptionAverted);
        Assert.False(result.IsPlasmaDischargeStable);
        Assert.Contains("NTM_ISLAND_GROWTH_APPROACHING_LOCKING", result.ActiveRegimes);
        Assert.NotEmpty(result.RecommendedMitigations);
    }

    [Fact]
    public void AnalyzeDischarge_BeamMisalignment_TriggersAlignmentAlert()
    {
        var telemetry = new TokamakNtmEccdTelemetry
        {
            PulseDischargeId = "PULSE-MISALIGNED",
            EccdDepositionRadialOffsetCm = 2.5, // Off target
            EccdDepositionWidthCm = 2.0
        };

        var result = _engine.AnalyzeDischarge(telemetry);

        Assert.False(result.IsEccdBeamAligned);
        Assert.Contains("ECCD_LAUNCHER_MIRROR_MISALIGNMENT", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeDischarge_SevereConfinementDegradation_TriggersBetaCollapseAlert()
    {
        var telemetry = new TokamakNtmEccdTelemetry
        {
            PulseDischargeId = "PULSE-BETA-COLLAPSE",
            NormalizedBetaDegradationPercent = 26.0 // Exceeds 18%
        };

        var result = _engine.AnalyzeDischarge(telemetry);

        Assert.Contains("HIGH_BETA_PLASMA_CONFINEMENT_COLLAPSE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeDischarge_GrowthRate_DecreasesWithInjectedPower()
    {
        var lowPowerTelemetry = new TokamakNtmEccdTelemetry
        {
            PulseDischargeId = "LOW-ECCD",
            MagneticIslandWidthCm = 2.5,
            EccdInjectedPowerMegaWatts = 4.0
        };

        var highPowerTelemetry = new TokamakNtmEccdTelemetry
        {
            PulseDischargeId = "HIGH-ECCD",
            MagneticIslandWidthCm = 2.5,
            EccdInjectedPowerMegaWatts = 18.0
        };

        var lowRes = _engine.AnalyzeDischarge(lowPowerTelemetry);
        var highRes = _engine.AnalyzeDischarge(highPowerTelemetry);

        // More power -> more negative growth rate (faster suppression)
        Assert.True(lowRes.CalculatedIslandGrowthRateInvSec > highRes.CalculatedIslandGrowthRateInvSec);
    }
}
