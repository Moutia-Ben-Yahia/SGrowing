using System;

namespace SGrowing.Api.Modules.BioTech.CrisprCas12aCollateralCleavage;

public sealed record Cas12aAssayTelemetry(
    string AssayId,
    string TargetViralPathogenId,
    double ElapsedReactionMinutes,
    double MeasuredFluorescenceRfu,
    double BaselineBackgroundRfu,
    double DarkNoiseStandardDeviationRfu,
    double FluorescenceSlopeRfuPerMinute,
    double TargetBindingAffinityKdNanomolar,
    int SeedRegionMismatchCount,
    int TotalMismatchCount,
    double ReporterProbeConcentrationMicromolar);

public sealed record Cas12aAssayResult(
    string AssayId,
    string TargetViralPathogenId,
    double SignalToNoiseRatio,
    double EstimatedTransCleavageVelocityUPerSec,
    double EstimatedTimeToPositiveMinutes,
    string AssayDiagnosticVerdict,
    string DiagnosticActionRecommendation,
    bool IsPathogenConfirmed,
    DateTimeOffset Timestamp);
