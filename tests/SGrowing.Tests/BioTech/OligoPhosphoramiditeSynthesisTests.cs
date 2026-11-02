using System;
using SGrowing.Api.Modules.BioTech.OligonucleotidePhosphoramiditeSynthesis;
using Xunit;

namespace SGrowing.Tests.BioTech;

public sealed class OligoPhosphoramiditeSynthesisTests
{
    private readonly OligoPhosphoramiditeSynthesisEngine _engine = new();

    [Fact]
    public void AnalyzeSynthesis_OptimalParameters_ReturnsHighFidelityStable()
    {
        var telemetry = new OligoPhosphoramiditeTelemetry
        {
            SynthesisRunId = "SYNTH-RUN-100MER",
            TargetSequenceName = "CRISPR-sgRNA-20nt-SpCas9",
            OligoLengthNtds = 100,
            StepwiseCouplingYieldPercent = 99.5,
            InLineTritylAbsorbance498NmRatio = 0.995,
            AcetonitrileMoisturePpm = 12.0,
            CappingEfficiencyPercent = 99.0,
            ActivatorCouplingTimeSeconds = 90.0,
            OxidationIodineConcentrationMolar = 0.02,
            SynthesisScaleMicromoles = 1.0
        };

        var result = _engine.AnalyzeSynthesis(telemetry);

        Assert.Equal("SYNTH-RUN-100MER", result.SynthesisRunId);
        Assert.True(result.IsStepwiseCouplingYieldOptimal);
        Assert.True(result.IsSolventMoistureWithinSpecification);
        Assert.True(result.IsCappingEfficiencySufficient);
        Assert.True(result.IsFullLengthPurityTargetMet);
        Assert.True(result.CumulativeFullLengthProductPurityPercent >= 55.0); // 0.995^99 ~ 60.9%
        Assert.Equal("HIGH_FIDELITY_PHOSPHORAMIDITE_SYNTHESIS_STABLE", result.SynthesisIntegrityRegime);
    }

    [Fact]
    public void AnalyzeSynthesis_WetAcetonitrile_TriggersMoistureHydrolysisCrisis()
    {
        var telemetry = new OligoPhosphoramiditeTelemetry
        {
            SynthesisRunId = "SYNTH-WET-SOLVENT",
            TargetSequenceName = "Aptamer-Selection-Pool",
            OligoLengthNtds = 80,
            StepwiseCouplingYieldPercent = 99.4,
            InLineTritylAbsorbance498NmRatio = 0.994,
            AcetonitrileMoisturePpm = 75.0, // Wet solvent > 30 ppm
            CappingEfficiencyPercent = 99.0
        };

        var result = _engine.AnalyzeSynthesis(telemetry);

        Assert.False(result.IsSolventMoistureWithinSpecification);
        Assert.Equal("ANHYDROUS_SOLVENT_MOISTURE_HYDROLYSIS_CRISIS", result.SynthesisIntegrityRegime);
        Assert.Contains("exceeds the critical Karl Fischer limit", result.SynthesisOptimizationGuidance);
    }

    [Fact]
    public void AnalyzeSynthesis_SubOptimalScy_TriggersCouplingYieldDeficit()
    {
        var telemetry = new OligoPhosphoramiditeTelemetry
        {
            SynthesisRunId = "SYNTH-LOW-SCY",
            TargetSequenceName = "qPCR-Probe",
            OligoLengthNtds = 50,
            StepwiseCouplingYieldPercent = 98.2, // Below 99.2%
            InLineTritylAbsorbance498NmRatio = 0.982,
            AcetonitrileMoisturePpm = 15.0,
            CappingEfficiencyPercent = 99.0
        };

        var result = _engine.AnalyzeSynthesis(telemetry);

        Assert.False(result.IsStepwiseCouplingYieldOptimal);
        Assert.Equal("SUB_CRITICAL_STEPWISE_COUPLING_YIELD_DEFICIT", result.SynthesisIntegrityRegime);
        Assert.Contains("fell below target", result.SynthesisOptimizationGuidance);
    }

    [Fact]
    public void AnalyzeSynthesis_PoorCapping_TriggersNMinusOneMutationAccumulation()
    {
        var telemetry = new OligoPhosphoramiditeTelemetry
        {
            SynthesisRunId = "SYNTH-BAD-CAPPING",
            TargetSequenceName = "Gene-Fragment-Block",
            OligoLengthNtds = 90,
            StepwiseCouplingYieldPercent = 99.3,
            InLineTritylAbsorbance498NmRatio = 0.993,
            AcetonitrileMoisturePpm = 18.0,
            CappingEfficiencyPercent = 96.0 // Incomplete capping < 98.5%
        };

        var result = _engine.AnalyzeSynthesis(telemetry);

        Assert.False(result.IsCappingEfficiencySufficient);
        Assert.Equal("INCOMPLETE_CAPPING_N_MINUS_ONE_MUTATION_ACCUMULATION", result.SynthesisIntegrityRegime);
        Assert.Contains("N-1 truncated deletion sequences", result.SynthesisOptimizationGuidance);
    }

    [Fact]
    public void AnalyzeSynthesis_LengthOneOrLess_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeSynthesis(null!));

        var invalidTelemetry = new OligoPhosphoramiditeTelemetry
        {
            SynthesisRunId = "SYNTH-INV",
            OligoLengthNtds = 1
        };

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeSynthesis(invalidTelemetry));
    }
}
