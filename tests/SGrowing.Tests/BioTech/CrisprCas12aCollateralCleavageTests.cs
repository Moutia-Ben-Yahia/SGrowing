using System;
using SGrowing.Api.Modules.BioTech.CrisprCas12aCollateralCleavage;
using Xunit;

namespace SGrowing.Tests.BioTech;

public sealed class CrisprCas12aCollateralCleavageTests
{
    private readonly CrisprCas12aCollateralCleavageEngine _engine = new();

    [Fact]
    public void AnalyzeAssayDiagnostics_HighSnrWithoutMismatch_ConfirmsPositivePathogen()
    {
        var telemetry = new Cas12aAssayTelemetry(
            AssayId: "DETECTR-RUN-SARS2-881",
            TargetViralPathogenId: "SARS-CoV-2-JN.1",
            ElapsedReactionMinutes: 14.5,
            MeasuredFluorescenceRfu: 2850.0,
            BaselineBackgroundRfu: 350.0,
            DarkNoiseStandardDeviationRfu: 45.0,
            FluorescenceSlopeRfuPerMinute: 82.0,
            TargetBindingAffinityKdNanomolar: 2.5,
            SeedRegionMismatchCount: 0,
            TotalMismatchCount: 0,
            ReporterProbeConcentrationMicromolar: 2.0);

        var result = _engine.AnalyzeAssayDiagnostics(telemetry);

        Assert.Equal("POSITIVE_PATHOGEN_DETECTED", result.AssayDiagnosticVerdict);
        Assert.Equal("CONFIRM_VIRAL_INFECTION_AND_DISPATCH_HEALTHCARE_ALERT", result.DiagnosticActionRecommendation);
        Assert.True(result.IsPathogenConfirmed);
        Assert.True(result.SignalToNoiseRatio >= 50.0);
        Assert.Equal(14.5, result.EstimatedTimeToPositiveMinutes);
    }

    [Fact]
    public void AnalyzeAssayDiagnostics_ElevatedFluorescenceWithSeedMismatch_FlagsOffTarget()
    {
        var telemetry = new Cas12aAssayTelemetry(
            AssayId: "DETECTR-RUN-INFLUENZA-882",
            TargetViralPathogenId: "Influenza-A-H5N1",
            ElapsedReactionMinutes: 20.0,
            MeasuredFluorescenceRfu: 750.0,
            BaselineBackgroundRfu: 350.0,
            DarkNoiseStandardDeviationRfu: 45.0,
            FluorescenceSlopeRfuPerMinute: 22.0,
            TargetBindingAffinityKdNanomolar: 15.0,
            SeedRegionMismatchCount: 1,
            TotalMismatchCount: 1,
            ReporterProbeConcentrationMicromolar: 2.0);

        var result = _engine.AnalyzeAssayDiagnostics(telemetry);

        Assert.Equal("SUSPECT_OFF_TARGET_INTERFERENCE", result.AssayDiagnosticVerdict);
        Assert.Equal("FLAG_PROBABLE_OFF_TARGET_CROSS_REACTIVITY", result.DiagnosticActionRecommendation);
        Assert.False(result.IsPathogenConfirmed);
    }

    [Fact]
    public void AnalyzeAssayDiagnostics_LowFluorescence_MarksSampleNegative()
    {
        var telemetry = new Cas12aAssayTelemetry(
            AssayId: "DETECTR-RUN-RSV-883",
            TargetViralPathogenId: "RSV-B",
            ElapsedReactionMinutes: 30.0,
            MeasuredFluorescenceRfu: 390.0,
            BaselineBackgroundRfu: 350.0,
            DarkNoiseStandardDeviationRfu: 45.0,
            FluorescenceSlopeRfuPerMinute: 1.2,
            TargetBindingAffinityKdNanomolar: 50.0,
            SeedRegionMismatchCount: 0,
            TotalMismatchCount: 0,
            ReporterProbeConcentrationMicromolar: 2.0);

        var result = _engine.AnalyzeAssayDiagnostics(telemetry);

        Assert.Equal("NEGATIVE_SAMPLE", result.AssayDiagnosticVerdict);
        Assert.Equal("MARK_SAMPLE_NEGATIVE_AND_LOG_QUALITY_METRICS", result.DiagnosticActionRecommendation);
        Assert.False(result.IsPathogenConfirmed);
        Assert.True(result.SignalToNoiseRatio < 2.0);
    }

    [Fact]
    public void AnalyzeAssayDiagnostics_IntermediateFluorescence_ReturnsIndeterminate()
    {
        var telemetry = new Cas12aAssayTelemetry(
            AssayId: "DETECTR-RUN-EBV-884",
            TargetViralPathogenId: "EBV-TYPE-1",
            ElapsedReactionMinutes: 10.0,
            MeasuredFluorescenceRfu: 480.0,
            BaselineBackgroundRfu: 350.0,
            DarkNoiseStandardDeviationRfu: 45.0,
            FluorescenceSlopeRfuPerMinute: 8.0,
            TargetBindingAffinityKdNanomolar: 8.0,
            SeedRegionMismatchCount: 0,
            TotalMismatchCount: 0,
            ReporterProbeConcentrationMicromolar: 2.0);

        var result = _engine.AnalyzeAssayDiagnostics(telemetry);

        Assert.Equal("INDETERMINATE_INCONCLUSIVE", result.AssayDiagnosticVerdict);
        Assert.Equal("EXTEND_INCUBATION_AND_REPEAT_FLUORESCENCE_READ", result.DiagnosticActionRecommendation);
        Assert.False(result.IsPathogenConfirmed);
        Assert.True(result.SignalToNoiseRatio >= 2.0 && result.SignalToNoiseRatio < 5.0);
    }
}
