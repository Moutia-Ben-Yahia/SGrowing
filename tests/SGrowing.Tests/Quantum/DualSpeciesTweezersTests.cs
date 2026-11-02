using SGrowing.Api.Modules.Quantum.DualSpeciesNeutralAtomTweezers;
using Xunit;

namespace SGrowing.Tests.Quantum;

public class DualSpeciesTweezersTests
{
    private readonly DualSpeciesTweezersEngine _engine = new();

    [Fact]
    public void AnalyzeTweezers_NominalDualSpeciesArray_ReturnsArrayReady()
    {
        var telemetry = new DualSpeciesTweezersTelemetry
        {
            TweezerArrayUnitId = "TWEEZER-DUAL-001",
            SpeciesPair = "Rb87_Cs133_DualSpecies",
            InteratomicDistanceMicrons = 4.2,
            RydbergBlockadeRadiusMicrons = 6.8,
            InterSpeciesCnotGateFidelityPercent = 99.45,
            AtomLossRatePerTweezerHopPercent = 0.012,
            MidCircuitImagingCrosstalkScatterPercent = 0.035,
            TotalTargetArrayAtomCount = 256,
            DefectFreeAssemblySuccessRatePercent = 98.2
        };

        var result = _engine.AnalyzeTweezers(telemetry);

        Assert.NotNull(result);
        Assert.Equal("TWEEZER-DUAL-001", result.TweezerArrayUnitId);
        Assert.True(result.IsRydbergBlockadeEnforced);
        Assert.True(result.IsShufflingLossAcceptable);
        Assert.True(result.IsSpectatorCrosstalkSuppressed);
        Assert.True(result.IsDualSpeciesQubitArrayReady);
        Assert.Empty(result.ActiveRegimes);
        Assert.True(result.BlockadeRadiusMarginRatio > 1.5);
        Assert.True(result.ShufflingSurvivalProbabilityPercent > 99.0);
    }

    [Fact]
    public void AnalyzeTweezers_SmallBlockadeRadius_TriggersBlockadeLeakageAlert()
    {
        var telemetry = new DualSpeciesTweezersTelemetry
        {
            TweezerArrayUnitId = "TWEEZER-DUAL-002",
            InteratomicDistanceMicrons = 6.5,
            RydbergBlockadeRadiusMicrons = 6.8 // Ratio = 1.04 < 1.15
        };

        var result = _engine.AnalyzeTweezers(telemetry);

        Assert.False(result.IsRydbergBlockadeEnforced);
        Assert.False(result.IsDualSpeciesQubitArrayReady);
        Assert.Contains("RYDBERG_BLOCKADE_BREAKDOWN_LEAKAGE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeTweezers_ElevatedShufflingLoss_TriggersAtomLossSpikeAlert()
    {
        var telemetry = new DualSpeciesTweezersTelemetry
        {
            TweezerArrayUnitId = "TWEEZER-DUAL-003",
            AtomLossRatePerTweezerHopPercent = 0.045
        };

        var result = _engine.AnalyzeTweezers(telemetry);

        Assert.False(result.IsShufflingLossAcceptable);
        Assert.False(result.IsDualSpeciesQubitArrayReady);
        Assert.Contains("TWEEZER_REARRANGEMENT_ATOMIC_LOSS_SPIKE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeTweezers_FluorescenceScatter_TriggersCrosstalkAlert()
    {
        var telemetry = new DualSpeciesTweezersTelemetry
        {
            TweezerArrayUnitId = "TWEEZER-DUAL-004",
            MidCircuitImagingCrosstalkScatterPercent = 0.085
        };

        var result = _engine.AnalyzeTweezers(telemetry);

        Assert.False(result.IsSpectatorCrosstalkSuppressed);
        Assert.False(result.IsDualSpeciesQubitArrayReady);
        Assert.Contains("INTER_SPECIES_FLUORESCENCE_CROSSTALK", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeTweezers_LowAssemblyRate_FailsReadiness()
    {
        var telemetry = new DualSpeciesTweezersTelemetry
        {
            TweezerArrayUnitId = "TWEEZER-DUAL-005",
            DefectFreeAssemblySuccessRatePercent = 89.0
        };

        var result = _engine.AnalyzeTweezers(telemetry);

        Assert.False(result.IsDualSpeciesQubitArrayReady);
    }
}
